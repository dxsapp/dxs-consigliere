#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.Metrics;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Services.Metrics;

/// <summary>
/// Wave 4 A2 M3 fix — production Raven-backed
/// <see cref="ISnapshotPersistence"/>. Mirrors the aggregator's
/// pre-refactor session calls verbatim so a future audit can
/// diff the two and confirm semantic equivalence.
/// </summary>
public sealed class RavenSnapshotPersistence : ISnapshotPersistence
{
    private readonly IDocumentStore _documentStore;

    public RavenSnapshotPersistence(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }

    public async Task StoreAsync(SourceMetricsSnapshot snapshot, CancellationToken cancellationToken)
    {
        using var session = _documentStore.OpenAsyncSession();
        await session.StoreAsync(snapshot, snapshot.Id, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetAllIdsOrderedAsync(CancellationToken cancellationToken)
    {
        using var session = _documentStore.OpenAsyncSession();
        return await session
            .Query<SourceMetricsSnapshot>()
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
}
