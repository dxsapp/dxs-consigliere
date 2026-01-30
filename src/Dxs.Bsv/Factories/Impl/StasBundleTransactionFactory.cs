using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dxs.Bsv.Factories.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Tokens;
using Dxs.Common.Extensions;

using Microsoft.Extensions.Logging;

namespace Dxs.Bsv.Factories.Impl;

public class StasBundleTransactionFactory(
    ITokenTransactionFactory stasTransactionFactory,
    IUtxoCache utxoCache,
    ILogger<StasBundleTransactionFactory> logger
) : IStasBundleTransactionFactory
{
    private readonly ILogger _logger = logger;

    public async Task<List<PreparedTransaction>> BuildStasTransactionsBundle(
        ITokenSchema tokenSchema,
        PrivateKey fromKey,
        PrivateKey feeKey,
        Address to,
        ulong satoshisToSend,
        List<OutPoint> utxos,
        OutPoint fundingOutPoint,
        ulong minSatoshisToSplit,
        params byte[][] data
    )
    {
        var transactions = new List<PreparedTransaction>();
        var totalFee = 0UL;
        var accumulatedBalance = utxos.Sum(x => x.Satoshis);

        if (utxos.Count > 1)
        {
            var (mergeTxs, mergeFee) = await Merge(
                tokenSchema,
                satoshisToSend,
                fromKey,
                feeKey,
                utxos,
                fundingOutPoint
            );

            transactions.AddRange(mergeTxs);
            totalFee += mergeFee;
        }

        var hasMerges = transactions.Any();

        var firstUtxo = utxos.First();
        var prevTx = hasMerges
            ? transactions.Last().Transaction
            : firstUtxo.Transaction;
        var stasOutPoint = hasMerges
            ? new OutPoint(prevTx, prevTx.Outputs[0].Idx)
            : firstUtxo;
        var feeOutPoint = hasMerges
            ? new OutPoint(prevTx, prevTx.Outputs[^1].Idx) // funding fee output always last
            : fundingOutPoint;

        if (stasOutPoint.Satoshis > satoshisToSend)
        {
            var splitPrepTx = await Split(
                new Destination(to, satoshisToSend),
                fromKey,
                feeKey,
                stasOutPoint,
                feeOutPoint,
                minSatoshisToSplit,
                data
            );

            transactions.Add(splitPrepTx);
            totalFee += splitPrepTx.Fee;
        }
        else
        {
            var transferPrepTx = await Transfer(
                tokenSchema,
                to,
                fromKey,
                feeKey,
                stasOutPoint,
                feeOutPoint,
                data
            );

            transactions.Add(transferPrepTx);
            totalFee += transferPrepTx.Fee;
        }

        Validate(transactions, fromKey.P2PkhAddress, to, satoshisToSend);

        _logger.LogDebug("Using {@UnspentOutputs} to create stas transaction with amount of {AmountTotal}/({AmountRequired} + {Fee})",
            utxos, accumulatedBalance, satoshisToSend, totalFee
        );

        return transactions;
    }

    private async Task<(List<PreparedTransaction>, ulong)> Merge(
        ITokenSchema tokenSchema,
        ulong satoshisToSend,
        PrivateKey fromKey,
        PrivateKey feeKey,
        List<OutPoint> utxos,
        OutPoint fundingOutPoint
    )
    {
        var transactions = new List<PreparedTransaction>();
        var totalFee = 0UL;
        var mergeLevels = new List<List<OutPoint>> { utxos };
        var current = mergeLevels.Last();

        var levelsBeforeTransfer = 0;

        Payment GetFundingPayment()
        {
            var outPoint = transactions.Any()
                ? new OutPoint(transactions[^1].Transaction, transactions[^1].Transaction.Outputs[^1].Idx)
                : fundingOutPoint;

            utxoCache.MarkUsed(outPoint, false);

            return new Payment(feeKey, outPoint);
        }

        while (current.Count != 1)
        {
            var newLevel = new List<OutPoint>();
            mergeLevels.Add(newLevel);

            if (levelsBeforeTransfer == 3)
            {
                levelsBeforeTransfer = 0;

                foreach (var outPoint in current)
                {
                    utxoCache.MarkUsed(outPoint, false);

                    var stasPayment = new Payment(fromKey, outPoint);
                    var feePayment = GetFundingPayment();
                    var destination = new Destination(fromKey.P2PkhAddress, outPoint.Satoshis);

                    var transferTxBuilder = await stasTransactionFactory.Transfer(
                        destination,
                        stasPayment,
                        feePayment,
                        tokenSchema
                    );
                    var transferTx = transferTxBuilder.BuildTransaction(fromKey.Network);

                    totalFee += transferTxBuilder.EstimatedFee;
                    newLevel.Add(new OutPoint(transferTx, 0));

                    var prepared = new PreparedTransaction
                    {
                        Transaction = transferTx,
                        UsedOutPoints = new List<OutPoint> { outPoint }
                    };
                    transactions.Add(prepared);
                }
            }
            else
            {
                levelsBeforeTransfer++;

                var mergeCounts = current.Count / 2;
                var remainder = current.Count % 2;

                if (remainder != 0)
                {
                    newLevel.Add(current.Last());
                }

                var currentIdx = 0;

                for (var i = 0; i < mergeCounts; i++)
                {
                    var outPoint1 = current[currentIdx++];
                    var outPoint2 = current[currentIdx++];

                    utxoCache.MarkUsed(outPoint1, false);
                    utxoCache.MarkUsed(outPoint2, false);

                    var fundingPayment = GetFundingPayment();
                    var lastMerge = mergeCounts == 1 && remainder == 0;
                    var inputSatoshis = outPoint1.Satoshis + outPoint2.Satoshis;

                    var destination1 = new Destination(fromKey.P2PkhAddress, inputSatoshis);
                    var destination2 = default(Destination?);

                    if (lastMerge && inputSatoshis != satoshisToSend)
                    {
                        destination1 = new Destination(fromKey.P2PkhAddress, satoshisToSend);
                        destination2 = new Destination(fromKey.P2PkhAddress, inputSatoshis - satoshisToSend);
                    }

                    var mergeTxBuilder = await stasTransactionFactory.Merge(
                        outPoint1,
                        outPoint2,
                        fromKey,
                        fundingPayment,
                        destination1,
                        destination2
                    );
                    var mergeTx = mergeTxBuilder.BuildTransaction(fromKey.Network);

                    totalFee += mergeTxBuilder.EstimatedFee;
                    newLevel.Add(new OutPoint(mergeTx, 0));

                    var prepared = new PreparedTransaction
                    {
                        Transaction = mergeTx,
                        UsedOutPoints = new List<OutPoint> { outPoint1, outPoint2 }
                    };
                    transactions.Add(prepared);
                }
            }

            current = newLevel;
        }

        return (transactions, totalFee);
    }

    private async Task<PreparedTransaction> Split(
        Destination destination1,
        PrivateKey fromKey,
        PrivateKey feeKey,
        OutPoint stasOutPoint,
        OutPoint feeOutPoint,
        ulong minSatoshisToSplit,
        params byte[][] data
    )
    {
        var splitSatoshis = stasOutPoint.Satoshis - destination1.Satoshis;
        var destinations = new List<Destination>
        {
            destination1
        };

        if (splitSatoshis > minSatoshisToSplit)
        {
            for (var i = 0; i < 3; i++)
            {
                var satoshis = splitSatoshis / 3 + (i == 2 ? splitSatoshis % 3 : 0);

                destinations.Add(new Destination(fromKey.P2PkhAddress, satoshis));
            }
        }
        else
        {
            destinations.Add(new Destination(fromKey.P2PkhAddress, splitSatoshis));
        }

        var txBuilder = await stasTransactionFactory.Split(
            destinations,
            new Payment(fromKey, stasOutPoint),
            new Payment(feeKey, feeOutPoint),
            data
        );

        utxoCache.MarkUsed(stasOutPoint, false);
        utxoCache.MarkUsed(feeOutPoint, false);

        return new PreparedTransaction
        {
            Transaction = txBuilder.BuildTransaction(fromKey.Network),
            UsedOutPoints = new()
            {
                stasOutPoint,
                feeOutPoint
            },
            FeeSize = txBuilder.Size,
            Fee = txBuilder.EstimatedFee
        };
    }

    private async Task<PreparedTransaction> Transfer(
        ITokenSchema tokenSchema,
        Address toAddress,
        PrivateKey fromKey,
        PrivateKey feeKey,
        OutPoint stasOutPoint,
        OutPoint feeOutPoint,
        params byte[][] data
    )
    {
        var txBuilder = await stasTransactionFactory.Transfer(
            new Destination(toAddress, stasOutPoint.Satoshis),
            new Payment(fromKey, stasOutPoint),
            new Payment(feeKey, feeOutPoint),
            tokenSchema,
            data
        );

        utxoCache.MarkUsed(stasOutPoint, false);
        utxoCache.MarkUsed(feeOutPoint, false);

        return new PreparedTransaction
        {
            Transaction = txBuilder.BuildTransaction(fromKey.Network),
            UsedOutPoints = new()
            {
                stasOutPoint,
                feeOutPoint,
            },
            FeeSize = txBuilder.Size,
            Fee = txBuilder.EstimatedFee
        };
    }

    private void Validate(List<PreparedTransaction> preparedTransactions, Address from, Address to, ulong satoshisToSend)
    {
        for (var i = 0; i < preparedTransactions.Count; i++)
        {
            var pTx = preparedTransactions[i];

            if (i == preparedTransactions.Count - 1)
            {
                Validate(pTx.Transaction, to, satoshisToSend);
            }
            else
            {
                foreach (var output in pTx.Transaction.Outputs)
                {
                    if (output.Type == ScriptType.P2STAS && output.Address != from)
                        throw new Exception("Invalid receiver in service transaction");
                }
            }
        }
    }

    private void Validate(Transaction transaction, Address to, ulong satoshisToSend)
    {
        var outputsToDestinations = transaction
            .Outputs
            .Where(x => x.Address == to)
            .ToList();

        if (outputsToDestinations.Count == 0)
            throw new Exception("Output transaction has no required outputs");

        if (outputsToDestinations.Count > 1)
            throw new Exception("Output transaction has more than one outputs to destination");

        if (outputsToDestinations[0].Satoshis != satoshisToSend)
            throw new Exception("Stas transaction has invalid amount");

        if (outputsToDestinations[0].Type != ScriptType.P2STAS)
            throw new Exception("Output transaction has invalid type");
    }
}
