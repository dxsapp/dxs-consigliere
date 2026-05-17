#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Consigliere.BackgroundTasks.Blocks;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.WebSockets;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 S3 — the end-to-end reorg pipeline. Called by
/// <see cref="HeadersChainService"/> on every
/// <see cref="ExtendResult.Fork"/> outcome with the freshly-stored
/// fork-side tip. Drives detector → projection-query → journal-emit →
/// hub event → re-broadcast.
///
/// <para>Design pivot (W3 S2 deferral): instead of fetching full block
/// bodies over P2P to enumerate orphaned txids — BSV mainnet blocks are
/// GB-scale and the projection already has the same data indexed by
/// <c>BlockHash</c> — we query <see cref="IOrphanedTxIdReader"/> for
/// each orphaned block's tx list. Header-chain PoW is already
/// validated inside <see cref="HeadersChain.TryExtend"/> so the chain
/// promotion itself is authenticated without body fetch.</para>
///
/// <para>Ordering rule (Core Rule §9): journal-append happens BEFORE
/// hub emission. Clients reacting to <c>OnReorg</c> by re-querying
/// projections must observe the <c>Reorged</c> state — which is set
/// by the projection rebuilder consuming the journal entries the
/// pipeline just wrote.</para>
/// </summary>
public sealed class ReorgPipeline : IReorgPipeline
{
    private readonly ReorgDetector _detector;
    private readonly HeadersChain _chain;
    private readonly IOrphanedTxIdReader _txIdReader;
    private readonly BlockObservationJournalWriter _journal;
    private readonly IHubContext<WalletHub, IWalletHub> _hub;
    private readonly IOrphanedTxRebroadcaster _rebroadcaster;
    private readonly BsvP2pHealth _health;
    private readonly IBlockHeaderStore? _headerStore;
    private readonly INewBlockNotifier? _newBlockNotifier;
    private readonly IProjectionRebuilder? _projectionRebuilder;
    private readonly ILogger<ReorgPipeline> _logger;

    public ReorgPipeline(
        ReorgDetector detector,
        HeadersChain chain,
        IOrphanedTxIdReader txIdReader,
        BlockObservationJournalWriter journal,
        IHubContext<WalletHub, IWalletHub> hub,
        IOrphanedTxRebroadcaster rebroadcaster,
        BsvP2pHealth health,
        ILogger<ReorgPipeline> logger,
        IBlockHeaderStore? headerStore = null,
        INewBlockNotifier? newBlockNotifier = null,
        IProjectionRebuilder? projectionRebuilder = null)
    {
        _detector = detector;
        _chain = chain;
        _txIdReader = txIdReader;
        _journal = journal;
        _hub = hub;
        _rebroadcaster = rebroadcaster;
        _health = health;
        _headerStore = headerStore;
        _newBlockNotifier = newBlockNotifier;
        _projectionRebuilder = projectionRebuilder;
        _logger = logger;
    }

