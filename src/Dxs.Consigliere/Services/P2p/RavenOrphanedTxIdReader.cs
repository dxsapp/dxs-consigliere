#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 S3 — Raven-backed
/// <see cref="IOrphanedTxIdReader"/>. Queries
/// <see cref="TxLifecycleProjectionDocument"/> by <c>BlockHash</c>
/// (the same index the projection rebuilder uses on
/// <see cref="Dxs.Bsv.BitcoinMonitor.Models.BlockObservationEventType.Disconnected"/>),
/// and returns the matching txid list in deterministic order so the
/// emitter can hand it to the re-broadcaster.
/// </summary>
public sealed class RavenOrphanedTxIdReader : IOrphanedTxIdReader
{
    private readonly IDocumentStore _store;

    public RavenOrphanedTxIdReader(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<string>> GetTxIdsByBlockHashAsync(
        string blockHashDisplayHex,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(blockHashDisplayHex)) return [];

        using var session = _store.GetNoCacheNoTrackingSession();
        var matches = await session.Query<TxLifecycleProjectionDocument>()
            .Where(x => x.BlockHash == blockHashDisplayHex)
            .Select(x => x.TxId)
            .ToListAsync(token: cancellationToken);

        return matches;
    }
}
