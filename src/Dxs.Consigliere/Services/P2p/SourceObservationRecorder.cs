#nullable enable
using System.Collections.Concurrent;
using System.Threading;

using Dxs.Bsv.BitcoinMonitor.Models;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 2 S4 — Consigliere-orchestration counter surface for the
/// mempool observer. Records every "we saw something" event before
/// dedupe so Wave 4's metrics dashboard can compare per-source
/// behaviour later.
///
/// <para>
/// Three buckets of counters:
/// <list type="bullet">
/// <item>
///   <c>InvObservedCount(source)</c> — every inv frame's tx item
///   counts here, before dedupe. Used by W4 to compare first-seen
///   rates across sources.
/// </item>
/// <item>
///   <c>Matched / Unmatched / ParseError / RateLimited</c> —
///   classification of every payload the mempool watcher actually
///   processes after the dedupe + fetch round-trip.
/// </item>
/// <item>
///   <c>GetDataTimeoutCount</c> / <c>OversizePayloadCount</c> —
///   per the audit W2 M4-followup three-way failure model.
///   Distinguishes a busted peer (silent timeout) from a real
///   oversize tx (peer disconnects with ProtocolViolation).
/// </item>
/// </list>
/// </para>
/// </summary>
public sealed class SourceObservationRecorder
{
    private readonly ConcurrentDictionary<string, long> _invObservedBySource =
        new(System.StringComparer.Ordinal);

    private long _matchedCount;
    private long _unmatchedCount;
    private long _parseErrorCount;
    private long _rateLimitedCount;
    private long _getDataTimeoutCount;
    private long _oversizePayloadCount;

    /// <summary>One inv-tx item observed from <paramref name="source"/>.</summary>
    public void RecordInvObserved(string source)
    {
        if (string.IsNullOrEmpty(source)) source = TxObservationSource.P2p;
        _invObservedBySource.AddOrUpdate(source, 1, (_, v) => v + 1);
    }

    /// <summary>The payload arrived and matched the watchlist; appended to the journal.</summary>
    public void RecordMatched() => Interlocked.Increment(ref _matchedCount);

    /// <summary>The payload arrived and did not match — counted, not persisted.</summary>
    public void RecordUnmatched() => Interlocked.Increment(ref _unmatchedCount);

    /// <summary>The payload failed to parse.</summary>
    public void RecordParseError() => Interlocked.Increment(ref _parseErrorCount);

    /// <summary>We deferred the getdata because the per-second budget was full.</summary>
    public void RecordRateLimited() => Interlocked.Increment(ref _rateLimitedCount);

    /// <summary>getdata issued, no tx frame returned within GetDataTimeoutMs;
    /// the session is still alive (peer silently dropped the request).</summary>
    public void RecordGetDataTimeout() => Interlocked.Increment(ref _getDataTimeoutCount);

    /// <summary>getdata issued, the session ended with ProtocolViolation
    /// while the peer's negotiated max-payload was high enough that
    /// our local cap is the plausible cause — see audit W2 M4
    /// followup classification rule.</summary>
    public void RecordOversizePayload() => Interlocked.Increment(ref _oversizePayloadCount);

    public long GetMatchedCount() => Interlocked.Read(ref _matchedCount);
    public long GetUnmatchedCount() => Interlocked.Read(ref _unmatchedCount);
    public long GetParseErrorCount() => Interlocked.Read(ref _parseErrorCount);
    public long GetRateLimitedCount() => Interlocked.Read(ref _rateLimitedCount);
    public long GetGetDataTimeoutCount() => Interlocked.Read(ref _getDataTimeoutCount);
    public long GetOversizePayloadCount() => Interlocked.Read(ref _oversizePayloadCount);

    public long GetInvObservedCount(string source)
        => _invObservedBySource.TryGetValue(source, out var v) ? v : 0;
}
