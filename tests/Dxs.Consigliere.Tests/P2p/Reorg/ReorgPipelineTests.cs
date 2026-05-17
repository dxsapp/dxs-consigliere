using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Common.Journal;
using Dxs.Consigliere.BackgroundTasks.Blocks;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.P2p;
using Dxs.Consigliere.WebSockets;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace Dxs.Consigliere.Tests.P2p.Reorg;

/// <summary>
/// Wave 3 S3 — pins the orchestration logic of
/// <see cref="ReorgPipeline"/>. Drives a synthetic
/// <see cref="HeadersChain"/> + fork into the pipeline and asserts the
/// downstream side effects in order: orphan-txid enumeration, journal
/// append per orphan (Core Rule §9 — journal-before-hub), single
/// <c>OnReorg</c> hub event with the correct DTO, hand-off to the
/// rebroadcaster.
/// </summary>
public class ReorgPipelineTests
{
    // --- Fakes ----------------------------------------------------------

    private sealed class FakeTxIdReader : IOrphanedTxIdReader
    {
        public readonly Dictionary<string, IReadOnlyList<string>> ByBlockHash = new();
        public Task<IReadOnlyList<string>> GetTxIdsByBlockHashAsync(string blockHash, CancellationToken ct)
            => Task.FromResult(ByBlockHash.TryGetValue(blockHash, out var v) ? v : (IReadOnlyList<string>)new List<string>());
    }

    // Shared sequence counter recording the order in which the pipeline
    // touched each downstream sink. Used by OrderingPin tests to assert
    // strict step-ordering, not just counts.
    private sealed class SequenceTracker
    {
        private int _next;
        public int Next() => System.Threading.Interlocked.Increment(ref _next);
    }

    private sealed class CapturingBlockAppender : IObservationJournalAppender<ObservationJournalEntry<BlockObservation>>
    {
        public readonly ConcurrentQueue<ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>>> Requests = new();
        public readonly HashSet<string> DuplicateFingerprints = new();
        public readonly List<int> SeqOnAppend = new();

        private readonly SequenceTracker? _tracker;

        public CapturingBlockAppender() { }
        public CapturingBlockAppender(SequenceTracker tracker) { _tracker = tracker; }

        public ValueTask<ObservationJournalAppendResult> AppendAsync(
            ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>> request, CancellationToken ct = default)
        {
            Requests.Enqueue(request);
            if (_tracker is not null) SeqOnAppend.Add(_tracker.Next());
            var isDup = DuplicateFingerprints.Contains(request.Fingerprint.Value);
            return ValueTask.FromResult(new ObservationJournalAppendResult(new JournalSequence(Requests.Count), isDuplicate: isDup));
        }
    }

    private sealed class CapturingRebroadcaster : IOrphanedTxRebroadcaster
    {
        public IReadOnlyDictionary<string, IReadOnlyList<string>>? Received;
        public int CallCount;

        public Task RebroadcastAsync(IReadOnlyDictionary<string, IReadOnlyList<string>> orphanedTxIdsPerBlock, CancellationToken ct)
        {
            CallCount++;
            Received = orphanedTxIdsPerBlock;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingHubClient
    {
        public readonly ConcurrentQueue<ReorgEventDto> Reorgs = new();
        public readonly List<int> SeqOnEmit = new();
        private readonly SequenceTracker? _tracker;

        public CapturingHubClient() { }
        public CapturingHubClient(SequenceTracker tracker) { _tracker = tracker; }

        public IWalletHub Build()
        {
            var mock = new Mock<IWalletHub>();
            mock.Setup(c => c.OnReorg(It.IsAny<ReorgEventDto>()))
                .Returns<ReorgEventDto>(dto =>
                {
                    Reorgs.Enqueue(dto);
                    if (_tracker is not null) SeqOnEmit.Add(_tracker.Next());
                    return Task.CompletedTask;
                });
            return mock.Object;
        }
    }

    private static (Mock<IHubContext<WalletHub, IWalletHub>> mockCtx, CapturingHubClient client) BuildHub(SequenceTracker? tracker = null)
    {
        var client = tracker is null ? new CapturingHubClient() : new CapturingHubClient(tracker);
        var clients = new Mock<IHubClients<IWalletHub>>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(client.Build());
        var ctx = new Mock<IHubContext<WalletHub, IWalletHub>>();
        ctx.SetupGet(c => c.Clients).Returns(clients.Object);
        return (ctx, client);
    }

    // --- Scenario builder ----------------------------------------------

    /// <summary>
    /// Build a 1-deep fork scenario: active tip = active[0] (height 1),
    /// fork tip = fork[1] (height 2). Common ancestor = genesis.
    /// </summary>
    private static (HeadersChain chain, BlockHeader genesis, BlockHeader[] active, BlockHeader[] fork)
        BuildOneDeepForkScenario()
    {
        var chain = new HeadersChain(new HeadersChainOptions { RetainedHeaderCount = 10 });
        var genesis = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x01);
        chain.Seed(genesis, 0);

        var a1 = HeaderTestUtil.BuildChild(genesis, merkleFill: 0xA1);
        chain.TryExtend(a1);

        var b1 = HeaderTestUtil.BuildChild(genesis, merkleFill: 0xB1, timestamp: 1700000005);
        chain.TryExtend(b1);
        var b2 = HeaderTestUtil.BuildChild(b1, merkleFill: 0xB1, timestamp: 1700000006);
        chain.TryExtend(b2);

        return (chain, genesis, new[] { a1 }, new[] { b1, b2 });
    }

