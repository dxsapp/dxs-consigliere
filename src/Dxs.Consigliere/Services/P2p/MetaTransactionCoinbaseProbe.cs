#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 A2 H2 + A2-followup N2 fix — default
/// <see cref="ICoinbaseProbe"/> backed by <see cref="MetaTransaction"/>.
/// A coinbase tx has the BSV-consensus signature
/// <c>Index == 0 AND Inputs.Count == 1 AND Inputs[0].TxId is the
/// all-zero outpoint hash</c>. The probe checks all three so a
/// non-coinbase tx whose <c>Index</c> defaulted to 0 (when the
/// upstream writer didn't know the position) is not misclassified.
///
/// <para>When the MetaTransaction document doesn't exist for a txid
/// (e.g. a projection row points at a block we observed but never
/// fetched the per-tx metadata for), this probe returns <c>false</c>
/// — the rebroadcaster's raw-lookup chain will then naturally skip
/// it via the no-raw counter.</para>
/// </summary>
public sealed class MetaTransactionCoinbaseProbe : ICoinbaseProbe
{
    /// <summary>
    /// The all-zero outpoint TxId that BSV consensus mandates for
    /// the single coinbase input. Display-order hex; 64 chars of '0'.
    /// </summary>
    private const string CoinbaseOutpointTxId =
        "0000000000000000000000000000000000000000000000000000000000000000";

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
        if (doc is not { Index: 0 }) return false;
        if (doc.Inputs is not { Count: 1 }) return false;
        var first = doc.Inputs[0];
        return string.Equals(first?.TxId, CoinbaseOutpointTxId, StringComparison.OrdinalIgnoreCase);
    }
}
