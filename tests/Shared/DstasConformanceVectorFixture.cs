using System.Text.Json;

namespace Dxs.Tests.Shared;

public static class DstasConformanceVectorFixture
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly IReadOnlyDictionary<string, DstasExpectedClassification> ExpectedById =
        new Dictionary<string, DstasExpectedClassification>(StringComparer.Ordinal)
        {
            ["transfer_regular_valid"] = new(1, null, false),
            ["freeze_valid"] = new(2, "freeze", false),
            ["frozen_owner_spend_rejected"] = new(1, null, false),
            ["unfreeze_valid"] = new(2, "unfreeze", false),
            ["confiscate_valid"] = new(3, "confiscation", false),
            ["confiscate_without_authority_rejected"] = new(3, "confiscation", false),
            ["confiscate_without_bit2_rejected"] = new(3, "confiscation", false),
            ["redeem_by_issuer_valid"] = new(1, null, true),
            ["redeem_by_non_issuer_rejected"] = new(1, null, false),
            ["redeem_frozen_rejected"] = new(1, null, false),
            ["redeem_confiscation_spending_type_rejected"] = new(3, "confiscation", false),
            ["swap_cancel_valid"] = new(4, "swap_cancel", false),
        };

    public static IReadOnlyList<DstasConformanceVector> LoadAll()
    {
        var fixturePath = RepoPathResolver.ResolveFromRepoRoot(
            "tests",
            "Dxs.Consigliere.Tests",
            "fixtures",
            "dstas-conformance-vectors.json");

        if (!File.Exists(fixturePath))
            throw new InvalidOperationException($"Missing DSTAS conformance fixture: {fixturePath}");

        var json = File.ReadAllText(fixturePath);
        var vectors = JsonSerializer.Deserialize<List<DstasConformanceVector>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize DSTAS conformance vectors.");

        return vectors;
    }

    public static DstasConformanceVector Load(string id)
        => LoadAll().FirstOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException($"Missing DSTAS conformance vector: {id}");

    public static DstasExpectedClassification ExpectedFor(string id)
        => ExpectedById.TryGetValue(id, out var expected)
            ? expected
            : throw new InvalidOperationException($"Missing DSTAS conformance expectation: {id}");

    public static IReadOnlyDictionary<string, DstasExpectedClassification> LoadExpectations() => ExpectedById;
}

public sealed record DstasExpectedClassification(int SpendingType, string? EventType, bool IsRedeem);

public sealed class DstasConformanceVector
{
    public string Id { get; set; } = string.Empty;
    public bool ExpectedSuccess { get; set; }
    public string TxHex { get; set; } = string.Empty;
    public List<int>? FailedInputs { get; set; }
    public List<DstasConformancePrevout> Prevouts { get; set; } = [];
}

public sealed class DstasConformancePrevout
{
    public int InputIndex { get; set; }
    public string TxId { get; set; } = string.Empty;
    public int Vout { get; set; }
    public string LockingScriptHex { get; set; } = string.Empty;
    public long Satoshis { get; set; }
}
