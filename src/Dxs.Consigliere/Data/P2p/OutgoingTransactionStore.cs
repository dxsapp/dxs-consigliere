using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.P2p;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Data.P2p;

/// <summary>
/// CRUD + queries for <see cref="OutgoingTransaction"/> documents.
/// </summary>
public sealed class OutgoingTransactionStore(IDocumentStore documentStore)
{
    public async Task<OutgoingTransaction> GetAsync(string txId, CancellationToken ct = default)
    {
        using var session = documentStore.OpenAsyncSession();
        return await session.LoadAsync<OutgoingTransaction>(OutgoingTransaction.BuildId(txId), ct);
    }

    public async Task SaveAsync(OutgoingTransaction tx, CancellationToken ct = default)
    {
        tx.Touch();
        using var session = documentStore.OpenAsyncSession();
        await session.StoreAsync(tx, tx.Id, ct);
        await session.SaveChangesAsync(ct);
    }

    public async Task<OutgoingTransaction> GetOrNullAsync(string txId, CancellationToken ct = default)
    {
        using var session = documentStore.OpenAsyncSession();
        return await session.LoadAsync<OutgoingTransaction>(OutgoingTransaction.BuildId(txId), ct);
    }

    /// <summary>
    /// All non-terminal documents — used by the lifecycle monitor.
    /// Falls back to a collection scan when no index is present.
    /// </summary>
    public async Task<IReadOnlyList<OutgoingTransaction>> GetNonTerminalAsync(CancellationToken ct = default)
    {
        using var session = documentStore.OpenAsyncSession();
        return await session
            .Query<OutgoingTransaction>()
            .Where(t =>
                t.State != OutgoingTxState.Confirmed &&
                t.State != OutgoingTxState.PolicyInvalid &&
                t.State != OutgoingTxState.InvalidRejected &&
                t.State != OutgoingTxState.ConflictRejected &&
                t.State != OutgoingTxState.Failed)
            .ToListAsync(ct);
    }

    /// <summary>All documents ordered newest-first (for admin-ui list).</summary>
    public async Task<IReadOnlyList<OutgoingTransaction>> ListRecentAsync(int limit = 100, CancellationToken ct = default)
    {
        using var session = documentStore.OpenAsyncSession();
        return await session
            .Query<OutgoingTransaction>()
            .OrderByDescending(t => t.CreatedAtMs)
            .Take(limit)
            .ToListAsync(ct);
    }
}
