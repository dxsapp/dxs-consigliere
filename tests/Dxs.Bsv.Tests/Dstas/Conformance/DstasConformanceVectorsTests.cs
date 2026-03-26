using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;
using Dxs.Bsv.Tokens.Validation;
using Dxs.Tests.Shared;

namespace Dxs.Bsv.Tests.Conformance;

public class DstasConformanceVectorsTests
{
    [Fact]
    public void ConformanceVectors_AreParsableAndMeetBasicDstasAssumptions()
    {
        var vectors = DstasConformanceVectorFixture.LoadAll();
        Assert.Equal(12, vectors.Count);

        foreach (var vector in vectors)
        {
            var tx = Transaction.Parse(vector.TxHex, Network.Mainnet);
            Assert.Equal(vector.Prevouts.Count, tx.Inputs.Count);
            Assert.True(vector.Prevouts.Count >= 2, $"Vector {vector.Id} must include STAS + fee prevouts.");

            var stasPrevoutReader = LockingScriptReader.Read(vector.Prevouts[0].LockingScriptHex, Network.Mainnet);
            Assert.Equal(ScriptType.DSTAS, stasPrevoutReader.ScriptType);

            var feePrevoutReader = LockingScriptReader.Read(vector.Prevouts[1].LockingScriptHex, Network.Mainnet);
            Assert.Equal(ScriptType.P2PKH, feePrevoutReader.ScriptType);

            if (vector.ExpectedSuccess)
            {
                Assert.True(vector.FailedInputs == null || vector.FailedInputs.Count == 0,
                    $"Vector {vector.Id} is successful and must not contain failedInputs.");
            }
            else
            {
                Assert.NotNull(vector.FailedInputs);
                Assert.NotEmpty(vector.FailedInputs!);
                foreach (var failedInput in vector.FailedInputs!)
                    Assert.InRange(failedInput, 0, tx.Inputs.Count - 1);
            }

            var expectedSpendingType = DstasConformanceVectorFixture.ExpectedFor(vector.Id).SpendingType;
            Assert.Equal(expectedSpendingType, tx.Inputs[0].DstasSpendingType);
        }
    }

    [Fact]
    public void ConformanceVectors_ProduceExpectedLineageClassification()
    {
        var vectors = DstasConformanceVectorFixture.LoadAll();
        var sut = new StasLineageEvaluator();

        foreach (var vector in vectors)
        {
            var tx = Transaction.Parse(vector.TxHex, Network.Mainnet);
            var lineage = BuildLineageTransaction(tx, vector);
            var result = sut.Evaluate(lineage);

            Assert.True(result.IsStas);
            var expected = DstasConformanceVectorFixture.ExpectedFor(vector.Id);
            Assert.Equal(expected.SpendingType, result.DstasSpendingType);
            Assert.Equal(expected.EventType, result.DstasEventType);
            Assert.Equal(expected.IsRedeem, result.IsRedeem);
        }
    }

    [Fact]
    public void ConformanceVectors_PreserveRepresentativeDstasFlagsAndActions()
    {
        var vectors = DstasConformanceVectorFixture.LoadAll()
            .Where(x => x.Id is "freeze_valid" or "unfreeze_valid" or "confiscate_valid" or "swap_cancel_valid")
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(4, vectors.Count);

        foreach (var vector in vectors)
        {
            var tx = Transaction.Parse(vector.TxHex, Network.Mainnet);
            var dstasOutput = tx.Outputs.First(x => x.Type == ScriptType.DSTAS);
            var reader = LockingScriptReader.Read(new OutPoint(tx, dstasOutput.Idx).ScriptPubKey, Network.Mainnet);

            Assert.NotNull(reader.Dstas);

            switch (vector.Id)
            {
                case "freeze_valid":
                    Assert.True(reader.Dstas!.Frozen);
                    Assert.Equal("empty", reader.Dstas.ActionType);
                    Assert.True(reader.Dstas.FreezeEnabled);
                    break;
                case "unfreeze_valid":
                    Assert.False(reader.Dstas!.Frozen);
                    Assert.Equal("empty", reader.Dstas.ActionType);
                    Assert.True(reader.Dstas.FreezeEnabled);
                    break;
                case "confiscate_valid":
                    Assert.False(reader.Dstas!.Frozen);
                    Assert.Equal("empty", reader.Dstas.ActionType);
                    Assert.True(reader.Dstas.ConfiscationEnabled);
                    break;
                case "swap_cancel_valid":
                    Assert.Equal("swap", reader.Dstas!.ActionType);
                    Assert.NotNull(reader.Dstas.RequestedScriptHash);
                    break;
            }
        }
    }

    private static StasLineageTransaction BuildLineageTransaction(Transaction tx, DstasConformanceVector vector)
    {
        var prevoutsByInput = vector.Prevouts.ToDictionary(x => x.InputIndex);

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
