using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;
using Dxs.Bsv.Tokens.Validation;
using Dxs.Tests.Shared;

namespace Dxs.Bsv.Tests.Conformance;

public class DstasProtocolOwnerFixturesTests
{
    private const string AuthorityChainId = "authority_multisig_freeze_unfreeze_cycle";
    private const string OwnerChainId = "owner_multisig_positive_spend";

    [Fact]
    public void AuthorityChain_RemainsParsableAndPreservesFreezeUnfreezeSemantics()
    {
        var chain = DstasProtocolOwnerFixture.LoadChain(AuthorityChainId);
        Assert.Equal(5, chain.Transactions.Count);

        var freeze = chain.Transactions.Single(x => x.Label == "freeze");
        var unfreeze = chain.Transactions.Single(x => x.Label == "unfreeze");

        AssertProtocolClassification(freeze);
        AssertProtocolClassification(unfreeze);

        var freezeTx = Transaction.Parse(freeze.TxHex, Network.Mainnet);
        var freezeOutput = freezeTx.Outputs.Single(x => x.Type == ScriptType.DSTAS);
        var freezeReader = LockingScriptReader.Read(new OutPoint(freezeTx, freezeOutput.Idx).ScriptPubKey, Network.Mainnet);

        Assert.NotNull(freezeReader.Dstas);
        Assert.True(freezeReader.Dstas!.Frozen);
        Assert.True(freezeReader.Dstas.FreezeEnabled);
        Assert.Equal("empty", freezeReader.Dstas.ActionType);
        Assert.Single(freezeReader.Dstas.ServiceFields);
        Assert.NotNull(freezeReader.Address);

        var unfreezeTx = Transaction.Parse(unfreeze.TxHex, Network.Mainnet);
        var unfreezeOutput = unfreezeTx.Outputs.Single(x => x.Type == ScriptType.DSTAS);
        var unfreezeReader = LockingScriptReader.Read(new OutPoint(unfreezeTx, unfreezeOutput.Idx).ScriptPubKey, Network.Mainnet);

        Assert.NotNull(unfreezeReader.Dstas);
        Assert.False(unfreezeReader.Dstas!.Frozen);
        Assert.True(unfreezeReader.Dstas.FreezeEnabled);
        Assert.Equal("empty", unfreezeReader.Dstas.ActionType);
        Assert.Single(unfreezeReader.Dstas.ServiceFields);
        Assert.NotNull(unfreezeReader.Address);
    }

    [Fact]
    public void OwnerChain_PreservesAddresslessOwnerAndPositiveSpendClassification()
    {
        var chain = DstasProtocolOwnerFixture.LoadChain(OwnerChainId);
        Assert.Equal(4, chain.Transactions.Count);

        var toOwner = chain.Transactions.Single(x => x.Label == "to_owner_multisig");
        var spend = chain.Transactions.Single(x => x.Label == "owner_multisig_spend");

        AssertProtocolClassification(toOwner);
        AssertProtocolClassification(spend);

        var toOwnerTx = Transaction.Parse(toOwner.TxHex, Network.Mainnet);
        var toOwnerOutput = toOwnerTx.Outputs.Single(x => x.Type == ScriptType.DSTAS);
        var toOwnerReader = LockingScriptReader.Read(new OutPoint(toOwnerTx, toOwnerOutput.Idx).ScriptPubKey, Network.Mainnet);

        Assert.NotNull(toOwnerReader.Dstas);
        Assert.Equal(20, toOwnerReader.Dstas!.Owner.Length);
        Assert.NotNull(toOwnerReader.Address);
        Assert.True(toOwnerReader.Dstas.FreezeEnabled);
        Assert.True(toOwnerReader.Dstas.ConfiscationEnabled);
        Assert.Equal(2, toOwnerReader.Dstas.ServiceFields.Count);
        Assert.Equal("empty", toOwnerReader.Dstas.ActionType);

        var spendTx = Transaction.Parse(spend.TxHex, Network.Mainnet);
        var spendOutput = spendTx.Outputs.Single(x => x.Type == ScriptType.DSTAS);
        var spendReader = LockingScriptReader.Read(new OutPoint(spendTx, spendOutput.Idx).ScriptPubKey, Network.Mainnet);

        Assert.NotNull(spendReader.Dstas);
        Assert.Equal(20, spendReader.Dstas!.Owner.Length);
        Assert.NotNull(spendReader.Address);
        Assert.True(spendReader.Dstas.FreezeEnabled);
        Assert.True(spendReader.Dstas.ConfiscationEnabled);
    }

    private static void AssertProtocolClassification(DstasProtocolTransactionFixture fixture)
    {
        var tx = Transaction.Parse(fixture.TxHex, Network.Mainnet);
        Assert.Equal(fixture.Prevouts.Count, tx.Inputs.Count);

        if (fixture.ExpectedSpendingType is not { } expectedSpendingType)
            return;

        Assert.Equal(expectedSpendingType, tx.Inputs[0].DstasSpendingType);

        var sut = new StasLineageEvaluator();
        var lineage = BuildLineageTransaction(tx, fixture);
        var result = sut.Evaluate(lineage);

        Assert.True(result.IsStas);
        Assert.Equal(expectedSpendingType, result.DstasSpendingType);
        Assert.Equal(fixture.ExpectedEventType, result.DstasEventType);
        Assert.Equal(fixture.ExpectedIsRedeem, result.IsRedeem);
    }

    private static StasLineageTransaction BuildLineageTransaction(Transaction tx, DstasProtocolTransactionFixture fixture)
    {
        var prevoutsByInput = fixture.Prevouts.ToDictionary(x => x.InputIndex);

        var inputs = tx.Inputs.Select((input, idx) =>
        {
            var prevout = prevoutsByInput[idx];
            var parentOutputs = Enumerable.Range(0, prevout.Vout + 1)
                .Select(vout => vout == prevout.Vout
                    ? ToLineageOutput(prevout.LockingScriptHex, prevout.Satoshis, Network.Mainnet)
                    : new StasLineageOutput(ScriptType.Unknown))
                .ToArray();

            return new StasLineageInput(
                input.TxId,
                (int)input.Vout,
                input.DstasSpendingType,
                new StasLineageParentTransaction(parentOutputs)
            );
        }).ToArray();

        var outputs = tx.Outputs.Select(output =>
        {
            var scriptBytes = output.GetScriptBytes(tx);
            return ToLineageOutput(scriptBytes.ToHexString(), (long)output.Satoshis, tx.Network, output);
        }).ToArray();

        return new StasLineageTransaction(tx.Id, inputs, outputs);
    }

    private static StasLineageOutput ToLineageOutput(string lockingScriptHex, long satoshis, Network network, Output? parsedOutput = null)
    {
        var reader = LockingScriptReader.Read(lockingScriptHex, network);
        var tokenId = parsedOutput?.TokenId ?? reader.GetTokenId();
        var address = parsedOutput?.Address?.Value ?? reader.Address?.Value;
        var hash160 = parsedOutput?.Address?.Hash160.ToHexString() ?? reader.Address?.Hash160.ToHexString();

        return new StasLineageOutput(
            reader.ScriptType,
            address,
            tokenId,
            hash160,
            reader.Dstas?.Frozen,
            reader.Dstas?.ActionType,
            reader.Dstas?.OptionalData is { Count: > 0 }
                ? string.Join("|", reader.Dstas.OptionalData.Select(x => x.ToHexString()))
                : null
        );
    }
}