    private static ReorgPipeline BuildPipeline(
        HeadersChain chain,
        out CapturingBlockAppender appender,
        out FakeTxIdReader reader,
        out CapturingHubClient hubClient,
        out CapturingRebroadcaster rebroadcaster,
        out BsvP2pHealth health)
    {
        var detector = new ReorgDetector();
        appender = new CapturingBlockAppender();
        var journal = new BlockObservationJournalWriter(appender);
        reader = new FakeTxIdReader();
        var (hubCtx, client) = BuildHub();
        hubClient = client;
        rebroadcaster = new CapturingRebroadcaster();
        health = new BsvP2pHealth();
        return new ReorgPipeline(
            detector, chain, reader, journal, hubCtx.Object, rebroadcaster, health,
            NullLogger<ReorgPipeline>.Instance);
    }

    private static string DisplayHex(BlockHeader header)
        => BlockHeaderHasher.ToDisplayHex(BlockHeaderHasher.Hash(header));

    // --- Tests ----------------------------------------------------------

    [Fact]
    public async Task OnePlan_AppendsDisconnectedForEachOrphan()
    {
        var (chain, genesis, active, fork) = BuildOneDeepForkScenario();
        var pipeline = BuildPipeline(chain, out var appender, out var reader, out _, out _, out _);
        reader.ByBlockHash[DisplayHex(active[0])] = new[] { "tx-a1-1", "tx-a1-2" };

        await pipeline.HandleForkObservedAsync(fork[1], CancellationToken.None);

        Assert.Single(appender.Requests);
        var req = appender.Requests.TryDequeue(out var r) ? r : null;
        Assert.NotNull(req);
        Assert.Equal(BlockObservationEventType.Disconnected, req!.Observation.Observation.EventType);
        Assert.Equal(BlockObservationSource.Reorg, req.Observation.Observation.Source);
        Assert.Equal(DisplayHex(active[0]), req.Observation.Observation.BlockHash);
    }

    [Fact]
    public async Task OnePlan_FiresOnReorg_WithCorrectDto()
    {
        var (chain, genesis, active, fork) = BuildOneDeepForkScenario();
        var pipeline = BuildPipeline(chain, out _, out var reader, out var hubClient, out _, out _);
        reader.ByBlockHash[DisplayHex(active[0])] = new[] { "tx-a1-1" };

        await pipeline.HandleForkObservedAsync(fork[1], CancellationToken.None);

        Assert.Single(hubClient.Reorgs);
        var dto = hubClient.Reorgs.TryDequeue(out var d) ? d : null;
        Assert.NotNull(dto);
        Assert.False(dto!.DegradedState);
        Assert.Equal(DisplayHex(genesis), dto.CommonAncestorHash);
        Assert.Equal(0, dto.CommonAncestorHeight);
        Assert.Equal(new[] { DisplayHex(active[0]) }, dto.OrphanedHashes);
        Assert.Equal(DisplayHex(fork[1]), dto.NewTipHash);
        Assert.Equal(2, dto.NewTipHeight);
    }

    [Fact]
    public async Task NoPlan_NoSideEffects()
    {
        // Fork at equal height — first-seen rule → null plan.
        var chain = new HeadersChain(new HeadersChainOptions { RetainedHeaderCount = 10 });
        var genesis = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x01);
        chain.Seed(genesis, 0);
        var a1 = HeaderTestUtil.BuildChild(genesis, merkleFill: 0xA1);
        chain.TryExtend(a1);
        var b1 = HeaderTestUtil.BuildChild(genesis, merkleFill: 0xB1, timestamp: 1700000005);
        chain.TryExtend(b1);

