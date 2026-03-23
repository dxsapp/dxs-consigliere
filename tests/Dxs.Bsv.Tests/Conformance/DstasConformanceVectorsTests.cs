using System.Text.Json;

using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;

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
