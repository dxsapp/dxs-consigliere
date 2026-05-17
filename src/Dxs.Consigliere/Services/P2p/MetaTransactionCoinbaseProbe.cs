#nullable enable
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 A2 H2 fix — default <see cref="ICoinbaseProbe"/> backed by
/// <see cref="MetaTransaction"/>. Coinbases are identified by
/// <c>Index == 0</c> (first transaction in any block per BSV consensus).
/// When the MetaTransaction document doesn't exist for a txid (e.g. a
/// projection row points at a block we observed but never fetched the
/// per-tx metadata for), this probe returns <c>false</c> — the
/// rebroadcaster's raw-lookup chain will then naturally skip it via
/// the no-raw counter.
/// </summary>
public sealed class MetaTransactionCoinbaseProbe : ICoinbaseProbe
{
    private readonly IDocumentStore _store;

    public MetaTransactionCoinbaseProbe(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<bool> IsCoinbaseAsync(string txId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(txId)) return false;
        using var session = _store.GetNoCacheNoTrackingSession();
        var doc = await session.LoadAsync<MetaTransaction>(txId, cancellationToken);
        return doc is { Index: 0 };
    }
}