        var pipeline = BuildPipeline(chain, out var appender, out _, out var hub, out var rebroadcaster, out _);
        await pipeline.HandleForkObservedAsync(b1, CancellationToken.None);

        Assert.Empty(appender.Requests);
        Assert.Empty(hub.Reorgs);
        Assert.Equal(0, rebroadcaster.CallCount);
    }

    [Fact]
    public async Task DegradedPlan_FiresSingleOnReorg_DegradedTrue_NoJournalAppend()
    {
        // Recreate the S1-tested "below retention" scenario.
        var chain = new HeadersChain(new HeadersChainOptions { RetainedHeaderCount = 3 });
        var genesis = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x01);
        chain.Seed(genesis, 0);
        var p = genesis;
        var active = new BlockHeader[5];
        for (var i = 0; i < 5; i++)
        {
            active[i] = HeaderTestUtil.BuildChild(p, merkleFill: 0xA0);
            chain.TryExtend(active[i]);
            p = active[i];
        }
        var fork = new BlockHeader[4];
        var fp = active[2];
        for (var i = 0; i < 4; i++)
        {
            fork[i] = HeaderTestUtil.BuildChild(fp, merkleFill: 0xB0, timestamp: 1700000005);
            chain.TryExtend(fork[i]);
            fp = fork[i];
        }
        var active6 = HeaderTestUtil.BuildChild(active[4], merkleFill: 0xA0);
        chain.TryExtend(active6);

        var pipeline = BuildPipeline(chain, out var appender, out _, out var hubClient, out var rebroadcaster, out var health);

        await pipeline.HandleForkObservedAsync(fork[3], CancellationToken.None);

