namespace Dxs.Consigliere.Configs;

/// <summary>
/// Wave 4 S4 — runtime knobs for the source-metrics aggregator.
/// Configured via the <c>Consigliere:Metrics:Sources</c> section.
/// All values are bounded to safe defaults (Core Rule §4: bucket
/// boundaries themselves are constants in
/// <c>SourceMetricsBuckets</c> and not configurable).
/// </summary>
public sealed class SourceMetricsConfig
{
    /// <summary>Default 30 000 ms — once per 30 s.</summary>
    public int SnapshotIntervalMs { get; set; } = 30_000;

    /// <summary>Default 720 snapshots = 6 hours @ 30 s intervals.</summary>
    public int SnapshotRetentionCount { get; set; } = 720;

    /// <summary>Default 5 minutes — passed to
    /// <see cref="Services.Metrics.SourceVisibilityTrackerOptions.EvictionWindowMs"/>.</summary>
    public long EvictionWindowMs { get; set; } = 5 * 60 * 1000L;

    /// <summary>Default true. Set false to short-circuit the
    /// aggregator's startup loop (the collector + tracker continue
    /// to function in-memory; only the Raven persistence + admin
    /// history are disabled).</summary>
    public bool Enabled { get; set; } = true;
}
