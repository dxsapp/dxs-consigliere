using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Common.BackgroundTasks;
using Dxs.Consigliere.Configs;
using CommonBackgroundTasksConfig = Dxs.Common.BackgroundTasks.BackgroundTasksConfig;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.P2p;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.BackgroundTasks.P2p;

/// <summary>
/// Runs every 15 seconds. Scans non-terminal <see cref="OutgoingTransaction"/>
/// documents and advances their state machine based on peer signals, observer
/// events, and retry timeouts.
/// </summary>
public sealed class OutgoingTransactionMonitor(
    OutgoingTransactionStore store,
    TxRelayCoordinator relay,
    CommonBackgroundTasksConfig config,
    IOptions<BsvP2pConfig> p2pOptions,
    ILogger<OutgoingTransactionMonitor> logger
) : PeriodicTask(config, logger)
{
    private readonly BsvP2pConfig _p2pCfg = p2pOptions.Value;

    public override string Name => nameof(OutgoingTransactionMonitor);
    protected override TimeSpan Period => TimeSpan.FromSeconds(15);
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromSeconds(30);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!_p2pCfg.Enabled) return;

        var docs = await store.GetNonTerminalAsync(cancellationToken);
        if (docs.Count == 0) return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var retryBackoff = new long[] { 60_000, 300_000, 1_800_000, 7_200_000, 28_800_000 };

        foreach (var doc in docs)
        {
            try
            {
                await ProcessOneAsync(doc, now, retryBackoff, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Monitor error on {TxId}", doc.TxId);
            }
        }
    }

    private async Task ProcessOneAsync(OutgoingTransaction doc, long nowMs, long[] retryBackoff, CancellationToken ct)
    {
        // Re-dispatch if stuck in Dispatching or Validated for > 60s
        if (doc.State is OutgoingTxState.Validated or OutgoingTxState.Dispatching)
        {
            if (nowMs - doc.UpdatedAtMs > 60_000)
            {
                logger.LogInformation("Re-dispatching stuck {TxId} (state={State})", doc.TxId, doc.State);
                doc.State = OutgoingTxState.Dispatching;
                await store.SaveAsync(doc, ct);
                _ = relay.AnnounceAsync(doc.TxId, doc.RawHex, ct);
            }
            return;
        }

        // PeerAcked → re-announce to new peers if stuck without mempool sighting for > 60s
        if (doc.State == OutgoingTxState.PeerAcked && nowMs - doc.UpdatedAtMs > 60_000)
        {
            doc.RetryCount++;
            if (doc.RetryCount > 5)
            {
                doc.State = OutgoingTxState.Failed;
                doc.TerminalReason = "Max retries exhausted; never reached mempool";
                relay.Evict(doc.TxId);
            }
            else
            {
                var backoff = retryBackoff[Math.Min(doc.RetryCount - 1, retryBackoff.Length - 1)];
                if (doc.NextRetryAtMs is null || nowMs >= doc.NextRetryAtMs)
                {
                    logger.LogInformation("Re-broadcasting {TxId} (attempt {N})", doc.TxId, doc.RetryCount);
                    doc.NextRetryAtMs = nowMs + backoff;
                    _ = relay.AnnounceAsync(doc.TxId, doc.RawHex, ct);
                }
            }
            await store.SaveAsync(doc, ct);
        }

        // ObserverUnknown / EvictedOrDropped: attempt re-broadcast once
        if (doc.State is OutgoingTxState.EvictedOrDropped or OutgoingTxState.ObserverUnknown)
        {
            if (doc.RetryCount == 0)
            {
                doc.RetryCount++;
                logger.LogWarning("Re-broadcasting evicted/unknown {TxId}", doc.TxId);
                doc.State = OutgoingTxState.Dispatching;
                await store.SaveAsync(doc, ct);
                _ = relay.AnnounceAsync(doc.TxId, doc.RawHex, ct);
            }
            else
            {
                doc.State = OutgoingTxState.Failed;
                doc.TerminalReason = $"Re-broadcast after eviction did not succeed";
                relay.Evict(doc.TxId);
                await store.SaveAsync(doc, ct);
            }
        }
    }

    /// <summary>
    /// Called by observer when the txid appears in mempool.
    /// Drives PeerAcked/PeerRelayed → MempoolSeen.
    /// </summary>
    public async Task OnMempoolSightingAsync(string txId, CancellationToken ct = default)
    {
        var doc = await store.GetOrNullAsync(txId, ct);
        if (doc is null) return;
        if (doc.State is OutgoingTxState.MempoolSeen or OutgoingTxState.Mined or OutgoingTxState.Confirmed) return;

        doc.State = OutgoingTxState.MempoolSeen;
        doc.MempoolSeenAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await store.SaveAsync(doc, ct);
        logger.LogInformation("{TxId} → MempoolSeen", txId);
    }

    /// <summary>Called by observer when the txid is included in a confirmed block.</summary>
    public async Task OnMinedAsync(string txId, string blockHash, CancellationToken ct = default)
    {
        var doc = await store.GetOrNullAsync(txId, ct);
        if (doc is null) return;
        if (doc.State is OutgoingTxState.Confirmed) return;

        doc.State = OutgoingTxState.Mined;
        doc.MinedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        doc.BlockHash = blockHash;
        doc.ConfirmationCount = 1;
        await store.SaveAsync(doc, ct);
        logger.LogInformation("{TxId} → Mined in {Block}", txId, blockHash);

        // Immediately check confirmation requirement (default 1).
        await CheckConfirmationsAsync(doc, ct);
    }

    private async Task CheckConfirmationsAsync(OutgoingTransaction doc, CancellationToken ct)
    {
        if (doc.ConfirmationCount >= 1 && doc.State == OutgoingTxState.Mined)
        {
            doc.State = OutgoingTxState.Confirmed;
            relay.Evict(doc.TxId);
            await store.SaveAsync(doc, ct);
            logger.LogInformation("{TxId} → Confirmed", doc.TxId);
        }
    }
}
