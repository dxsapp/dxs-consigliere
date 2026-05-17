using System;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Consigliere.Data.Models.Metrics;
using Dxs.Consigliere.Services.Metrics;
using Dxs.Consigliere.Services.P2p;

namespace Dxs.Consigliere.Tests.Metrics;

/// <summary>
/// Wave 4 S2 — pins the collector's aggregation contract:
/// per-source InvObserved counts; P2p-specific outcome counts on the
/// P2p row only; rebroadcast counters; visibility tracker output;
/// LastDegradedReorgAt mirrored from BsvP2pHealth.
/// </summary>
public class SourceMetricsCollectorTests
{
    private static (SourceMetricsCollector collector,
                    SourceObservationRecorder observation,
                    OrphanedTxRebroadcastRecorder rebroadcast,
                    SourceVisibilityTracker tracker,
                    BsvP2pHealth health) Build()
    {
        var observation = new SourceObservationRecorder();
        var rebroadcast = new OrphanedTxRebroadcastRecorder();
        var tracker = new SourceVisibilityTracker();
        var health = new BsvP2pHealth();
        var collector = new SourceMetricsCollector(observation, rebroadcast, tracker, health);
        return (collector, observation, rebroadcast, tracker, health);
    }

    [Fact]
    public void Collect_EmptyState_ZeroCounters_AllKnownSourcesPresent()
    {
        var (collector, _, _, _, _) = Build();

        var snap = collector.Collect();
        Assert.True(snap.SnapshotUnixMs > 0);
        Assert.StartsWith("metrics/sources/", snap.Id);
        Assert.Equal(3, snap.ObservationCounters.Count);
        Assert.Contains(TxObservationSource.P2p, snap.ObservationCounters.Keys);
        Assert.Contains(TxObservationSource.Bitails, snap.ObservationCounters.Keys);
        Assert.Contains(TxObservationSource.JungleBus, snap.ObservationCounters.Keys);
        foreach (var (_, c) in snap.ObservationCounters)
        {
            Assert.Equal(0, c.InvObserved);
            Assert.Equal(0, c.Matched);
            Assert.Equal(0, c.Unmatched);
        }
        Assert.Null(snap.LastDegradedReorgAt);
    }

    [Fact]
    public void Collect_ReflectsObservationRecorderState()
    {
        var (collector, observation, _, _, _) = Build();
        observation.RecordInvObserved(TxObservationSource.P2p);
        observation.RecordInvObserved(TxObservationSource.P2p);
        observation.RecordInvObserved(TxObservationSource.Bitails);
        observation.RecordMatched();
        observation.RecordUnmatched();
        observation.RecordRateLimited();

        var snap = collector.Collect();
        Assert.Equal(2, snap.ObservationCounters[TxObservationSource.P2p].InvObserved);
        Assert.Equal(1, snap.ObservationCounters[TxObservationSource.Bitails].InvObserved);
        Assert.Equal(0, snap.ObservationCounters[TxObservationSource.JungleBus].InvObserved);

        // P2p-specific outcomes only on the P2p row.
        Assert.Equal(1, snap.ObservationCounters[TxObservationSource.P2p].Matched);
        Assert.Equal(1, snap.ObservationCounters[TxObservationSource.P2p].Unmatched);
        Assert.Equal(1, snap.ObservationCounters[TxObservationSource.P2p].RateLimited);
        // Bitails / JungleBus rows have zero outcomes (those counters
        // are P2p-specific by W2 design).
        Assert.Equal(0, snap.ObservationCounters[TxObservationSource.Bitails].Matched);
        Assert.Equal(0, snap.ObservationCounters[TxObservationSource.Bitails].Unmatched);
    }

    [Fact]
    public void Collect_ReflectsRebroadcastRecorderState()
    {
        var (collector, _, rebroadcast, _, _) = Build();
        rebroadcast.IncrementAnnounced();
        rebroadcast.IncrementAnnounced();
        rebroadcast.IncrementSkippedCoinbase();
        rebroadcast.IncrementSkippedNoRaw();

        var snap = collector.Collect();
        Assert.Equal(2, snap.Rebroadcast.Announced);
        Assert.Equal(1, snap.Rebroadcast.SkippedCoinbase);
        Assert.Equal(1, snap.Rebroadcast.SkippedNoRaw);
        Assert.Equal(0, snap.Rebroadcast.AnnounceFailed);
    }

    [Fact]
    public void Collect_MirrorsBsvP2pHealthLastDegradedReorgAt()
    {
        var (collector, _, _, _, health) = Build();
        var when = new DateTimeOffset(2026, 5, 18, 9, 0, 0, TimeSpan.Zero);
        health.MarkDegradedReorg(when);

        var snap = collector.Collect();
        Assert.Equal(when, snap.LastDegradedReorgAt);
    }

    [Fact]
    public void Collect_IncludesVisibilityTrackerData()
    {
        var (collector, _, _, tracker, _) = Build();
        tracker.RecordObservation("tx1", TxObservationSource.P2p,
            DateTimeOffset.FromUnixTimeMilliseconds(1000));
        tracker.RecordObservation("tx1", TxObservationSource.Bitails,
            DateTimeOffset.FromUnixTimeMilliseconds(1075));

        var snap = collector.Collect();
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.P2p].FirstSeen);
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.Bitails].LagBuckets[2]);
    }

    [Fact]
    public void Collect_MultipleInvocations_ReflectIncrements()
    {
        var (collector, observation, _, _, _) = Build();
        observation.RecordMatched();
        var snap1 = collector.Collect();
        Assert.Equal(1, snap1.ObservationCounters[TxObservationSource.P2p].Matched);

        observation.RecordMatched();
        observation.RecordMatched();
        var snap2 = collector.Collect();
        Assert.Equal(3, snap2.ObservationCounters[TxObservationSource.P2p].Matched);

        // Snapshots are independent instances (no shared state).
        Assert.Equal(1, snap1.ObservationCounters[TxObservationSource.P2p].Matched);
    }
}
