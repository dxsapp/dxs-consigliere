#nullable enable
using System;
using System.Collections.Generic;

namespace Dxs.Consigliere.Data.Models.Metrics;

/// <summary>
/// Wave 4 S0 — append-only Raven document holding one point-in-time
/// aggregated snapshot of per-source observation metrics. Written by
/// <c>SourceMetricsAggregator</c> on a configurable interval (default
/// 30 s) and read by the admin endpoint <c>/api/admin/metrics/sources</c>.
///
/// <para>Document IDs follow <c>metrics/sources/{SnapshotUnixMs}</c>
/// so a numeric-string ORDER BY hits the natural time order. Retention
/// is enforced by doc-id eviction (delete oldest beyond
/// <c>SnapshotRetentionCount</c>), not by Raven expiration.</para>
///
/// <para>All fields are populated at write-time; snapshots are never
/// edited after creation (Core Rule §2 in the wave master.md).</para>
/// </summary>
public sealed class SourceMetricsSnapshot
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Unix milliseconds at snapshot capture time.</summary>
    public long SnapshotUnixMs { get; set; }

    /// <summary>
    /// Per-source counters consumed from
    /// <c>SourceObservationRecorder</c> (W2) — inv-observed +
    /// classification outcomes. Keyed by
    /// <c>TxObservationSource.{P2p,Bitails,JungleBus}</c> string
    /// constants.
    /// </summary>
    public Dictionary<string, SourceObservationCounters> ObservationCounters { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Per-source visibility tracker output (first-seen counts,
    /// lag-bucket distribution, only-saw counts).
    /// </summary>
    public Dictionary<string, SourceVisibilityCounters> VisibilityCounters { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// W3 orphan-tx rebroadcast counters (cross-source, not
    /// per-source — orphan tx re-announce is a single-source
    /// concept).
    /// </summary>
    public OrphanedTxRebroadcastCounters Rebroadcast { get; set; }
        = new();

    /// <summary>
    /// Timestamp of the last reorg that the W3 detector flagged as
    /// degraded (fork point below the retained header window). Null
    /// when never degraded since startup. Mirrors
    /// <c>BsvP2pHealth.LastDegradedReorgAt</c>.
    /// </summary>
    public DateTimeOffset? LastDegradedReorgAt { get; set; }
}

/// <summary>
/// Per-source observation counters from <c>SourceObservationRecorder</c>
/// (W2 S4). All counters are monotonically-increasing snapshots.
/// </summary>
public sealed class SourceObservationCounters
{
    public long InvObserved { get; set; }
    public long Matched { get; set; }
    public long Unmatched { get; set; }
    public long ParseError { get; set; }
    public long RateLimited { get; set; }
    public long GetDataTimeout { get; set; }
    public long OversizePayload { get; set; }
}

/// <summary>
/// Per-source visibility tracker output (W4 S1). The histogram is a
/// fixed 6-bucket distribution; bucket boundaries are constants per
/// Core Rule §4.
/// </summary>
public sealed class SourceVisibilityCounters
{
    public long FirstSeen { get; set; }
    public long OnlySaw { get; set; }

    /// <summary>
    /// Six-bucket lag histogram. Index 0 = &lt;10 ms; 1 = 10-50 ms;
    /// 2 = 50-200 ms; 3 = 200 ms - 1 s; 4 = 1 s - 5 s; 5 = &gt;5 s.
    /// See <see cref="SourceMetricsBuckets"/> for the canonical
    /// boundaries.
    /// </summary>
    public long[] LagBuckets { get; set; } = new long[SourceMetricsBuckets.Count];
}

/// <summary>
/// W3 orphan-tx rebroadcast counters from
/// <c>OrphanedTxRebroadcastRecorder</c>.
/// </summary>
public sealed class OrphanedTxRebroadcastCounters
{
    public long Announced { get; set; }
    public long SkippedNoRaw { get; set; }
    public long SkippedCoinbase { get; set; }
    public long AnnounceNoReadyPeer { get; set; }
    public long AnnounceFailed { get; set; }
}

/// <summary>
/// Wave 4 — frozen lag-histogram bucket boundaries (Core Rule §4: no
/// runtime configuration of bucket cut-points). Six buckets covering
/// 0 ms to &gt;5 s.
/// </summary>
public static class SourceMetricsBuckets
{
    public const int Count = 6;

    // A2 M1 fix: the previous `public static readonly long[]` field
    // was reassignment-safe but caller-mutable (array elements could
    // be overwritten in place, violating Core Rule §4). The array is
    // now private; the public accessor returns it as a read-only
    // view (IReadOnlyList<long>) so callers can read the boundaries
    // but cannot mutate them.
    private static readonly long[] _upperBoundsMs = [10, 50, 200, 1_000, 5_000];

    /// <summary>Upper bound (exclusive, in ms) of each bucket. Index 5
    /// (>5 s) is open-ended; the list holds 5 boundaries.</summary>
    public static IReadOnlyList<long> UpperBoundsMs => _upperBoundsMs;

    /// <summary>Returns the bucket index for a given lag in
    /// milliseconds. Negative inputs (clock skew per Core Rule §5)
    /// clamp to bucket 0.</summary>
    public static int IndexFor(long lagMs)
    {
        if (lagMs < _upperBoundsMs[0]) return 0;
        if (lagMs < _upperBoundsMs[1]) return 1;
        if (lagMs < _upperBoundsMs[2]) return 2;
        if (lagMs < _upperBoundsMs[3]) return 3;
        if (lagMs < _upperBoundsMs[4]) return 4;
        return 5;
    }

    /// <summary>Builds the standard Raven document id from a UTC
    /// timestamp.</summary>
    public static string BuildId(long snapshotUnixMs) =>
        $"metrics/sources/{snapshotUnixMs:D14}";
}
