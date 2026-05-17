using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Consigliere.Data.Models.Metrics;
using Dxs.Consigliere.Services.Metrics;
using Dxs.Consigliere.Services.P2p;

namespace Dxs.Consigliere.Tests.Metrics;

/// <summary>
/// Wave 4 S6 — end-to-end fixture validation. Drive synthetic
/// observations through the visibility tracker + W2/W3 recorders,
/// invoke the collector, assert every counter + bucket count matches
/// the fixture-injected input byte-for-byte (master.md done-when:
/// "fixture-injected observations match counter values exactly; lag
/// histogram bucket counts deterministic").
///
/// Aggregator-driven persistence (Raven) is excluded — covered by
/// MetricsSetupDiResolutionTests + the aggregator's own TickOnce
/// path. This fixture is in-memory.
/// </summary>
public class SourceMetricsEndToEndFixtureTests
{
    private sealed class FixtureScenario
    {
        public required SourceMetricsCollector Collector;
        public required SourceObservationRecorder Observation;
        public required OrphanedTxRebroadcastRecorder Rebroadcast;
        public required SourceVisibilityTracker Tracker;
        public required BsvP2pHealth Health;

        public static FixtureScenario Build()
        {
            var observation = new SourceObservationRecorder();
            var rebroadcast = new OrphanedTxRebroadcastRecorder();
            var tracker = new SourceVisibilityTracker();
            var health = new BsvP2pHealth();
            return new FixtureScenario
            {
                Collector = new SourceMetricsCollector(observation, rebroadcast, tracker, health),
                Observation = observation,
                Rebroadcast = rebroadcast,
                Tracker = tracker,
                Health = health,
            };
        }
    }