        Assert.Empty(appender.Requests);   // no journal append when degraded
        Assert.Equal(0, rebroadcaster.CallCount); // no rebroadcast when degraded
        Assert.Single(hubClient.Reorgs);
        var dto = hubClient.Reorgs.TryDequeue(out var d) ? d : null;
        Assert.True(dto!.DegradedState);
        Assert.Empty(dto.OrphanedHashes);
        Assert.NotNull(health.LastDegradedReorgAt);
    }

    [Fact]
    public async Task Pipeline_HandsOffAffectedTxIds_ToRebroadcaster()
    {
        var (chain, genesis, active, fork) = BuildOneDeepForkScenario();
        var pipeline = BuildPipeline(chain, out _, out var reader, out _, out var rebroadcaster, out _);
        reader.ByBlockHash[DisplayHex(active[0])] = new[] { "tx-a1-1", "tx-a1-2" };

        await pipeline.HandleForkObservedAsync(fork[1], CancellationToken.None);

        Assert.Equal(1, rebroadcaster.CallCount);
        Assert.NotNull(rebroadcaster.Received);
        Assert.True(rebroadcaster.Received!.ContainsKey(DisplayHex(active[0])));
        Assert.Equal(new[] { "tx-a1-1", "tx-a1-2" }, rebroadcaster.Received[DisplayHex(active[0])]);
    }

    [Fact]
    public async Task Pipeline_NoAffectedTxIds_SkipsRebroadcastHandoff()
    {
        // Orphan block had no projection rows (e.g. nothing we tracked
        // confirmed in it). The pipeline still journals + fires
        // OnReorg, but skips the rebroadcaster hand-off.
        var (chain, genesis, active, fork) = BuildOneDeepForkScenario();
        var pipeline = BuildPipeline(chain, out var appender, out var reader, out var hub, out var rebroadcaster, out _);
        reader.ByBlockHash[DisplayHex(active[0])] = new List<string>(); // empty

        await pipeline.HandleForkObservedAsync(fork[1], CancellationToken.None);

        Assert.Single(appender.Requests);
        Assert.Single(hub.Reorgs);
        Assert.Equal(0, rebroadcaster.CallCount);
    }

    [Fact]
    public async Task RepeatPlan_SameForkTip_IsIdempotent_StableState()
    {
        // Audit W3 A2 M3 fix + post-C2 (PromoteFork) semantics: after a
        // reorg, the active tip has been promoted to the fork tip. A
        // second invocation with the SAME fork tip sees the detector
        // return null (no plan: fork tip height no longer exceeds the
        // active tip height) → no journal append, no hub event. The
        // chain state is stable. This is a stronger guarantee than the
        // pre-C2 design which would have re-emitted with IsDuplicate=true.
        var (chain, genesis, active, fork) = BuildOneDeepForkScenario();
        var pipeline = BuildPipeline(chain, out var appender, out var reader, out var hub, out _, out _);
        reader.ByBlockHash[DisplayHex(active[0])] = new[] { "tx-a1-1" };

        await pipeline.HandleForkObservedAsync(fork[1], CancellationToken.None);
        Assert.Equal(DisplayHex(fork[1]), DisplayHex(chain.Tip!));

        var appendsAfterFirst = appender.Requests.Count;
        var reorgsAfterFirst = hub.Reorgs.Count;

        // Second invocation with the same fork tip.
        await pipeline.HandleForkObservedAsync(fork[1], CancellationToken.None);

        // No new side effects: chain tip unchanged, no new journal
        // entry, no new hub event. The system is stable under replay.
        Assert.Equal(DisplayHex(fork[1]), DisplayHex(chain.Tip!));
        Assert.Equal(appendsAfterFirst, appender.Requests.Count);
        Assert.Equal(reorgsAfterFirst, hub.Reorgs.Count);
    }

    private sealed class ThrowingHeaderStore : IBlockHeaderStore
    {
        public bool SetActiveTipAsyncCalled;
        public Task SaveAsync(BlockHeaderDocument doc, CancellationToken ct = default) => Task.CompletedTask;
        public Task<BlockHeaderDocument> GetByHashAsync(string hashHex, CancellationToken ct = default)
            => Task.FromResult<BlockHeaderDocument>(null!);
        public Task<BlockHeaderDocument> GetTipAsync(CancellationToken ct = default)
            => Task.FromResult<BlockHeaderDocument>(null!);
        public Task<IReadOnlyList<BlockHeaderDocument>> RecentAsync(int count, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BlockHeaderDocument>>(new List<BlockHeaderDocument>());
        public Task PruneBelowAsync(long minHeight, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetActiveTipAsync(string blockHashHex, long height, CancellationToken ct = default)
        {
            SetActiveTipAsyncCalled = true;
            throw new System.InvalidOperationException("simulated durable-commit failure");
        }
        public Task<BlockHeaderActiveTip?> GetActiveTipAsync(CancellationToken ct = default)
            => Task.FromResult<BlockHeaderActiveTip?>(null);
    }

    [Fact]
    public async Task DurableCommit_Throws_RollsBackInMemoryPromoteFork()
    {
        // Audit W3 A2-followup-3 N5b fix: pin the rollback path.
        // SetActiveTipAsync at Step 8 ("promote-fork-durable") throws.
        // The catch block must:
        //   (a) mark health degraded;
        //   (b) roll back the in-memory PromoteFork via the captured
        //       preReorgTip;
        //   (c) re-throw the exception.
        // After the throw, chain.Tip is back at the pre-reorg active tip
        // and a subsequent header arrival can replay the plan (the
        // detector will see the still-stored fork as a Fork again).
        var (chain, genesis, active, fork) = BuildOneDeepForkScenario();
        var preReorgTipHex = DisplayHex(chain.Tip!);

        var detector = new ReorgDetector();
        var appender = new CapturingBlockAppender();
        var journal = new BlockObservationJournalWriter(appender);
        var reader = new FakeTxIdReader();
        reader.ByBlockHash[DisplayHex(active[0])] = new[] { "tx-a1-1" };
        var (hubCtx, hubClient) = BuildHub();
        var rebroadcaster = new CapturingRebroadcaster();
        var health = new BsvP2pHealth();
        var throwingStore = new ThrowingHeaderStore();
        var pipeline = new ReorgPipeline(
            detector, chain, reader, journal, hubCtx.Object, rebroadcaster, health,
            NullLogger<ReorgPipeline>.Instance,
            headerStore: throwingStore);

        await Assert.ThrowsAsync<System.InvalidOperationException>(
            () => pipeline.HandleForkObservedAsync(fork[1], CancellationToken.None));

        // The durable commit was attempted (step reached "promote-fork-durable").
        Assert.True(throwingStore.SetActiveTipAsyncCalled);

        // Health flagged degraded.
        Assert.NotNull(health.LastDegradedReorgAt);

        // Rollback: in-memory tip is back at the pre-reorg active tip.
        Assert.Equal(preReorgTipHex, DisplayHex(chain.Tip!));

        // Side effects that ran before the throw stay (journal +
        // hub + rebroadcast) because they're idempotent by
        // construction — the catch block doesn't undo them; replay
        // safety comes from the dedupe fingerprint + dedupe in the
        // rebroadcaster. We just assert here that the THROW landed
        // after the hub + rebroadcast steps, confirming the
        // "promote-fork-durable" position.
        Assert.Single(appender.Requests);
        Assert.Single(hubClient.Reorgs);
        Assert.Equal(1, rebroadcaster.CallCount);
    }

    [Fact]
    public async Task FiveDeepFork_AppendsFiveDisconnectedEntries_FiresSingleOnReorg()
    {
        // S6 depth scenario: active chain 4 long, fork chain 5 long off
        // the genesis (common ancestor). Pipeline must journal-append
        // each orphan in disconnect order (newest first) and fire one
        // OnReorg with all four orphan hashes in the DTO.
        var chain = new HeadersChain(new HeadersChainOptions { RetainedHeaderCount = 20 });
        var genesis = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x01);
        chain.Seed(genesis, 0);

        var active = new BlockHeader[4];
        var p = genesis;
        for (var i = 0; i < 4; i++)
        {
            active[i] = HeaderTestUtil.BuildChild(p, merkleFill: 0xA5);
            chain.TryExtend(active[i]);
            p = active[i];
        }
        var fork = new BlockHeader[5];
        var fp = genesis;
        for (var i = 0; i < 5; i++)
        {
            fork[i] = HeaderTestUtil.BuildChild(fp, merkleFill: 0xB5, timestamp: 1700000005);
            chain.TryExtend(fork[i]);
            fp = fork[i];
        }

        var pipeline = BuildPipeline(chain, out var appender, out var reader, out var hub, out var rebroadcaster, out _);
        for (var i = 0; i < 4; i++)
            reader.ByBlockHash[DisplayHex(active[i])] = new[] { $"tx-active-{i}-1", $"tx-active-{i}-2" };

        await pipeline.HandleForkObservedAsync(fork[4], CancellationToken.None);

        // 4 journal entries (one per orphan), 1 hub event, 1 rebroadcast call.
        Assert.Equal(4, appender.Requests.Count);
        Assert.Single(hub.Reorgs);
        var dto = hub.Reorgs.TryDequeue(out var d) ? d : null;
        Assert.Equal(4, dto!.OrphanedHashes.Length);
        // Disconnect order: newest active first.
        Assert.Equal(DisplayHex(active[3]), dto.OrphanedHashes[0]);
        Assert.Equal(DisplayHex(active[0]), dto.OrphanedHashes[3]);
        Assert.Equal(DisplayHex(fork[4]), dto.NewTipHash);
        Assert.Equal(5, dto.NewTipHeight);

        Assert.Equal(1, rebroadcaster.CallCount);
        Assert.Equal(4, rebroadcaster.Received!.Count);
    }

    [Fact]
    public async Task OrderingPin_JournalAppendBeforeHubEmit_StrictSequence()
    {
        // Audit W3 A2 M3 fix: pin journal-before-hub via shared
        // monotonic sequence counter, not just counts. Every call into
        // the journal-append and hub-emit fakes increments the counter
        // and records its sequence number; the assertion verifies every
        // journal-append sequence is < every hub-emit sequence (Core
        // Rule §9: clients reacting to OnReorg by re-querying
        // projections must see Reorged state — which requires the
        // journal entry to have landed first).
        var tracker = new SequenceTracker();
        var (chain, genesis, active, fork) = BuildOneDeepForkScenario();

        // Build with seq-tracker-aware fakes.
        var detector = new ReorgDetector();
        var appender = new CapturingBlockAppender(tracker);
        var journal = new BlockObservationJournalWriter(appender);
        var reader = new FakeTxIdReader();
        var (hubCtx, hubClient) = BuildHub(tracker);
        var rebroadcaster = new CapturingRebroadcaster();
        var health = new BsvP2pHealth();
        var pipeline = new ReorgPipeline(
            detector, chain, reader, journal, hubCtx.Object, rebroadcaster, health,
            NullLogger<ReorgPipeline>.Instance);

        reader.ByBlockHash[DisplayHex(active[0])] = new[] { "tx-a1-1" };
        await pipeline.HandleForkObservedAsync(fork[1], CancellationToken.None);

        Assert.NotEmpty(appender.SeqOnAppend);
        Assert.NotEmpty(hubClient.SeqOnEmit);
        var maxJournalSeq = appender.SeqOnAppend.Max();
        var minHubSeq = hubClient.SeqOnEmit.Min();
        Assert.True(maxJournalSeq < minHubSeq,
            $"every journal append must precede every hub emit, "
            + $"got max-journal-seq={maxJournalSeq} >= min-hub-seq={minHubSeq}");
    }
}
