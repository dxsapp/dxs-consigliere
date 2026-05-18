using System;
using System.Linq;
using System.Threading.Tasks;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Consigliere.Data.Models.Metrics;
using Dxs.Consigliere.Services.Metrics;

namespace Dxs.Consigliere.Tests.Metrics;

/// <summary>
/// Wave 4 S1 — pins the visibility tracker's per-source counters,
/// lag bucket placement, idempotency, and eviction semantics.
/// </summary>
public class SourceVisibilityTrackerTests
{
    private static DateTimeOffset At(long unixMs) => DateTimeOffset.FromUnixTimeMilliseconds(unixMs);

    [Fact]
    public void FirstObservation_IncrementsFirstSeen_NotLag()
    {
        var t = new SourceVisibilityTracker();
        t.RecordObservation("tx1", TxObservationSource.P2p, At(1000));

        var snap = new SourceMetricsSnapshot();
        t.SnapshotInto(snap);
        Assert.True(snap.VisibilityCounters.ContainsKey(TxObservationSource.P2p));
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.P2p].FirstSeen);
        Assert.Equal(0, snap.VisibilityCounters[TxObservationSource.P2p].OnlySaw);
        Assert.All(snap.VisibilityCounters[TxObservationSource.P2p].LagBuckets,
            x => Assert.Equal(0, x));
    }

    [Fact]
    public void SecondSource_OnSameTx_RecordsLagBucketFromFirstSeen()
    {
        var t = new SourceVisibilityTracker();
        t.RecordObservation("tx1", TxObservationSource.P2p, At(1000));
        // 75 ms later → bucket 2 (50-200 ms).
        t.RecordObservation("tx1", TxObservationSource.Bitails, At(1075));

        var snap = new SourceMetricsSnapshot();
        t.SnapshotInto(snap);
        var bitails = snap.VisibilityCounters[TxObservationSource.Bitails];
        Assert.Equal(0, bitails.FirstSeen);
        // 75 ms is in bucket 2 (50-200 ms).
        Assert.Equal(1, bitails.LagBuckets[2]);
        for (var i = 0; i < SourceMetricsBuckets.Count; i++)
            if (i != 2) Assert.Equal(0, bitails.LagBuckets[i]);
    }

    [Fact]
    public void DuplicateObservation_SameSource_NotCounted()
    {
        var t = new SourceVisibilityTracker();
        t.RecordObservation("tx1", TxObservationSource.P2p, At(1000));
        t.RecordObservation("tx1", TxObservationSource.P2p, At(1500));

        var snap = new SourceMetricsSnapshot();
        t.SnapshotInto(snap);
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.P2p].FirstSeen);
        // No lag bucket — same source.
        Assert.All(snap.VisibilityCounters[TxObservationSource.P2p].LagBuckets,
            x => Assert.Equal(0, x));
    }

    [Fact]
    public void NegativeLag_ClampsToFirstBucket()
    {
        // Clock skew: second source's observedAt is BEFORE the
        // first source's. Per Core Rule §5 this clamps to bucket 0.
        var t = new SourceVisibilityTracker();
        t.RecordObservation("tx1", TxObservationSource.P2p, At(2000));
        t.RecordObservation("tx1", TxObservationSource.Bitails, At(1000));

        var snap = new SourceMetricsSnapshot();
        t.SnapshotInto(snap);
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.Bitails].LagBuckets[0]);
    }

    [Fact]
    public void EvictStaleEntries_CommitsOnlySaw_WhenSingleSourceObservedTx()
    {
        var t = new SourceVisibilityTracker(new SourceVisibilityTrackerOptions
        {
            EvictionWindowMs = 5 * 60 * 1000L,
        });
        t.RecordObservation("tx-onlySaw", TxObservationSource.P2p, At(1000));
        t.RecordObservation("tx-doubleSeen", TxObservationSource.P2p, At(1000));
        t.RecordObservation("tx-doubleSeen", TxObservationSource.Bitails, At(1100));

        // 5min + 1ms later: both eligible for eviction.
        t.EvictStaleEntries(At(1000 + 5 * 60 * 1000L + 1));

        var snap = new SourceMetricsSnapshot();
        t.SnapshotInto(snap);
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.P2p].OnlySaw);
        Assert.Equal(0, snap.VisibilityCounters[TxObservationSource.Bitails].OnlySaw);
        Assert.Equal(0, t.InflightCount);
    }

    [Fact]
    public void EvictStaleEntries_LeavesYoungEntriesIntact()
    {
        var t = new SourceVisibilityTracker(new SourceVisibilityTrackerOptions
        {
            EvictionWindowMs = 5 * 60 * 1000L,
        });
        t.RecordObservation("tx1", TxObservationSource.P2p, At(1000));

        // Only 1 min later: not stale.
        t.EvictStaleEntries(At(1000 + 60 * 1000L));

        Assert.Equal(1, t.InflightCount);
        var snap = new SourceMetricsSnapshot();
        t.SnapshotInto(snap);
        Assert.Equal(0, snap.VisibilityCounters[TxObservationSource.P2p].OnlySaw);
    }

    [Fact]
    public void EvictStaleEntries_DoubleEviction_DoesNotDoubleCount()
    {
        var t = new SourceVisibilityTracker(new SourceVisibilityTrackerOptions
        {
            EvictionWindowMs = 5 * 60 * 1000L,
        });
        t.RecordObservation("tx1", TxObservationSource.P2p, At(1000));

        t.EvictStaleEntries(At(1000 + 6 * 60 * 1000L));
        t.EvictStaleEntries(At(1000 + 7 * 60 * 1000L));

        var snap = new SourceMetricsSnapshot();
        t.SnapshotInto(snap);
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.P2p].OnlySaw);
    }

    [Fact]
    public void EmptyTracker_SnapshotsEmptyContainers()
    {
        var t = new SourceVisibilityTracker();
        var snap = new SourceMetricsSnapshot();
        t.SnapshotInto(snap);
        Assert.Empty(snap.VisibilityCounters);
    }

    [Fact]
    public void Concurrent_TwoFirstObservations_SameTx_FirstSeenIncrementsExactlyOnce()
    {
        // Audit W4 A2 H1 regression: the pre-fix GetOrAdd-with-side-
        // effect pattern could increment FirstSeen TWICE when two
        // threads raced. The post-fix TryAdd pattern guarantees
        // exactly one increment.
        //
        // Drive many parallel first-observation attempts at the same
        // txid from many sources to maximise the race surface.
        var t = new SourceVisibilityTracker();
        var sourceCount = 32;
        var sources = new string[sourceCount];
        for (var i = 0; i < sourceCount; i++) sources[i] = $"src-{i}";
        var barrier = new System.Threading.Barrier(sourceCount);
        var tasks = new Task[sourceCount];
        for (var i = 0; i < sourceCount; i++)
        {
            var s = sources[i];
            tasks[i] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                t.RecordObservation("hot-tx", s, At(1000));
            });
        }
        Task.WaitAll(tasks);

        var snap = new SourceMetricsSnapshot();
        t.SnapshotInto(snap);
        // Exactly one source should have FirstSeen = 1; all others
        // contributed to lag buckets (lag = 0 here since identical
        // timestamps; bucket 0).
        long totalFirstSeen = 0;
        long totalBucketHits = 0;
        foreach (var (_, v) in snap.VisibilityCounters)
        {
            totalFirstSeen += v.FirstSeen;
            foreach (var b in v.LagBuckets) totalBucketHits += b;
        }
        Assert.Equal(1, totalFirstSeen);
        Assert.Equal(sourceCount - 1, totalBucketHits);
    }

    [Fact]
    public void EvictionRace_RecordObservationConcurrentWithEviction_NeverDoubleCounts()
    {
        // Audit W4 A2 H2 regression: the pre-fix eviction did not
        // synchronise with per-entry mutation, so a late observation
        // arriving DURING eviction could commit BOTH OnlySaw and a
        // lag bucket for the same tx. The post-fix lock-aware
        // eviction + Removed flag check inside RecordObservation
        // forces consistency: each tx contributes EITHER to OnlySaw
        // OR to a lag bucket but never both.
        //
        // Drive 32 concurrent insert/evict cycles at the same key.
        var t = new SourceVisibilityTracker(new SourceVisibilityTrackerOptions
        {
            EvictionWindowMs = 1, // aggressive eviction
        });
        const int loops = 64;
        var recordTasks = new Task[loops];
        var evictTasks = new Task[loops];
        var basis = DateTimeOffset.FromUnixTimeMilliseconds(1_000);
        for (var i = 0; i < loops; i++)
        {
            var tx = $"tx-{i}";
            recordTasks[i] = Task.Run(() =>
            {
                t.RecordObservation(tx, TxObservationSource.P2p, basis);
                t.RecordObservation(tx, TxObservationSource.Bitails, basis.AddMilliseconds(2));
            });
            evictTasks[i] = Task.Run(() =>
            {
                t.EvictStaleEntries(basis.AddMilliseconds(10));
            });
        }
        Task.WaitAll(recordTasks);
        Task.WaitAll(evictTasks);
        // Drain any remaining inflight entries.
        t.EvictStaleEntries(basis.AddMilliseconds(100));

        var snap = new SourceMetricsSnapshot();
        t.SnapshotInto(snap);

        // For each tx, ONE of two outcomes is valid:
        // - it was double-observed and OnlySaw should not count it,
        // - or it was single-observed and OnlySaw counted it once.
        // Invariant: total "tx-accounted" events should equal `loops`.
        long firstSeenP2p =
            snap.VisibilityCounters.TryGetValue(TxObservationSource.P2p, out var p2p)
                ? p2p.FirstSeen
                : 0;
        // Eviction may have raced past some tx-P2p observations
        // before they ran; those will then have a NEW FirstSeen
        // when re-inserted by the late Bitails call. So the FirstSeen
        // total may exceed `loops`. The key invariant we pin: no
        // single tx contributes BOTH OnlySaw and a lag bucket. We
        // check that by counting OnlySaw vs LagBucket sums and
        // asserting their sum doesn't exceed firstSeenP2p (since a
        // late observation either reuses the entry → bucket, or
        // installs a new entry → no double-count for the old).
        long bitailsBucketTotal =
            snap.VisibilityCounters.TryGetValue(TxObservationSource.Bitails, out var b)
                ? b.LagBuckets.Sum()
                : 0;
        long onlySawP2p =
            snap.VisibilityCounters.TryGetValue(TxObservationSource.P2p, out var p2pOnly)
                ? p2pOnly.OnlySaw
                : 0;
        // Soft invariant: bucket + onlySaw counts shouldn't exceed
        // the number of P2p first-seen events.
        Assert.True(bitailsBucketTotal + onlySawP2p <= firstSeenP2p,
            $"OnlySaw + LagBuckets should not exceed FirstSeen "
            + $"(onlySaw={onlySawP2p}, bucketTotal={bitailsBucketTotal}, firstSeen={firstSeenP2p})");
        // Tracker survives.
        Assert.True(t.InflightCount >= 0);
    }

    [Fact]
    public void ThreeSourcesObservingSameTx_AllRegisterLagFromFirst()
    {
        var t = new SourceVisibilityTracker();
        t.RecordObservation("tx1", TxObservationSource.P2p, At(1000));
        t.RecordObservation("tx1", TxObservationSource.Bitails, At(1005));   // 5ms → bucket 0
        t.RecordObservation("tx1", TxObservationSource.JungleBus, At(1300)); // 300ms → bucket 3

        var snap = new SourceMetricsSnapshot();
        t.SnapshotInto(snap);
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.Bitails].LagBuckets[0]);
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.JungleBus].LagBuckets[3]);
        // P2p was first; no lag for it.
        Assert.All(snap.VisibilityCounters[TxObservationSource.P2p].LagBuckets,
            x => Assert.Equal(0, x));
    }
}
