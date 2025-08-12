using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Transactions.Build;

namespace Dxs.Bsv.Tokens.Stas;

public class StasProtocolTransactionFactory(IBroadcastProvider broadcastProvider) : ITokenTransactionFactory
{
    public async Task<TransactionBuilder> CreateContract(
        ITokenSchema schema,
        IList<Payment> tokenPayments,
        Address contractOwnerAddress,
        ulong satoshisToToken,
        Payment feePayment
    )
    {
        if (!tokenPayments.Any())
            throw new Exception("Provide at least one token outpoint");

        var tx = TransactionBuilder.Init();
        var totalSatoshis = feePayment.OutPoint.Satoshis;

        foreach (var payment in tokenPayments)
        {
            tx.AddInput(payment.OutPoint, payment.PrivateKey);
            totalSatoshis += payment.OutPoint.Satoshis;
        }

        if (totalSatoshis < satoshisToToken)
            throw new Exception("Not enough satoshis to issue tokens");

        var satoshisPerByte = await GetSatoshisPerByte();

        return tx
            .AddP2PkhOutput(satoshisToToken, contractOwnerAddress, schema.ToBytes())
            .AddInput(feePayment.OutPoint, feePayment.PrivateKey)
            .AddChangeOutputWithFee(
                feePayment.PrivateKey.P2PkhAddress,
                totalSatoshis - satoshisToToken,
                satoshisPerByte
            )
            .Sign();
    }

    public async Task<TransactionBuilder> Issue(
        ITokenSchema schema,
        IList<Destination> destinations,
        Payment contractPayment,
        Payment feePayment,
        params byte[][] data
    )
    {
        var builder = TransactionBuilder.Init();
        var fundingSatoshis = feePayment.OutPoint.Satoshis;
        var tokenSatoshis = 0UL;

        builder.AddInput(contractPayment.OutPoint, contractPayment.PrivateKey)
            .AddInput(feePayment.OutPoint, feePayment.PrivateKey);

        foreach (var destination in destinations)
        {
            tokenSatoshis += destination.Satoshis;
            builder.AddStasOutput(destination.Satoshis, schema, destination.Address, destination.Data);
        }

        if (tokenSatoshis != contractPayment.OutPoint.Satoshis)
            throw new Exception("Only "); //TODO

        var feeRate = await GetSatoshisPerByte();

        var feeIdx = builder.Outputs.Count;

        if (data.Any())
            builder.AddNullDataOutput(data);

        return builder
            .AddChangeOutputWithFee(
                feePayment.PrivateKey.P2PkhAddress,
                fundingSatoshis,
                feeRate,
                feeIdx
            )
            .Sign();
    }

    public async Task<TransactionBuilder> Transfer(
        Destination destination,
        Payment tokenPayment,
        Payment feePayment,
        ITokenSchema schema = null,
        params byte[][] data
    )
    {
        if (tokenPayment.OutPoint.Satoshis != destination.Satoshis)
            throw new Exception("Destination satoshis must equal input satoshis"); //TODO

        var feeRate = await GetSatoshisPerByte();
        var builder = TransactionBuilder
            .Init()
            .AddInput(tokenPayment.OutPoint, tokenPayment.PrivateKey)
            .AddInput(feePayment.OutPoint, feePayment.PrivateKey)
            .AddStasOutput(destination.Satoshis, schema!, destination.Address, destination.Data);

        if (data.Any())
            builder.AddNullDataOutput(data);

        builder.AddChangeOutputWithFee(
            feePayment.PrivateKey.P2PkhAddress,
            feePayment.OutPoint.Satoshis,
            feeRate,
            1
        );

        return builder.Sign();
    }

