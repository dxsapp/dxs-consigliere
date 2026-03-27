using System.Text.Json;

using Dxs.Bsv;
using Dxs.Bsv.Extensions;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;

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

    public static IReadOnlyList<DstasProtocolPrevoutFixture> ResolvePrevouts(DstasProtocolChainFixture chain, DstasProtocolTransactionFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(fixture);

        var tx = Transaction.Parse(fixture.TxHex, Network.Mainnet);
        var resolved = fixture.Prevouts.ToDictionary(x => x.InputIndex);
        if (resolved.Count == tx.Inputs.Count)
            return resolved.OrderBy(x => x.Key).Select(x => x.Value).ToArray();

        var chainIndex = chain.Transactions.FindIndex(x => ReferenceEquals(x, fixture) || string.Equals(x.Label, fixture.Label, StringComparison.Ordinal));
        if (chainIndex < 0)
            throw new InvalidOperationException($"Fixture '{fixture.Label}' is not present in chain '{chain.Id}'.");

        var priorTransactions = chain.Transactions
            .Take(chainIndex)
            .Select(x => (Fixture: x, Transaction: Transaction.Parse(x.TxHex, Network.Mainnet)))
            .ToDictionary(x => x.Transaction.Id, x => x);

        for (var i = 0; i < tx.Inputs.Count; i++)
        {
            if (resolved.ContainsKey(i))
                continue;

            var input = tx.Inputs[i];
            if (!priorTransactions.TryGetValue(input.TxId, out var parent))
                throw new InvalidOperationException(
                    $"Missing protocol-owner prevout source for {chain.Id}/{fixture.Label} input {i}: parent tx '{input.TxId}' is not available in prior chain context.");

            if (input.Vout >= parent.Transaction.Outputs.Count)
                throw new InvalidOperationException(
                    $"Missing protocol-owner prevout source for {chain.Id}/{fixture.Label} input {i}: output {input.Vout} is outside parent tx '{parent.Fixture.Label}'.");

            var outPoint = new OutPoint(parent.Transaction, input.Vout);
            resolved[i] = new DstasProtocolPrevoutFixture
            {
                Label = $"derived:{parent.Fixture.Label}:{input.Vout}",
                InputIndex = i,
                TxId = input.TxId,
                Vout = (int)input.Vout,
                LockingScriptHex = outPoint.ScriptPubKey.ToHexString(),
                Satoshis = (long)outPoint.Satoshis
            };
        }

        return resolved
            .OrderBy(x => x.Key)
            .Select(x => x.Value)
            .ToArray();
    }
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