    public async Task HandleForkObservedAsync(BlockHeader forkTip, CancellationToken cancellationToken)
    {
        var plan = _detector.TryDetect(_chain, forkTip);
        if (plan is null)
        {
            _logger.LogDebug("Fork observed but detector returned no plan (fork tip not longer / heavier than active chain)");
            return;
        }

        if (plan.IsDegraded)
        {
            _logger.LogWarning(
                "Degraded reorg detected — fork point below retained header window. "
                + "Firing OnReorg(DegradedState=true). NewTip={NewTip}@{NewTipHeight}",
                plan.NewTipHash, plan.NewTipHeight);

            _health.MarkDegradedReorg(DateTimeOffset.UtcNow);
            await _hub.Clients
                .Group("block:tip")
                .OnReorg(new ReorgEventDto(
                    CommonAncestorHash: plan.CommonAncestorHash,
                    CommonAncestorHeight: plan.CommonAncestorHeight,
                    OrphanedHashes: Array.Empty<string>(),
                    NewTipHash: plan.NewTipHash,
                    NewTipHeight: plan.NewTipHeight,
                    DegradedState: true));
            return;
        }

        _logger.LogInformation(
            "Reorg detected: common ancestor {Ancestor}@{AncestorHeight}, "
            + "{OrphanCount} orphan(s), new tip {NewTip}@{NewTipHeight}",
            plan.CommonAncestorHash, plan.CommonAncestorHeight,
            plan.OrphanedHashes.Count, plan.NewTipHash, plan.NewTipHeight);

        // M1: track progress across steps. On any post-journal failure
        // we mark the health degraded so the operator alarm fires and
        // refuse to silently continue with a partially-applied reorg.
        var progressTag = "before-journal";
        // Pre-promote snapshot: the catch block uses this to roll back
        // the in-memory PromoteFork on post-promote / pre-durable
        // failure so subsequent header arrivals can replay the plan.
        BlockHeader? preReorgTip = null;
        try
        {
            // Step 1: enumerate affected txids per orphan block. This
            // runs BEFORE any journal append so we capture the txids
            // while the projection still records them under the orphaned
            // BlockHash (the rebuilder will clear BlockHash on
            // Disconnected events). Read failure here aborts cleanly:
            // no side effects yet.
            var affectedTxIds = new List<string>();
            var perOrphanTxIds = new Dictionary<string, IReadOnlyList<string>>(plan.OrphanedHashes.Count);
            foreach (var orphanHash in plan.OrphanedHashes)
            {
                var txIds = await _txIdReader.GetTxIdsByBlockHashAsync(orphanHash, cancellationToken);
                perOrphanTxIds[orphanHash] = txIds;
                affectedTxIds.AddRange(txIds);
            }

            // Step 2: append a Disconnected journal entry per orphan, in
            // disconnect order (newest-first). The rebuilder consumes
            // these and transitions matching projections to Reorged.
            // Dedupe by fingerprint makes a repeat replay idempotent.
            progressTag = "journal";
            var reasonFragment = $"fork:{plan.CommonAncestorHash}";
            foreach (var orphanHash in plan.OrphanedHashes)
            {
                var appended = await _journal.AppendDisconnectedAsync(
                    orphanHash,
                    BlockObservationSource.Reorg,
                    reason: reasonFragment,
                    cancellationToken);
                if (!appended)
                {
                    _logger.LogDebug(
                        "Disconnect journal entry for orphan {Orphan} was a duplicate (idempotent replay)",
                        orphanHash);
                }
            }

            // Step 3 — Audit W3 A2 C2 + A2-followup N1 fix: promote
            // the fork's tip to the active chain tip in-memory AND
            // persist the new active-tip pointer durably. Without the
            // durable pointer, restart would re-pick the highest-
            // height retained header — which could be a rejected
            // low-work fork (A2-followup N1). The fork-side headers
            // themselves were already persisted by
            // HeadersChainService.PersistAsync on the original Fork
            // case, but the active-tip pointer was deliberately not
            // updated then; it updates here.
            //
            // Order matters: the in-memory PromoteFork must succeed
            // (so the detector + chain locator agree on the new tip)
            // BEFORE the durable commit. If PromoteFork throws (the
            // fork tip somehow no longer in the retention window),
            // we exit before persisting anything new and the next
            // header arrival re-runs the plan from the original tip.
            // In-memory promote happens BEFORE rebuild/hub so the
            // detector doesn't try to re-fire while we're mid-pipeline.
            // The durable commit happens at the very end (below) so a
            // failure between here and the durable commit leaves
            // durable state on the OLD tip, allowing the next header
            // arrival to replay the full plan (Core Rule §M1 retry
            // semantics; A2-followup M1 concern).
            //
            // Capture the pre-promotion tip so the catch block below
            // can roll back the in-memory promote on a post-promote
            // failure — without rollback the next header would see
            // chain.Tip already at the fork tip, the detector would
            // return null on retry, and the pipeline would never
            // resume.
            preReorgTip = _chain.Tip!;
            progressTag = "promote-fork-in-memory";
            _chain.PromoteFork(forkTip);

            // Step 4 — Audit W3 A2 H1 fix: drive the projection
            // rebuilder ourselves so the OnReorg hub event below
            // signals an already-reconciled projection state. The
            // rebuilder is lazy / query-driven in normal operation;
            // for a reorg we want the strong guarantee that clients
            // re-querying the projection after seeing OnReorg observe
            // Reorged.
            progressTag = "rebuild";
            if (_projectionRebuilder is not null)
                await _projectionRebuilder.RebuildAsync(cancellationToken);

            // Step 5: fire the hub event. Per Core Rule §9 (post-fix:
            // "journal-append AND projection-rebuild BEFORE hub emit"),
            // clients reacting to OnReorg by re-querying projections
            // observe the Reorged state and the new tip.
            progressTag = "hub-emit";
            await _hub.Clients
                .Group("block:tip")
                .OnReorg(new ReorgEventDto(
                    CommonAncestorHash: plan.CommonAncestorHash,
                    CommonAncestorHeight: plan.CommonAncestorHeight,
                    OrphanedHashes: plan.OrphanedHashes.ToArray(),
                    NewTipHash: plan.NewTipHash,
                    NewTipHeight: plan.NewTipHeight,
                    DegradedState: false));

            // Step 6: emit OnNewBlock for the promoted tip so clients
            // tracking the headers chain see the chain switch as a
            // forward-progress event. (The OnReorg above describes the
            // disconnect; the OnNewBlock describes the new active tip.)
            progressTag = "new-block-notify";
            if (_newBlockNotifier is not null)
            {
                var prevHashDisplayHex = BlockHeaderHasher.ToDisplayHex(
                    BlockHeaderHasher.PrevBlock(forkTip));
                var tipDto = new BlockTipDto(
                    Hash: plan.NewTipHash,
                    Height: plan.NewTipHeight,
                    TimestampMs: ((long)BlockHeaderHasher.TimestampUnixSeconds(forkTip)) * 1000L,
                    PrevHash: prevHashDisplayHex,
                    HeaderSize: BlockHeader.Size);
                await _newBlockNotifier.NotifyAsync(tipDto, cancellationToken);
            }

            // Step 7: hand the affected tx list to the re-broadcaster.
            progressTag = "rebroadcast";
            if (affectedTxIds.Count > 0)
                await _rebroadcaster.RebroadcastAsync(perOrphanTxIds, cancellationToken);

            // Step 8 — Audit W3 A2-followup N1+M1 fix: durable commit
            // of the active-tip pointer is the LAST step. Until this
            // write succeeds, the persistent record still points at
            // the OLD active tip; a restart between in-memory promote
            // and durable commit replays the full plan correctly on
            // the next header arrival (journal entries are idempotent
            // by fingerprint, rebroadcast deduplicates per call). The
            // pointer document stores WIRE-ORDER hex so
            // HeadersChainService.StartAsync can hand it straight to
            // HeadersChain.TryGetByWireHashHex.
            progressTag = "promote-fork-durable";
            if (_headerStore is not null)
            {
                var newTipWireHex = Convert.ToHexString(BlockHeaderHasher.Hash(forkTip))
                    .ToLowerInvariant();
                await _headerStore.SetActiveTipAsync(
                    newTipWireHex, plan.NewTipHeight, cancellationToken);
            }
        }
        catch (Exception ex) when (progressTag != "before-journal")
        {
            // M1: a failure AFTER any journal append leaves the system
            // in a partially-applied state (orphan disconnects in the
            // journal, but no tip-switch / hub event / re-broadcast).
            // The journal entries are idempotent — a replay of the same
            // plan after the failure will resume correctly. We mark
            // health degraded so the operator alarm fires and surface a
            // log line tagged with the step that failed.
            _health.MarkDegradedReorg(DateTimeOffset.UtcNow);

            // A2-followup M1 fix: roll back the in-memory promote on
            // a post-in-memory-promote failure so the detector sees the
            // fork again on next header arrival and retry runs. Skip
            // this if the durable commit succeeded (failure was in a
            // later sub-step or the catch fired for an unrelated
            // reason) — once durable, the new tip is authoritative.
            if (preReorgTip is not null
                && progressTag is "rebuild" or "hub-emit" or "new-block-notify" or "rebroadcast")
            {
                try { _chain.PromoteFork(preReorgTip); }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx,
                        "Reorg pipeline rollback of in-memory PromoteFork failed at step '{Stage}'. "
                        + "Chain may be on the new tip without durable commitment — operator action "
                        + "required.", progressTag);
                }
            }

            _logger.LogError(ex,
                "Reorg pipeline failed at step '{Stage}' after journal entries were appended. "
                + "Health marked degraded; subsequent header arrivals will retry the plan via "
                + "the journal's idempotent fingerprint. NewTip={NewTip}@{NewTipHeight}",
                progressTag, plan.NewTipHash, plan.NewTipHeight);
            throw;
        }
    }
}
