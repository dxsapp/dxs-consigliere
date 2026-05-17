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
/// CRUD + queries for <see cref="BlockHeaderDocument"/>. Backs the
/// in-memory <see cref="Dxs.Bsv.P2p.Chain.HeadersChain"/> with
/// durable storage so restarts don't lose the trailing window.
///
/// The store deliberately exposes only the operations the Wave 1
/// service needs (Save / GetByHash / GetTip / Recent / PruneBelow).
/// Reorg ancestor-walk reads happen via Recent + GetByHash; W3 may
/// extend this surface when it lands.
/// </summary>
public sealed class BlockHeaderStore(IDocumentStore documentStore) : IBlockHeaderStore
{
    public async Task SaveAsync(BlockHeaderDocument doc, CancellationToken ct = default)
    {
        using var session = documentStore.OpenAsyncSession();
        await session.StoreAsync(doc, doc.Id, ct);
        await session.SaveChangesAsync(ct);
    }

    public async Task<BlockHeaderDocument> GetByHashAsync(string hashHex, CancellationToken ct = default)
    {
        using var session = documentStore.OpenAsyncSession();
        return await session.LoadAsync<BlockHeaderDocument>(BlockHeaderDocument.BuildId(hashHex), ct);
    }

    public async Task<BlockHeaderDocument> GetTipAsync(CancellationToken ct = default)
    {
        using var session = documentStore.OpenAsyncSession();
        return await session
            .Query<BlockHeaderDocument>()
            .OrderByDescending(h => h.Height)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<BlockHeaderDocument>> RecentAsync(int count, CancellationToken ct = default)
    {
        if (count <= 0) return [];
        using var session = documentStore.OpenAsyncSession();
        return await session
            .Query<BlockHeaderDocument>()
            .OrderByDescending(h => h.Height)
            .Take(count)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Delete every header with <c>Height &lt; minHeight</c>. Honours the
    /// retention window driven by
    /// <see cref="Dxs.Bsv.P2p.Chain.HeadersChainOptions.RetainedHeaderCount"/>.
    /// </summary>
    public async Task PruneBelowAsync(long minHeight, CancellationToken ct = default)
    {
        using var session = documentStore.OpenAsyncSession();
        var stale = await session
            .Query<BlockHeaderDocument>()
            .Where(h => h.Height < minHeight)
            .ToListAsync(ct);
        foreach (var doc in stale) session.Delete(doc);
        await session.SaveChangesAsync(ct);
    }

    public async Task SetActiveTipAsync(string blockHashHex, long height, CancellationToken ct = default)
    {
        using var session = documentStore.OpenAsyncSession();
        var doc = await session.LoadAsync<BlockHeaderActiveTipDocument>(
            BlockHeaderActiveTipDocument.DocumentId, ct);
        if (doc is null)
        {
            doc = new BlockHeaderActiveTipDocument
            {
                Id = BlockHeaderActiveTipDocument.DocumentId,
                BlockHashHex = blockHashHex,
                Height = height,
            };
            await session.StoreAsync(doc, doc.Id, ct);
        }
        else
        {
            doc.BlockHashHex = blockHashHex;
            doc.Height = height;
        }
        await session.SaveChangesAsync(ct);
    }

    public async Task<BlockHeaderActiveTip?> GetActiveTipAsync(CancellationToken ct = default)
    {
        using var session = documentStore.OpenAsyncSession();
        var doc = await session.LoadAsync<BlockHeaderActiveTipDocument>(
            BlockHeaderActiveTipDocument.DocumentId, ct);
        if (doc is null) return null;
        return new BlockHeaderActiveTip(doc.BlockHashHex, doc.Height);
    }
}
