// Wave 5 S2: legacy multi-provider HTTP broadcast methods + helpers
// + Polly retry policy + multi-attempt Raven document writer all
// removed. The ctor dropped IBitailsRestApiClient /
// IWhatsOnChainRestApiClient / IAdminProviderConfigService /
// IExternalChainProviderCatalog / IUtxoCache / INetworkProvider /
// IOptions<AppConfig> dependencies. IBitcoindService stays as the
// fee-rate source (SatoshisPerByte forwarder). IDocumentStore was
// dropped by A2 L1 (the legacy Broadcast doc writer was the only
// consumer).
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.Audit;
using Dxs.Consigliere.Services.P2p;

namespace Dxs.Consigliere.Services.Impl;

public class BroadcastService(
    IBitcoindService bitcoindService,
    IAuditLogger auditLogger,
    ILogger<BroadcastService> logger
) : IBroadcastService
{
    public Task<decimal> SatoshisPerByte() => bitcoindService.SatoshisPerByte();

    // Wave 5 S2: legacy Broadcast(string)/Broadcast(Transaction) +
    // multi-provider routing + Polly retry + BroadcastTo{Node,Bitails,
    // WhatsOnChain}Async + ResolveBroadcastTargetsAsync helpers were
    // deleted outright (no [Obsolete] shimming per vnext policy). The
    // overloads were also removed from IBroadcastService; the shape
    // test in tests/Broadcast/IBroadcastServiceShapeTests.cs asserts
    // their absence so a re-introduction fails the build.

    // ── Wave 2 Gate 3: P2P broadcast (renamed BroadcastAsync in W5 S0) ─

    // Property-injected via BroadcastServiceP2pExtension.Configure().
    // Kept as property injection to avoid coupling W5 to the BsvP2pSetup
    // wiring order; production DI sets them post-AddBsvP2pZoneServices.
    //
    // A2 M1 fix: OutgoingStore + RelayCoordinator are interface-typed
    // (IOutgoingTransactionRepository, ITxAnnouncer) so unit tests can
    // mock them without standing up Raven / the full P2P pool.
    // PolicyValidator stays concrete because tests can construct it
    // with a simple IDocumentStore mock (the validator's only
    // hard-state coupling).
    internal IBroadcastPolicyValidator PolicyValidator { get; set; }
    internal IOutgoingTransactionRepository OutgoingStore { get; set; }
    internal ITxAnnouncer Announcer { get; set; }

    // Feeds a successfully-validated self-broadcast into the SAME ingest
    // pipeline an observed mempool tx takes (match → SaveTransaction →
    // journal), so a broadcast that pays/spends a watched address updates
    // its projection immediately instead of waiting for a peer to relay
    // the tx back (which the node dedupes as its own). Property-injected
    // alongside the other P2P bits in BsvP2pSetup.
    internal ObservedTxIngestor Ingestor { get; set; }

    public async Task<BroadcastReceipt> BroadcastAsync(string rawHex, BroadcastSource source, string clientConnectionId = null, CancellationToken ct = default)
    {
        if (PolicyValidator is null || OutgoingStore is null)
        {
            // P2P subsystem not enabled — degrade gracefully.
            return new BroadcastReceipt(null, OutgoingTxState.Failed, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                "P2P broadcast subsystem not enabled (Consigliere:Broadcast:P2p:Enabled = false)");
        }

        var validation = await PolicyValidator.ValidateAsync(rawHex, ct);

        // wave-A3 S3: audit BEFORE the actual broadcast. Context is
        // the minimum non-leaky payload (length + honest source);
        // raw hex bytes are excluded per the slice's what-not-to-do
        // constraint.
        //
        // S3-followup-2: `source` is the caller's true provenance,
        // not a hardcoded "admin-ui". Fail-stop applies ONLY to
        // operator-initiated broadcasts — the destructive-admin
        // action S3 was built to gate. Wallet / api / system
        // broadcasts are audited best-effort (RecordAsync logs its
        // own failure) so a degraded Raven audit store can't halt
        // the wallet path or the background rebroadcast monitor —
        // the latter would otherwise break the Wave-5
        // "always-eventually-broadcast" contract.
        if (validation.IsValid)
        {
            var auditOk = await auditLogger.RecordAsync(
                AuditActionNames.BroadcastTx,
                validation.TxId,
                new { rawHexLength = rawHex?.Length ?? 0, source = source.ToWire() },
                cancellationToken: ct);
            if (!auditOk && source == BroadcastSource.Operator)
            {
                return new BroadcastReceipt(
                    null,
                    OutgoingTxState.Failed,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    "audit_write_failed");
            }
        }

        if (!validation.IsValid)
        {
            if (PolicyValidator.IsDuplicateResult(validation))
            {
                // Idempotent: return existing receipt.
                var existing = await OutgoingStore.GetOrNullAsync(PolicyValidator.ExtractTxIdFromDuplicate(validation), ct);
                if (existing is not null)
                    return new BroadcastReceipt(existing.TxId, existing.State, existing.CreatedAtMs);
            }
            return new BroadcastReceipt(null, OutgoingTxState.PolicyInvalid, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), validation.FailReason);
        }

        var tx = new OutgoingTransaction
        {
            Id = OutgoingTransaction.BuildId(validation.TxId),
            TxId = validation.TxId,
            RawHex = rawHex,
            ParsedSizeBytes = validation.SizeBytes,
            State = OutgoingTxState.Validated,
            ClientConnectionId = clientConnectionId,
        };
        await OutgoingStore.SaveAsync(tx, ct);

        // Feed our own validated tx into the shared ingest pipeline so a
        // broadcast that pays/spends a watched address credits/debits its
        // projection right away. Best-effort: the tx is already validated +
        // queued for dispatch, so an ingest hiccup must not fail the
        // broadcast (the projection also self-heals if a peer relays the tx
        // back later). Ingestor is null only when the P2P subsystem is off,
        // which the guard above already excludes.
        if (Ingestor is not null)
        {
            try
            {
                var rawBytes = Convert.FromHexString(rawHex);
                var outcome = await Ingestor.IngestAsync(validation.TxId, rawBytes, TxObservationSource.Self);
                if (outcome is TxIngestOutcome.SaveFailed)
                    logger.LogWarning("Self-ingest SaveTransaction failed for {TxId}", validation.TxId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Self-ingest failed for {TxId}", validation.TxId);
            }
        }

        // Fire-and-forget dispatch — lifecycle worker picks it up if this fails.
        _ = Task.Run(async () =>
        {
            try
            {
                tx.State = OutgoingTxState.Dispatching;
                await OutgoingStore.SaveAsync(tx, default);

                var served = await Announcer.AnnounceAsync(tx.TxId, tx.RawHex, default);
                if (served == 0)
                {
                    // Wave 5 A2 H1 fix: a 0-peer announce is NOT a
                    // terminal failure. The W5 Core Rule §2 mandates
                    // that no-ready-peer broadcasts STAY queued in
                    // Dispatching; OutgoingTransactionMonitor +
                    // OrphanedTxRebroadcaster (W3) retry when peers
                    // reconnect. Setting Failed here would have moved
                    // the doc out of the monitor's non-terminal set
                    // (per OutgoingTxStates.IsTerminal) and the tx
                    // would never be retried — breaking the
                    // "no HTTP fallback, but always-eventually-broadcast"
                    // contract.
                    //
                    // We keep State at Dispatching, record LastError
                    // for operator visibility, and let the lifecycle
                    // monitor pick it up on the next tick.
                    tx.LastError = "No peers available at dispatch time; awaiting peer reconnect";
                    await OutgoingStore.SaveAsync(tx, default);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background dispatch failed for {TxId}", tx.TxId);
            }
        }, ct);

        return new BroadcastReceipt(tx.TxId, OutgoingTxState.Validated, tx.CreatedAtMs);
    }
}
