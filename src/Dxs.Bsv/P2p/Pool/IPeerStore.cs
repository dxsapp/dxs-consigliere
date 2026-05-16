#nullable enable
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Bsv.P2p.Pool;

/// <summary>
/// Persistence for <see cref="PeerRecord"/>. Gate 2 ships an in-memory
/// implementation (<see cref="InMemoryPeerStore"/>); Gate 3 will add a
/// RavenDB-backed one inside <c>Dxs.Consigliere</c>.
/// </summary>
public interface IPeerStore
{
    Task<PeerRecord?> GetAsync(IPEndPoint endpoint, CancellationToken ct);

    /// <summary>Insert or update by endpoint.</summary>
    Task UpsertAsync(PeerRecord record, CancellationToken ct);

    /// <summary>
    /// Get a set of selectable candidates: not currently in the pool,
    /// not banned, /24-diverse if possible, ordered with most-recent-success first.
    /// </summary>
    Task<IReadOnlyList<PeerRecord>> SelectCandidatesAsync(
        int max,
        IReadOnlySet<string> excludeKeys,
        IReadOnlySet<string> excludeSubnets,
        CancellationToken ct);

    /// <summary>All known records — useful for diagnostics / soak reports.</summary>
    Task<IReadOnlyList<PeerRecord>> ListAllAsync(CancellationToken ct);
}
