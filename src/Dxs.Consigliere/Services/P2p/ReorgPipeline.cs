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
    private readonly ILogger<ReorgPipeline> _logger;

    public ReorgPipeline(
        ReorgDetector detector,
        HeadersChain chain,
        IOrphanedTxIdReader txIdReader,
        BlockObservationJournalWriter journal,
        IHubContext<WalletHub, IWalletHub> hub,
        IOrphanedTxRebroadcaster rebroadcaster,
        BsvP2pHealth health,
        ILogger<ReorgPipeline> logger)
    {
        _detector = detector;
        _chain = chain;
        _txIdReader = txIdReader;
        _journal = journal;
        _hub = hub;
        _rebroadcaster = rebroadcaster;
        _health = health;
        _logger = logger;
    }

    public async Task HandleForkObservedAsync(BlockHeader forkTip, CancellationToken cancellationToken)
    {
        var plan = _detector.TryDetect(_chain, forkTip);
        if (plan is null)
        {
            _logger.LogDebug("Fork observed but detector returned no plan (fork tip not longer than active chain)");
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

        // Step 1: enumerate affected txids per orphan block. This runs
        // BEFORE any journal append so we capture the txids while the
        // projection still records them under the orphaned BlockHash
        // (the rebuilder will clear BlockHash on Disconnected events).
        var affectedTxIds = new List<string>();
        var perOrphanTxIds = new Dictionary<string, IReadOnlyList<string>>(plan.OrphanedHashes.Count);
        foreach (var orphanHash in plan.OrphanedHashes)
        {
            var txIds = await _txIdReader.GetTxIdsByBlockHashAsync(orphanHash, cancellationToken);
            perOrphanTxIds[orphanHash] = txIds;
            affectedTxIds.AddRange(txIds);
        }

        // Step 2: append a Disconnected journal entry per orphan, in
        // disconnect order (newest-first). The rebuilder consumes these
        // and transitions matching projections to Reorged. Dedupe by
        // fingerprint makes a repeat replay idempotent.
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

        // Step 3: fire the hub event. Per Core Rule §9, this is AFTER
        // every journal append has landed.
        await _hub.Clients
            .Group("block:tip")
            .OnReorg(new ReorgEventDto(
                CommonAncestorHash: plan.CommonAncestorHash,
                CommonAncestorHeight: plan.CommonAncestorHeight,
                OrphanedHashes: plan.OrphanedHashes.ToArray(),
                NewTipHash: plan.NewTipHash,
                NewTipHeight: plan.NewTipHeight,
                DegradedState: false));

        // Step 4: hand the affected tx list to the re-broadcaster. The
        // rebroadcaster filters coinbases (i==0 in any orphan's tx list)
        // and missing-raw txs internally — we pass the union here.
        if (affectedTxIds.Count > 0)
        {
            await _rebroadcaster.RebroadcastAsync(perOrphanTxIds, cancellationToken);
        }
    }
}
