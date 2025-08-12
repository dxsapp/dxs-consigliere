using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dxs.Bsv.Factories.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Transactions.Build;
using Dxs.Common.Exceptions.Transactions;
using Dxs.Common.Extensions;
using Dxs.Common.Utils;
using Microsoft.Extensions.Logging;

namespace Dxs.Bsv.Factories.Impl;

public class P2PkhTransactionFactory(
    IUtxoCache utxoCache,
    ILogger<P2PkhTransactionFactory> logger
): IP2PkhTransactionFactory
{
    private readonly ILogger _logger = logger;

    private readonly NamedAsyncLock _addressLock = new();

    public async Task<PreparedTransaction> BuildP2PkhTransaction(
        IReadOnlyList<PrivateKey> fromKeys,
        IReadOnlyList<Destination> destinations,
        FeeType feeType,
        params byte[][] notes
    )
    {
        using var scope = _logger.BeginScope("Building {@Transaction}",
            new
            {
                From = string.Join(',', fromKeys.Select(f => f.P2PkhAddress.Value)),
                To = string.Join(',', destinations.Select(f => f.Address.Value)),
                Amount = destinations.Sum(x => x.Satoshis)
            }
        );

        var transactionBuilder = TransactionBuilder.Init();

        if (notes?.Length > 0)
            transactionBuilder.AddNullDataOutput(notes);

        var totalSatoshis = 0UL;
        foreach (var destination in destinations)
        {
            totalSatoshis += destination.Satoshis;
            transactionBuilder.AddP2PkhOutput(destination.Satoshis, destination.Address, destination.Data);
        }

        transactionBuilder.AddP2PkhOutput(0, fromKeys[^1].P2PkhAddress);

        if (feeType is FeeType.Calculated or FeeType.Constant && destinations.Count != 1)
            throw new Exception("For calculated fee type the count of destinations must be 1");

        var utxos = new List<OutPoint>();
        var accumulatedBalance = 0UL;
        var amountSatoshis = feeType == FeeType.Constant
            ? totalSatoshis - DefaultFees.Total.ToSatoshis()
            : totalSatoshis;
        var enough = false;
        var fee = 0UL;

        foreach (var fromKey in fromKeys)
        {
            var fromAddress = fromKey.P2PkhAddress;

            using (await _addressLock.LockAsync(fromAddress.Value))
            {
                OutPoint? firstUtxo = null;
                while (await utxoCache.GetNextUtxoOrNull(fromAddress) is { } utxo && firstUtxo != utxo) // prevent cycled enumeration
                {
                    firstUtxo = utxo;

                    utxos.Add(utxo);
                    accumulatedBalance += utxo.Satoshis;

                    transactionBuilder.AddInput(utxo, fromKey);
                    fee = transactionBuilder.GetFee(DefaultFees.Rate);

                    if (fee > amountSatoshis)
                    {
                        _logger.LogDebug(
                            "Building p2pkh transaction, fee amount {Fee} greater than sending amount {amountSatoshis}",
                            fee, amountSatoshis
                        );
                    }

                    if (accumulatedBalance > amountSatoshis + fee)
                    {
                        if (feeType == FeeType.Calculated)
                        {
                            transactionBuilder.Outputs[^2].Value = amountSatoshis - fee;
                            transactionBuilder.Outputs[^1].Value = accumulatedBalance - amountSatoshis;
                        }
                        else
                        {
                            if (feeType == FeeType.Constant)
                            {
                                transactionBuilder.Outputs[^2].Value = amountSatoshis;
                            }

                            transactionBuilder.Outputs[^1].Value = accumulatedBalance - amountSatoshis - fee;
                        }

                        enough = true;
                        break;
                    }
                }
            }

            if (enough) break;
        }

        if (!enough)
        {
            throw new NotEnoughMoneyException(
                fromKeys.Select(k => k.P2PkhAddress.Value).ToHashSet(),
                accumulatedBalance,
                string.Join(',', destinations.Select(f => f.Address.Value)),
                totalSatoshis,
                fee,
                null
            );
        }

        _logger.LogDebug("Using {@UnspentOutputs} to create transaction with amount of {AmountTotal}/({AmountRequired} + {Fee})",
            utxos, accumulatedBalance, totalSatoshis, fee
        );

        return new PreparedTransaction
        {
            Transaction = transactionBuilder.SignAndBuildTransaction(Network.Mainnet),
            UsedOutPoints = utxos,
            FeeSize = transactionBuilder.Size,
            Fee = fee
        };
    }
}