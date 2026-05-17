#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

using Dxs.Consigliere.Data.Models.Metrics;

namespace Dxs.Consigliere.Services.Metrics;

/// <summary>
/// Wave 4 S1 — in-process tracker observing every successful tx
/// observation across the W2 + W3 runners (P2p / Bitails / JungleBus).
/// Computes per-source counts:
/// <list type="bullet">
///   <item><b>FirstSeen</b> — how many txids each source was the
///         FIRST to observe (winner of the cross-source race).</item>
///   <item><b>LagBuckets</b> — 6-bucket distribution of how long
///         after the first-source-of-record each subsequent source
///         saw the same tx. Negative lags clamp to bucket 0 per
///         Core Rule §5.</item>
///   <item><b>OnlySaw</b> — count of txids that ONLY this source
///         ever observed within the eviction window (default 5 min
///         after first-seen). Committed at eviction time.</item>
/// </list>
///
/// <para>Eviction loop is invoked by the aggregator on every snapshot
/// tick via <see cref="EvictStaleEntries"/>; we don't spin a separate
/// background task here so the tracker stays pure-logic-testable.</para>
/// </summary>
public sealed class SourceVisibilityTracker
{
    private readonly SourceVisibilityTrackerOptions _options;

    private sealed class Entry
    {
        public required string FirstSource;
        public required long FirstSeenUnixMs;
        public required HashSet<string> Sources;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _firstSeen = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _onlySaw = new(StringComparer.OrdinalIgnoreCase);
    // Each per-source bucket array is allocated lazily on first hit so
    // we don't accidentally surface sources that never observed
    // anything.
    private readonly ConcurrentDictionary<string, long[]> _lagBuckets = new(StringComparer.OrdinalIgnoreCase);

    public SourceVisibilityTracker(SourceVisibilityTrackerOptions? options = null)
    {
        _options = options ?? new SourceVisibilityTrackerOptions();
    }

    /// <summary>
    /// Record one observation. Idempotent per <c>(txId, source)</c>
    /// pair within the eviction window: a duplicate call updates no
    /// counters (the tracker is upstream of dedupe).
    /// </summary>
    public void RecordObservation(string txId, string source, DateTimeOffset observedAt)
    {
        if (string.IsNullOrEmpty(txId)) return;
        if (string.IsNullOrEmpty(source)) return;
        var observedUnixMs = observedAt.ToUnixTimeMilliseconds();

        var added = false;
        var entry = _entries.GetOrAdd(txId, _ =>
        {
            added = true;
            return new Entry
            {
                FirstSource = source,
                FirstSeenUnixMs = observedUnixMs,
                Sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { source },
            };
        });

        if (added)
        {
            Increment(_firstSeen, source);
            return;
        }

        // Existing entry; record the lag if this is a NEW source for
        // this tx.
        lock (entry)
        {
            if (!entry.Sources.Add(source)) return; // duplicate

            var lagMs = observedUnixMs - entry.FirstSeenUnixMs;
            var bucketIndex = SourceMetricsBuckets.IndexFor(lagMs);
            var buckets = _lagBuckets.GetOrAdd(source, _ => new long[SourceMetricsBuckets.Count]);
            Interlocked.Increment(ref buckets[bucketIndex]);
        }
    }

    /// <summary>
    /// Walks the in-flight tx → first-seen map and commits an
    /// <c>OnlySaw</c> increment for every entry past
    /// <see cref="SourceVisibilityTrackerOptions.EvictionWindowMs"/>
    /// that was observed by exactly one source. Idempotent under
    /// concurrent invocation (atomic remove gates the commit).
    /// </summary>
    public void EvictStaleEntries(DateTimeOffset now)
    {
        var nowUnixMs = now.ToUnixTimeMilliseconds();
        var cutoff = nowUnixMs - _options.EvictionWindowMs;

        foreach (var (txId, entry) in _entries)
        {
            if (entry.FirstSeenUnixMs > cutoff) continue;

            if (!_entries.TryRemove(txId, out var removed)) continue;
            if (removed.Sources.Count == 1)
                Increment(_onlySaw, removed.FirstSource);
        }
    }

    /// <summary>
    /// Snapshot all per-source counters into the snapshot's
    /// <c>VisibilityCounters</c> dictionary. Atomic reads per
    /// counter; the cross-counter set is eventually consistent
    /// (acceptable per master.md Core Rule §6).
    /// </summary>
    public void SnapshotInto(SourceMetricsSnapshot snapshot)
    {
        // Union all source keys across the three counter maps so
        // sources that have only one type of stat (e.g. junglebus
        // first-saw but never lagged) still surface.
        var allSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in _firstSeen.Keys) allSources.Add(k);
        foreach (var k in _onlySaw.Keys) allSources.Add(k);
        foreach (var k in _lagBuckets.Keys) allSources.Add(k);

        foreach (var source in allSources)
        {
            var counters = new SourceVisibilityCounters
            {
                FirstSeen = _firstSeen.TryGetValue(source, out var fs) ? fs : 0,
                OnlySaw = _onlySaw.TryGetValue(source, out var os) ? os : 0,
                LagBuckets = _lagBuckets.TryGetValue(source, out var buckets)
                    ? (long[])buckets.Clone()
                    : new long[SourceMetricsBuckets.Count],
            };
            snapshot.VisibilityCounters[source] = counters;
        }
    }

    public int InflightCount => _entries.Count;

    private static void Increment(ConcurrentDictionary<string, long> dict, string key)
        => dict.AddOrUpdate(key, 1L, (_, v) => v + 1L);
}

public sealed class SourceVisibilityTrackerOptions
{
    /// <summary>
    /// Default 5 minutes — long enough for a slow source to catch up,
    /// short enough to bound tracker memory at peak mempool rates.
    /// Configurable via <c>SourceMetricsConfig.EvictionWindowMs</c>.
    /// </summary>
    public long EvictionWindowMs { get; set; } = 5 * 60 * 1000L;
}