    public async Task<TransactionBuilder> Split(
        IList<Destination> destinations,
        Payment tokenPayment,
        Payment feePayment,
        params byte[][] data
    )
    {
        if (destinations.Count == 0)
            throw new Exception("Provide at least one destination");
        if (destinations.Count > 4)
            throw new Exception("Provided too many destinations, STAS supports up to 4 destinations");

        var feeRate = await GetSatoshisPerByte();
        var builder = TransactionBuilder
            .Init()
            .AddInput(tokenPayment.OutPoint, tokenPayment.PrivateKey)
            .AddInput(feePayment.OutPoint, feePayment.PrivateKey);

        foreach (var destination in destinations)
            builder.AddStasOutput(destination.Satoshis, destination.Address, tokenPayment.OutPoint.ScriptPubKey, destination.Data);

        var feeIdx = builder.Outputs.Count;

        if (data.Any())
            builder.AddNullDataOutput(data);

        builder.AddChangeOutputWithFee(
            feePayment.PrivateKey.P2PkhAddress,
            feePayment.OutPoint.Satoshis,
            feeRate,
            feeIdx
        );

        return builder.Sign();
    }

    public async Task<TransactionBuilder> Merge(
        OutPoint stasOutPoint1,
        OutPoint stasOutPoint2,
        PrivateKey senderPrivateKey,
        Payment feePayment,
        Destination destination1,
        Destination? destination2 = null,
        params byte[][] data
    )
    {
        if (!stasOutPoint1.ScriptPubKey.SequenceEqual(stasOutPoint2.ScriptPubKey))
            throw new Exception("Only identical outputs can be merged");

        var inputTokenSatoshis = stasOutPoint1.Satoshis + stasOutPoint2.Satoshis;
        var outputTokenSatoshis = destination1.Satoshis + (destination2?.Satoshis ?? 0ul);

        if (outputTokenSatoshis != inputTokenSatoshis)
            throw new Exception("Input amount and output amount must be equal");

        var feeRate = await GetSatoshisPerByte();
        var builder = TransactionBuilder
            .Init()
            .AddStasMergeInput(stasOutPoint1, senderPrivateKey)
            .AddStasMergeInput(stasOutPoint2, senderPrivateKey)
            .AddInput(feePayment.OutPoint, feePayment.PrivateKey)
            .AddStasOutput(destination1.Satoshis, destination1.Address, stasOutPoint1.ScriptPubKey, destination1.Data);

        if (destination2 is { } splitDestination)
            builder.AddStasOutput(splitDestination.Satoshis, splitDestination.Address, stasOutPoint1.ScriptPubKey, splitDestination.Data);

        var feeIdx = builder.Outputs.Count;

        if (data.Any())
            builder.AddNullDataOutput(data);

        builder
            .AddChangeOutputWithFee(
                feePayment.PrivateKey.P2PkhAddress,
                feePayment.OutPoint.Satoshis,
                feeRate,
                feeIdx
            );

        return builder.Sign();
    }

    public async Task<TransactionBuilder> Redeem(
        ITokenSchema schema,
        //TODO Allow merge
        Payment tokenPayment,
        Payment feePayment,
        IList<Destination> splitDestinations,
        params byte[][] data
    )
    {
        var network = tokenPayment.OutPoint.Address.Network;
        var redeemAddress = new Address(schema.TokenId.FromHexString(), ScriptType.P2PKH, network);
        var splitAmount = splitDestinations.Aggregate(0UL, (current, destination) => current + destination.Satoshis);

        if (splitAmount > tokenPayment.OutPoint.Satoshis)
            throw new Exception("Insufficient input amount");

        var feeRate = await GetSatoshisPerByte();
        var builder = TransactionBuilder
            .Init()
            .AddInput(tokenPayment.OutPoint, tokenPayment.PrivateKey)
            .AddInput(feePayment.OutPoint, feePayment.PrivateKey)
            .AddP2PkhOutput(tokenPayment.OutPoint.Satoshis - splitAmount, redeemAddress);

        foreach (var destination in splitDestinations)
            builder.AddStasOutput(destination.Satoshis, destination.Address, tokenPayment.OutPoint.ScriptPubKey);

        var feeIdx = builder.Outputs.Count;

        if (data.Any())
            builder.AddNullDataOutput(data);

        builder
            .AddChangeOutputWithFee(
                feePayment.PrivateKey.P2PkhAddress,
                feePayment.OutPoint.Satoshis,
                feeRate,
                feeIdx
            );

        return builder.Sign();
    }

    private async Task<decimal> GetSatoshisPerByte()
    {
        var fees = new List<decimal>();
        var fee = await broadcastProvider.SatoshisPerByte();
        fees.Add(fee);

        return fees.Min();
    }
}