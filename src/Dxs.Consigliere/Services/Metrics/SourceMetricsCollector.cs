#nullable enable
using System;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Consigliere.Data.Models.Metrics;
using Dxs.Consigliere.Services.P2p;

namespace Dxs.Consigliere.Services.Metrics;

/// <summary>
/// Wave 4 S2 — production <see cref="ISourceMetricsCollector"/>.
/// Reads the in-memory state of every metric input and packages it
/// into a single <see cref="SourceMetricsSnapshot"/>. Pure (no I/O);
/// the aggregator hosted service in S4 calls this on every tick.
///
/// <para>Per-source observation counters (<c>InvObserved</c>) are
/// keyed by the canonical <see cref="TxObservationSource"/>
/// constants. The other W2 outcome counters
/// (<c>Matched/Unmatched/ParseError/RateLimited/GetDataTimeout/
/// OversizePayload</c>) are P2p-specific — only the P2p runner ever
/// increments them — so they all live on the
/// <see cref="TxObservationSource.P2p"/> row of the per-source dict.
/// Bitails / JungleBus rows in the dict carry only their
/// <c>InvObserved</c> values.</para>
/// </summary>
public sealed class SourceMetricsCollector : ISourceMetricsCollector
{
    /// <summary>Canonical sources observed by the W4 metrics. Frozen
    /// per master.md Core Rule §7.</summary>
    private static readonly string[] KnownSources =
    {
        TxObservationSource.P2p,
        TxObservationSource.Bitails,
        TxObservationSource.JungleBus,
    };

    private readonly SourceObservationRecorder _observationRecorder;
    private readonly OrphanedTxRebroadcastRecorder _rebroadcastRecorder;
    private readonly SourceVisibilityTracker _visibilityTracker;
    private readonly BsvP2pHealth _health;

    public SourceMetricsCollector(
        SourceObservationRecorder observationRecorder,
        OrphanedTxRebroadcastRecorder rebroadcastRecorder,
        SourceVisibilityTracker visibilityTracker,
        BsvP2pHealth health)
    {
        _observationRecorder = observationRecorder;
        _rebroadcastRecorder = rebroadcastRecorder;
        _visibilityTracker = visibilityTracker;
        _health = health;
    }

    public SourceMetricsSnapshot Collect()
    {
        var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var snapshot = new SourceMetricsSnapshot
        {
            SnapshotUnixMs = nowUnixMs,
            Id = SourceMetricsBuckets.BuildId(nowUnixMs),
            LastDegradedReorgAt = _health.LastDegradedReorgAt,
            Rebroadcast = new OrphanedTxRebroadcastCounters
            {
                Announced = _rebroadcastRecorder.GetAnnouncedCount(),
                SkippedNoRaw = _rebroadcastRecorder.GetSkippedNoRawCount(),
                SkippedCoinbase = _rebroadcastRecorder.GetSkippedCoinbaseCount(),
                AnnounceNoReadyPeer = _rebroadcastRecorder.GetAnnounceNoReadyPeerCount(),
                AnnounceFailed = _rebroadcastRecorder.GetAnnounceFailedCount(),
            },
        };

        // Per-source InvObserved counts.
        foreach (var source in KnownSources)
        {
            snapshot.ObservationCounters[source] = new SourceObservationCounters
            {
                InvObserved = _observationRecorder.GetInvObservedCount(source),
            };
        }

        // P2p-specific outcomes (only the W2 P2p runner produces these).
        var p2p = snapshot.ObservationCounters[TxObservationSource.P2p];
        p2p.Matched = _observationRecorder.GetMatchedCount();
        p2p.Unmatched = _observationRecorder.GetUnmatchedCount();
        p2p.ParseError = _observationRecorder.GetParseErrorCount();
        p2p.RateLimited = _observationRecorder.GetRateLimitedCount();
        p2p.GetDataTimeout = _observationRecorder.GetGetDataTimeoutCount();
        p2p.OversizePayload = _observationRecorder.GetOversizePayloadCount();

        // Visibility tracker per-source first-seen + lag + only-saw.
        _visibilityTracker.SnapshotInto(snapshot);

        return snapshot;
    }
}
