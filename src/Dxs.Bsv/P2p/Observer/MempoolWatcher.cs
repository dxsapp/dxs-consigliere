#nullable enable
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Dxs.Bsv.P2p.Observer;

/// <summary>
/// Wave 2 S4 — pure mempool dedupe + rate-limit coordinator.
/// Decision-making only; does NOT do I/O. The runner (S5) calls
/// <see cref="ShouldFetch"/> on every inbound <c>inv(MSG_TX)</c>;
/// the runner is responsible for actually issuing <c>getdata</c>
/// and routing the resulting <c>tx</c> payload back into the rest
/// of the Wave 2 pipeline (parse → match → journal).
///
/// <para>
/// Dedupe model: a bounded
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by lowercase
/// hex txid, value = first-seen unix-ms. Entries older than
/// <see cref="MempoolWatcherOptions.DedupeTtlSeconds"/> are evicted
/// opportunistically (no background thread). The cache size is
/// capped at <see cref="MempoolWatcherOptions.DedupeMaxEntries"/>;
/// overflow evicts the oldest entries to keep memory bounded even
/// during a hot mempool.
/// </para>
/// <para>
/// Rate-limit model: simple sliding 1-second window of
/// "getdata-issued" timestamps; when the window holds
/// <see cref="MempoolWatcherOptions.MaxGetDataPerSec"/> entries the
/// fetch is deferred (counted as RateLimited; not dropped silently —
/// the runner can retry on the next inv).
/// </para>
/// </summary>
public sealed class MempoolWatcher
{
    private readonly MempoolWatcherOptions _options;

    // txid → first-seen unix-ms.
    private readonly ConcurrentDictionary<string, long> _seenTxids =
        new(StringComparer.OrdinalIgnoreCase);

    // Recent getdata-issued timestamps (unix-ms). Trimmed on each query
    // to entries within the last 1000 ms.
    private readonly ConcurrentQueue<long> _getDataTimestamps = new();
    private long _getDataCountInWindow;

    public MempoolWatcher(MempoolWatcherOptions options)
    {
        _options = options ?? new MempoolWatcherOptions();
    }

    /// <summary>
    /// Result of feeding a fresh inv item to the watcher.
    /// </summary>
    public enum FetchDecision
    {
        /// <summary>Caller should issue getdata. The txid is now in the dedupe cache.</summary>
        Fetch,
        /// <summary>Already seen within the dedupe TTL; do not re-fetch.</summary>
        Duplicate,
        /// <summary>Per-second budget exhausted; caller should drop (recorder counts this).</summary>
        RateLimited,
    }

    /// <summary>
    /// Decide whether to fetch <paramref name="txid"/>. On
    /// <see cref="FetchDecision.Fetch"/> the txid is recorded in the
    /// dedupe cache and a slot in the rate-limit window is consumed.
    /// </summary>
    public FetchDecision DecideFetch(string txid)
    {
        if (string.IsNullOrEmpty(txid)) return FetchDecision.Duplicate;

        var nowMs = NowUnixMs();
        var ttlMs = _options.DedupeTtlSeconds * 1000L;

        if (_seenTxids.Count >= _options.DedupeMaxEntries)
            EvictOldEntries(nowMs);

        // Fresh insert wins. TryAdd is the only path that returns true
        // on first-seen — using AddOrUpdate's return value as a
        // duplicate discriminator is fragile because two calls within
        // the same millisecond produce equal nowMs.
        if (_seenTxids.TryAdd(txid, nowMs))
        {
            if (!TryConsumeRateBudget(nowMs))
                return FetchDecision.RateLimited;
            return FetchDecision.Fetch;
        }

        // Existing entry — check TTL.
        if (_seenTxids.TryGetValue(txid, out var existing) &&
            nowMs - existing <= ttlMs)
        {
            return FetchDecision.Duplicate;
        }

        // Stale entry — refresh and fall through to rate-limit gate.
        _seenTxids[txid] = nowMs;
        if (!TryConsumeRateBudget(nowMs))
            return FetchDecision.RateLimited;
        return FetchDecision.Fetch;
    }

    /// <summary>Forget a txid early — e.g. when the runner gives up on a
    /// fetch and wants to allow another peer's inv to retry.</summary>
    public void Forget(string txid) => _seenTxids.TryRemove(txid, out _);

    public int DedupeSize => _seenTxids.Count;

    private bool TryConsumeRateBudget(long nowMs)
    {
        // Trim window first (entries older than 1000 ms).
        while (_getDataTimestamps.TryPeek(out var oldest) && nowMs - oldest > 1000)
        {
            if (_getDataTimestamps.TryDequeue(out _))
                Interlocked.Decrement(ref _getDataCountInWindow);
        }
        if (Interlocked.Read(ref _getDataCountInWindow) >= _options.MaxGetDataPerSec)
            return false;
        _getDataTimestamps.Enqueue(nowMs);
        Interlocked.Increment(ref _getDataCountInWindow);
        return true;
    }

    private void EvictOldEntries(long nowMs)
    {
        var ttlMs = _options.DedupeTtlSeconds * 1000L;
        var cutoff = nowMs - ttlMs;
        var evicted = 0;
        var target = _options.DedupeMaxEntries / 10; // evict ~10% per pass
        foreach (var (txid, ts) in _seenTxids)
        {
            if (ts <= cutoff)
            {
                if (_seenTxids.TryRemove(txid, out _))
                    evicted++;
                if (evicted >= target) break;
            }
        }
        // Fallback: if TTL eviction didn't free enough, drop arbitrary
        // entries to keep the cap.
        while (_seenTxids.Count >= _options.DedupeMaxEntries)
        {
            foreach (var (txid, _) in _seenTxids)
            {
                _seenTxids.TryRemove(txid, out _);
                if (_seenTxids.Count < _options.DedupeMaxEntries) break;
            }
            break;
        }
    }

    private static long NowUnixMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
