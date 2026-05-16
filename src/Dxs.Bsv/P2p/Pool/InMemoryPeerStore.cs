#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Bsv.P2p.Pool;

/// <summary>
/// Process-local <see cref="IPeerStore"/>. Lost on restart. Good enough for
/// Gate 2 soak and unit tests; production deployments will replace this
/// with a RavenDB-backed implementation in Gate 3.
/// </summary>
public sealed class InMemoryPeerStore : IPeerStore
{
    private readonly ConcurrentDictionary<string, PeerRecord> _records = new();

    public Task<PeerRecord?> GetAsync(IPEndPoint endpoint, CancellationToken ct)
    {
        _records.TryGetValue(KeyOf(endpoint), out var record);
        return Task.FromResult(record);
    }

    public Task UpsertAsync(PeerRecord record, CancellationToken ct)
    {
        _records[record.Key] = record;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PeerRecord>> SelectCandidatesAsync(
        int max,
        IReadOnlySet<string> excludeKeys,
        IReadOnlySet<string> excludeSubnets,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var candidates = _records.Values
            .Where(r => r.NegativeUntilUtc is null || r.NegativeUntilUtc <= now)
            .Where(r => !excludeKeys.Contains(r.Key))
            .Where(r => !excludeSubnets.Contains(r.Subnet24))
            .OrderByDescending(r => r.LastConnectedUtc ?? DateTime.MinValue)
            .ThenByDescending(r => r.SuccessCount)
            .Take(max)
            .ToList();
        return Task.FromResult<IReadOnlyList<PeerRecord>>(candidates);
    }

    public Task<IReadOnlyList<PeerRecord>> ListAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PeerRecord>>(_records.Values.ToList());

    private static string KeyOf(IPEndPoint endpoint) => $"{endpoint.Address}:{endpoint.Port}";
}
