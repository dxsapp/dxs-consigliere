using System.Text.Json;

using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;
using Dxs.Bsv.Tokens.Validation;

namespace Dxs.Bsv.Tests.Conformance;

public class DstasConformanceVectorsTests
{
    private static readonly IReadOnlyDictionary<string, int> ExpectedSpendingTypeById =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["transfer_regular_valid"] = 1,
            ["freeze_valid"] = 2,
            ["frozen_owner_spend_rejected"] = 1,
            ["unfreeze_valid"] = 2,
            ["confiscate_valid"] = 3,
            ["confiscate_without_authority_rejected"] = 3,
            ["confiscate_without_bit2_rejected"] = 3,
            ["redeem_by_issuer_valid"] = 1,
            ["redeem_by_non_issuer_rejected"] = 1,
            ["redeem_frozen_rejected"] = 1,
            ["redeem_confiscation_spending_type_rejected"] = 3,
            ["swap_cancel_valid"] = 4,
        };

    private static readonly IReadOnlyDictionary<string, string?> ExpectedEventTypeById =
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["transfer_regular_valid"] = null,
            ["freeze_valid"] = "freeze",
            ["frozen_owner_spend_rejected"] = null,
            ["unfreeze_valid"] = "unfreeze",
            ["confiscate_valid"] = "confiscation",
            ["confiscate_without_authority_rejected"] = "confiscation",
            ["confiscate_without_bit2_rejected"] = "confiscation",
            ["redeem_by_issuer_valid"] = null,
            ["redeem_by_non_issuer_rejected"] = null,
            ["redeem_frozen_rejected"] = null,
            ["redeem_confiscation_spending_type_rejected"] = "confiscation",
            ["swap_cancel_valid"] = "swap_cancel",
        };

    private static readonly IReadOnlyDictionary<string, bool> ExpectedRedeemById =
        new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["transfer_regular_valid"] = false,
            ["freeze_valid"] = false,
            ["frozen_owner_spend_rejected"] = false,
            ["unfreeze_valid"] = false,
            ["confiscate_valid"] = false,
            ["confiscate_without_authority_rejected"] = false,
            ["confiscate_without_bit2_rejected"] = false,
            ["redeem_by_issuer_valid"] = true,
            ["redeem_by_non_issuer_rejected"] = false,
            ["redeem_frozen_rejected"] = false,
            ["redeem_confiscation_spending_type_rejected"] = false,
            ["swap_cancel_valid"] = false,
        };

    [Fact]
    public void ConformanceVectors_AreParsableAndMeetBasicDstasAssumptions()
    {
        var vectors = LoadVectors();
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

            var expectedSpendingType = ExpectedSpendingTypeById[vector.Id];
            Assert.Equal(expectedSpendingType, tx.Inputs[0].DstasSpendingType);
        }
    }

    [Fact]
    public void ConformanceVectors_ProduceExpectedLineageClassification()
    {
        var vectors = LoadVectors();
        var sut = new StasLineageEvaluator();

        foreach (var vector in vectors)
        {
            var tx = Transaction.Parse(vector.TxHex, Network.Mainnet);
            var lineage = BuildLineageTransaction(tx, vector);
            var result = sut.Evaluate(lineage);

            Assert.True(result.IsStas);
            Assert.Equal(ExpectedSpendingTypeById[vector.Id], result.DstasSpendingType);
            Assert.Equal(ExpectedEventTypeById[vector.Id], result.DstasEventType);
            Assert.Equal(ExpectedRedeemById[vector.Id], result.IsRedeem);
        }
    }

    [Fact]
    public void ConformanceVectors_PreserveRepresentativeDstasFlagsAndActions()
    {
        var vectors = LoadVectors()
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

    private static List<ConformanceVector> LoadVectors()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "dstas-conformance-vectors.json");
        Assert.True(File.Exists(fixturePath), $"Missing fixture file: {fixturePath}");

        var json = File.ReadAllText(fixturePath);
        var vectors = JsonSerializer.Deserialize<List<ConformanceVector>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(vectors);
        return vectors!;
    }

    private static StasLineageTransaction BuildLineageTransaction(Transaction tx, ConformanceVector vector)
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

    private sealed class ConformanceVector
    {
        public string Id { get; set; } = string.Empty;
        public bool ExpectedSuccess { get; set; }
        public string TxHex { get; set; } = string.Empty;
        public List<int>? FailedInputs { get; set; }
        public List<ConformancePrevout> Prevouts { get; set; } = [];
    }

    private sealed class ConformancePrevout
    {
        public int InputIndex { get; set; }
        public string TxId { get; set; } = string.Empty;
        public int Vout { get; set; }
        public string LockingScriptHex { get; set; } = string.Empty;
        public long Satoshis { get; set; }
    }
}
