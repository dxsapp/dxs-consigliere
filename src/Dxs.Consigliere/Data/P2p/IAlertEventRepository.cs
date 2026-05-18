#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.P2p;

namespace Dxs.Consigliere.Data.P2p;

/// <summary>
/// Wave 6 S2 — thin abstraction over the subset of Raven document
/// operations the <c>P2pAlertPoller</c> needs. Mirrors the W4
/// <c>ISnapshotPersistence</c> shape: write + ordered-id list for
/// retention + bulk delete + a bounded admin-endpoint read.
///
/// <para>The interface lets the evaluator + poller be unit-tested
/// without standing up an in-memory <c>IDocumentStore</c>;
/// production binds to <c>RavenAlertEventRepository</c>.</para>
/// </summary>
public interface IAlertEventRepository
{
    Task SaveAsync(P2pAlertEvent alertEvent, CancellationToken cancellationToken);

    /// <summary>Returns every retained alert id in lexicographic
    /// (= time) order — oldest first. Used by the poller's
    /// retention loop.</summary>
    Task<IReadOnlyList<string>> GetAllIdsOrderedAsync(CancellationToken cancellationToken);

    /// <summary>Deletes the given alert ids in one batch.</summary>
    Task DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken);

    /// <summary>Reads up to <paramref name="limit"/> most recent
    /// alerts. When <paramref name="sinceUnixMs"/> is set, only
    /// events fired strictly after that timestamp are returned (for
    /// the admin endpoint's incremental polling).</summary>
    Task<IReadOnlyList<P2pAlertEvent>> GetRecentAsync(
        int limit,
        long? sinceUnixMs,
        CancellationToken cancellationToken);
}
