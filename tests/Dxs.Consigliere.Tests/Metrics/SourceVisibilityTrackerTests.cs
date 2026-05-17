using System;

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
