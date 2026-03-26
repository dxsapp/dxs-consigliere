using System.Text.Json;

namespace Dxs.Tests.Shared;

public static class DstasProtocolOwnerFixture
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<DstasProtocolChainFixture> LoadAll()
    {
        var fixturePath = DstasProtocolTruthOracle.ResolveValidatedPath("dstas_protocol_owner_fixtures");
        var json = File.ReadAllText(fixturePath);
        var payload = JsonSerializer.Deserialize<DstasProtocolOwnerFixturePayload>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize DSTAS protocol-owner fixture.");

        return payload.Chains;
    }

    public static DstasProtocolChainFixture LoadChain(string id)
        => LoadAll().FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Missing DSTAS protocol-owner chain fixture: {id}");

    public static DstasProtocolTransactionFixture LoadTransaction(string chainId, string label)
        => LoadChain(chainId).Transactions.FirstOrDefault(x => string.Equals(x.Label, label, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Missing DSTAS protocol-owner transaction fixture: {chainId}/{label}");
}

public sealed class DstasProtocolOwnerFixturePayload
{
    public string ExportedAt { get; set; } = string.Empty;
    public List<DstasProtocolChainFixture> Chains { get; set; } = [];
}

public sealed class DstasProtocolChainFixture
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<DstasProtocolTransactionFixture> Transactions { get; set; } = [];
}

public sealed class DstasProtocolTransactionFixture
{
    public string Label { get; set; } = string.Empty;
    public string TxHex { get; set; } = string.Empty;
    public List<DstasProtocolPrevoutFixture> Prevouts { get; set; } = [];
    public int? ExpectedSpendingType { get; set; }
    public string? ExpectedEventType { get; set; }
    public bool ExpectedIsRedeem { get; set; }
}

public sealed class DstasProtocolPrevoutFixture
{
    public string Label { get; set; } = string.Empty;
    public int InputIndex { get; set; }
    public string TxId { get; set; } = string.Empty;
    public int Vout { get; set; }
    public string LockingScriptHex { get; set; } = string.Empty;
    public long Satoshis { get; set; }
}
