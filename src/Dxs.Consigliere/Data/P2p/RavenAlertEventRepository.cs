#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.P2p;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Data.P2p;

/// <summary>
/// Wave 6 S2 — production Raven-backed <see cref="IAlertEventRepository"/>.
/// Mirrors the W4 <c>RavenSnapshotPersistence</c> patterns verbatim
/// (D14-padded id ordering = numeric time ordering, single-session
/// per call).
/// </summary>
public sealed class RavenAlertEventRepository : IAlertEventRepository
{
    private readonly IDocumentStore _documentStore;

    public RavenAlertEventRepository(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }

    public async Task SaveAsync(P2pAlertEvent alertEvent, CancellationToken cancellationToken)
    {
        using var session = _documentStore.OpenAsyncSession();
        await session.StoreAsync(alertEvent, alertEvent.Id, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetAllIdsOrderedAsync(CancellationToken cancellationToken)
    {
        using var session = _documentStore.OpenAsyncSession();
        return await session
            .Query<P2pAlertEvent>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(token: cancellationToken);
    }

    public async Task DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return;
        using var session = _documentStore.OpenAsyncSession();
        foreach (var id in ids) session.Delete(id);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<P2pAlertEvent>> GetRecentAsync(
        int limit,
        long? sinceUnixMs,
        CancellationToken cancellationToken)
    {
        if (limit <= 0) return System.Array.Empty<P2pAlertEvent>();
        using var session = _documentStore.OpenAsyncSession();
        var query = session.Query<P2pAlertEvent>();
        if (sinceUnixMs is { } since)
            query = query.Where(x => x.AlertUnixMs > since);
        return await query
            .OrderByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(token: cancellationToken);
    }
}
