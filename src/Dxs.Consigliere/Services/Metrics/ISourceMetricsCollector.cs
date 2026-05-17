#nullable enable
using Dxs.Consigliere.Data.Models.Metrics;

namespace Dxs.Consigliere.Services.Metrics;

/// <summary>
/// Wave 4 S0 — wave-level entrypoint that snapshots all per-source
/// metric inputs (the W2 <c>SourceObservationRecorder</c>, the W3
/// <c>OrphanedTxRebroadcastRecorder</c>, the wave-4
/// <c>SourceVisibilityTracker</c>, and <c>BsvP2pHealth</c>) into a
/// single <see cref="SourceMetricsSnapshot"/>. The aggregator hosted
/// service in S4 invokes this on every snapshot tick.
///
/// <para>Per Core Rule §6 the snapshot is eventually-consistent across
/// the underlying counters (each <c>Interlocked.Read</c> is atomic but
/// the read-set is not a single transaction). At 30 s sampling
/// intervals the inconsistency window is well below the
/// observability resolution.</para>
/// </summary>
public interface ISourceMetricsCollector
{
    SourceMetricsSnapshot Collect();
}
