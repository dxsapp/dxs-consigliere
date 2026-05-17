#nullable enable
using System.Threading;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 3 S4 — singleton counter sink for orphan-tx-rebroadcast
/// telemetry. Mirrors the W2 <see cref="SourceObservationRecorder"/>
/// shape (Interlocked.Increment + GetXCount readers). W4 source-metrics
/// will surface these via the admin API; W3 just publishes them.
/// </summary>
public sealed class OrphanedTxRebroadcastRecorder
{
    private long _announced;
    private long _skippedNoRaw;
    private long _skippedCoinbase;
    private long _announceNoReadyPeer;
    private long _announceFailed;

    public void IncrementAnnounced() => Interlocked.Increment(ref _announced);
    public void IncrementSkippedNoRaw() => Interlocked.Increment(ref _skippedNoRaw);
    public void IncrementSkippedCoinbase() => Interlocked.Increment(ref _skippedCoinbase);
    public void IncrementAnnounceNoReadyPeer() => Interlocked.Increment(ref _announceNoReadyPeer);
    public void IncrementAnnounceFailed() => Interlocked.Increment(ref _announceFailed);

    public long GetAnnouncedCount() => Interlocked.Read(ref _announced);
    public long GetSkippedNoRawCount() => Interlocked.Read(ref _skippedNoRaw);
    public long GetSkippedCoinbaseCount() => Interlocked.Read(ref _skippedCoinbase);
    public long GetAnnounceNoReadyPeerCount() => Interlocked.Read(ref _announceNoReadyPeer);
    public long GetAnnounceFailedCount() => Interlocked.Read(ref _announceFailed);
}