    [Fact]
    public void Fixture_ThreeSources_OneWatchedTx_ProducesNonZeroCountersOnEachSource()
    {
        // Program done-when literal: "admin API + SPA page show three
        // sources with non-zero counters in fixture run". Replicate
        // the canonical fixture: each of P2p / Bitails / JungleBus
        // observes the same watched tx.
        var s = FixtureScenario.Build();

        // Inv observations (any-tx counter, per-source).
        s.Observation.RecordInvObserved(TxObservationSource.P2p);
        s.Observation.RecordInvObserved(TxObservationSource.Bitails);
        s.Observation.RecordInvObserved(TxObservationSource.JungleBus);

        // Watched-tx visibility: P2p first by 30ms (bucket 1),
        // JungleBus 150ms after that (bucket 2 — total lag from P2p
        // is 180ms; index 2 covers 50-200ms).
        s.Tracker.RecordObservation("tx-watched-1", TxObservationSource.P2p,
            DateTimeOffset.FromUnixTimeMilliseconds(1_000));
        s.Tracker.RecordObservation("tx-watched-1", TxObservationSource.Bitails,
            DateTimeOffset.FromUnixTimeMilliseconds(1_030));
        s.Tracker.RecordObservation("tx-watched-1", TxObservationSource.JungleBus,
            DateTimeOffset.FromUnixTimeMilliseconds(1_180));

        var snap = s.Collector.Collect();

        // Per-source InvObserved: each source = 1.
        Assert.Equal(1, snap.ObservationCounters[TxObservationSource.P2p].InvObserved);
        Assert.Equal(1, snap.ObservationCounters[TxObservationSource.Bitails].InvObserved);
        Assert.Equal(1, snap.ObservationCounters[TxObservationSource.JungleBus].InvObserved);

        // Visibility: P2p got FirstSeen += 1; the other two got
        // lag bucket counts.
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.P2p].FirstSeen);
        Assert.Equal(0, snap.VisibilityCounters[TxObservationSource.Bitails].FirstSeen);
        Assert.Equal(0, snap.VisibilityCounters[TxObservationSource.JungleBus].FirstSeen);

        // Bitails: 30 ms after first → bucket 1 (10-50 ms).
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.Bitails].LagBuckets[1]);
        // JungleBus: 180 ms after first → bucket 2 (50-200 ms).
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.JungleBus].LagBuckets[2]);
    }

    [Fact]
    public void Fixture_ExactBucketDistribution_AcrossSixBucketRanges()
    {
        // Pin "lag histogram bucket counts deterministic": inject one
        // observation per bucket and assert the histogram counts are
        // exactly one per bucket for the responding source.
        var s = FixtureScenario.Build();
        var basis = DateTimeOffset.FromUnixTimeMilliseconds(1_000);

        // Each entry uses a unique tx so cross-tx state is independent.
        // P2p observes each first; Bitails responds with a specific lag.
        var lagsMs = new long[] { 5, 30, 100, 500, 3_000, 7_500 };
        for (var i = 0; i < lagsMs.Length; i++)
        {
            var txid = $"tx-bucket-{i}";
            s.Tracker.RecordObservation(txid, TxObservationSource.P2p, basis);
            s.Tracker.RecordObservation(txid, TxObservationSource.Bitails,
                basis.AddMilliseconds(lagsMs[i]));
        }

        var snap = s.Collector.Collect();
        var bitails = snap.VisibilityCounters[TxObservationSource.Bitails];
        // Each bucket exactly 1.
        Assert.Equal(1, bitails.LagBuckets[0]); // <10ms (5ms)
        Assert.Equal(1, bitails.LagBuckets[1]); // 10-50ms (30ms)
        Assert.Equal(1, bitails.LagBuckets[2]); // 50-200ms (100ms)
        Assert.Equal(1, bitails.LagBuckets[3]); // 200ms-1s (500ms)
        Assert.Equal(1, bitails.LagBuckets[4]); // 1s-5s (3s)
        Assert.Equal(1, bitails.LagBuckets[5]); // >5s (7.5s)

        Assert.Equal(6, snap.VisibilityCounters[TxObservationSource.P2p].FirstSeen);
    }

    [Fact]
    public void Fixture_AllRebroadcastCounters_ReflectInjectedCounts()
    {
        var s = FixtureScenario.Build();
        s.Rebroadcast.IncrementAnnounced();
        s.Rebroadcast.IncrementAnnounced();
        s.Rebroadcast.IncrementAnnounced();
        s.Rebroadcast.IncrementSkippedNoRaw();
        s.Rebroadcast.IncrementSkippedCoinbase();
        s.Rebroadcast.IncrementSkippedCoinbase();
        s.Rebroadcast.IncrementAnnounceNoReadyPeer();
        s.Rebroadcast.IncrementAnnounceFailed();

        var snap = s.Collector.Collect();
        Assert.Equal(3, snap.Rebroadcast.Announced);
        Assert.Equal(1, snap.Rebroadcast.SkippedNoRaw);
        Assert.Equal(2, snap.Rebroadcast.SkippedCoinbase);
        Assert.Equal(1, snap.Rebroadcast.AnnounceNoReadyPeer);
        Assert.Equal(1, snap.Rebroadcast.AnnounceFailed);
    }

    [Fact]
    public void Fixture_OnlySawCommit_ViaEvictionWindow()
    {
        // P2p observes a tx; no other source ever sees it; eviction
        // commits OnlySaw += 1 for P2p.
        var s = FixtureScenario.Build();
        s.Tracker.RecordObservation("tx-only", TxObservationSource.P2p,
            DateTimeOffset.FromUnixTimeMilliseconds(0));

        // Eviction window default 5 minutes; jump 6 min ahead.
        s.Tracker.EvictStaleEntries(DateTimeOffset.FromUnixTimeMilliseconds(6 * 60 * 1000L));

        var snap = s.Collector.Collect();
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.P2p].OnlySaw);
    }

    [Fact]
    public void Fixture_SnapshotIdFormat_LexicographicallySortable()
    {
        // Two snapshots taken in quick succession must produce ids
        // that sort correctly. The aggregator's retention loop
        // relies on this for ORDER BY id eviction.
        var s = FixtureScenario.Build();
        var first = s.Collector.Collect();
        System.Threading.Thread.Sleep(2);
        var second = s.Collector.Collect();

        Assert.True(string.CompareOrdinal(first.Id, second.Id) <= 0,
            $"Expected id ordering: {first.Id} <= {second.Id}");
    }
}
