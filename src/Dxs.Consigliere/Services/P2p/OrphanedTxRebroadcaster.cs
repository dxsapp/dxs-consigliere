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
/// <para>Coinbase exclusion is EXPLICIT via
/// <see cref="ICoinbaseProbe"/> (audit W3 A2 H2 + A2-followup N2):
/// the probe runs BEFORE the raw lookup and skips coinbase txs via
/// <see cref="OrphanedTxRebroadcastRecorder.IncrementSkippedCoinbase"/>.
/// The production default
/// <see cref="MetaTransactionCoinbaseProbe"/> requires the full BSV
/// consensus signature <c>Index == 0 AND Inputs.Count == 1 AND
/// Inputs[0].TxId is all-zero</c> so a non-coinbase tx whose
/// <c>Index</c> defaulted to 0 is not misclassified.</para>
/// </summary>
public sealed class OrphanedTxRebroadcaster : IOrphanedTxRebroadcaster
{
    private readonly IOutgoingRawLookup _outgoingLookup;
    private readonly IRawTransactionPayloadStore _payloadStore;
    private readonly ITxAnnouncer _txAnnouncer;
    private readonly ICoinbaseProbe _coinbaseProbe;
    private readonly OrphanedTxRebroadcastRecorder _recorder;
    private readonly ILogger<OrphanedTxRebroadcaster> _logger;

    public OrphanedTxRebroadcaster(
        IOutgoingRawLookup outgoingLookup,
        IRawTransactionPayloadStore payloadStore,
        ITxAnnouncer txAnnouncer,
        ICoinbaseProbe coinbaseProbe,
        OrphanedTxRebroadcastRecorder recorder,
        ILogger<OrphanedTxRebroadcaster> logger)
    {
        _outgoingLookup = outgoingLookup;
        _payloadStore = payloadStore;
        _txAnnouncer = txAnnouncer;
        _coinbaseProbe = coinbaseProbe;
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

                // Audit W3 A2 H2: explicit coinbase skip BEFORE the raw
                // lookup. Coinbases are block-bound; re-announcing them
                // would always be rejected by the receiving peer and
                // accidental hits would count against the
                // AnnounceFailed metric, hiding the real signal.
                if (await _coinbaseProbe.IsCoinbaseAsync(txId, cancellationToken))
                {
                    _recorder.IncrementSkippedCoinbase();
                    continue;
                }

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
