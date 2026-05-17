#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data;

using Microsoft.Extensions.Logging;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 S4 — default <see cref="IOrphanedTxRebroadcaster"/>
/// implementation. Per orphan-block tx list:
/// <list type="number">
///   <item>Look up raw bytes via <see cref="OutgoingTransactionStore"/>
///         (first preference — kept hot during W2 broadcast); fall back
///         to <see cref="IRawTransactionPayloadStore"/>.</item>
///   <item>If raw bytes found: call
///         <see cref="TxRelayCoordinator.AnnounceAsync"/> — the single
///         W2 announce path.</item>
///   <item>If missing: increment
///         <see cref="OrphanedTxRebroadcastRecorder.IncrementSkippedNoRaw"/>
///         — cannot re-announce without raw bytes.</item>
///   <item>If announce throws: counter increments and the loop
///         continues; one failing tx never blocks others.</item>
/// </list>
///
/// <para>Coinbase exclusion is implicit: coinbase txs almost never live
/// in <see cref="OutgoingTransactionStore"/> (we don't broadcast them)
/// nor in <see cref="IRawTransactionPayloadStore"/> for a thin-node
/// observer (we don't fetch full block bodies — W3 S2 deferred);
/// the natural "no raw" path skips them. If a coinbase txid does happen
/// to have raw bytes recorded and gets re-announced, the receiving peer
/// rejects it (coinbases are block-bound by consensus) and
/// <see cref="OrphanedTxRebroadcastRecorder.IncrementAnnounceFailed"/>
/// fires.</para>
/// </summary>
public sealed class OrphanedTxRebroadcaster : IOrphanedTxRebroadcaster
{
    private readonly IOutgoingRawLookup _outgoingLookup;
    private readonly IRawTransactionPayloadStore _payloadStore;
    private readonly ITxAnnouncer _txAnnouncer;
    private readonly OrphanedTxRebroadcastRecorder _recorder;
    private readonly ILogger<OrphanedTxRebroadcaster> _logger;

    public OrphanedTxRebroadcaster(
        IOutgoingRawLookup outgoingLookup,
        IRawTransactionPayloadStore payloadStore,
        ITxAnnouncer txAnnouncer,
        OrphanedTxRebroadcastRecorder recorder,
        ILogger<OrphanedTxRebroadcaster> logger)
    {
        _outgoingLookup = outgoingLookup;
        _payloadStore = payloadStore;
        _txAnnouncer = txAnnouncer;
        _recorder = recorder;
        _logger = logger;
    }

    public async Task RebroadcastAsync(
        IReadOnlyDictionary<string, IReadOnlyList<string>> orphanedTxIdsPerBlock,
        CancellationToken cancellationToken)
    {
        if (orphanedTxIdsPerBlock.Count == 0) return;

        // Dedupe across orphan blocks (a tx that confirmed in two
        // orphans — possible if our projection state was inconsistent —
        // shouldn't be re-announced twice).
        var seenTxIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, txIds) in orphanedTxIdsPerBlock)
        {
            foreach (var txId in txIds)
            {
                if (string.IsNullOrEmpty(txId)) continue;
                if (!seenTxIds.Add(txId)) continue;

                var rawHex = await ResolveRawHexAsync(txId, cancellationToken);
                if (rawHex is null)
                {
                    _recorder.IncrementSkippedNoRaw();
                    continue;
                }

                try
                {
                    var announcedCount = await _txAnnouncer.AnnounceAsync(txId, rawHex, cancellationToken);
                    if (announcedCount > 0)
                    {
                        _recorder.IncrementAnnounced();
                    }
                    else
                    {
                        _recorder.IncrementAnnounceNoReadyPeer();
                        _logger.LogDebug(
                            "Re-announce attempt for orphan tx {TxId} reached no Ready peers", txId);
                    }
                }
                catch (Exception ex)
                {
                    _recorder.IncrementAnnounceFailed();
                    _logger.LogWarning(ex,
                        "Re-announce throw for orphan tx {TxId}", txId);
                }
            }
        }
    }

    private async Task<string?> ResolveRawHexAsync(string txId, CancellationToken ct)
    {
        var outgoingRaw = await _outgoingLookup.GetRawHexAsync(txId, ct);
        if (!string.IsNullOrEmpty(outgoingRaw)) return outgoingRaw;

        var envelope = await _payloadStore.LoadByTxIdAsync(txId, ct);
        if (envelope is not null && !string.IsNullOrEmpty(envelope.PayloadHex))
            return envelope.PayloadHex;

        return null;
    }
}
