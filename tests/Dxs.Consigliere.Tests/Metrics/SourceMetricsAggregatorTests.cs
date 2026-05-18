using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Metrics;
using Dxs.Consigliere.Services.Metrics;
using Dxs.Consigliere.Services.P2p;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Metrics;

/// <summary>
/// Wave 4 A2 M3 — round-trip pin via the
/// <see cref="ISnapshotPersistence"/> abstraction. A fake persistence
/// captures every Store / Delete call so the aggregator's behaviour
/// can be asserted without standing up Raven embedded.
///
/// What's pinned here:
/// 1. <see cref="SourceMetricsAggregator.TickOnceAsync"/> writes one
///    snapshot per call.
/// 2. The snapshot id is lex-sortable (D14-padded timestamp).
/// 3. Retention eviction deletes oldest-first beyond the configured
///    retention count.
/// 4. Eviction does not delete when below the retention threshold.
/// </summary>
public class SourceMetricsAggregatorTests
{
    private sealed class FakePersistence : ISnapshotPersistence
    {
        public readonly ConcurrentDictionary<string, SourceMetricsSnapshot> Stored = new();
        public readonly ConcurrentBag<string> Deleted = new();

        public Task StoreAsync(SourceMetricsSnapshot snapshot, CancellationToken ct)
        {
            Stored[snapshot.Id] = snapshot;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetAllIdsOrderedAsync(CancellationToken ct)
        {
            var ids = Stored.Keys.OrderBy(k => k, System.StringComparer.Ordinal).ToList();
            return Task.FromResult<IReadOnlyList<string>>(ids);
        }

        public Task DeleteAsync(IReadOnlyList<string> ids, CancellationToken ct)
        {
            foreach (var id in ids)
            {
                Stored.TryRemove(id, out _);
                Deleted.Add(id);
            }
            return Task.CompletedTask;
        }
    }

    private static (SourceMetricsAggregator aggregator,
                    FakePersistence persistence,
                    SourceVisibilityTracker tracker,
                    SourceObservationRecorder observation) Build(int retention)
    {
        var tracker = new SourceVisibilityTracker();
        var observation = new SourceObservationRecorder();
        var rebroadcast = new OrphanedTxRebroadcastRecorder();
        var health = new BsvP2pHealth();
        var collector = new SourceMetricsCollector(observation, rebroadcast, tracker, health);
        var persistence = new FakePersistence();
        var aggregator = new SourceMetricsAggregator(
            collector, tracker, persistence,
            Options.Create(new SourceMetricsConfig
            {
                Enabled = false, // disable the auto-loop; tests use TickOnceAsync.
                SnapshotIntervalMs = 1_000,
                SnapshotRetentionCount = retention,
            }),
            NullLogger<SourceMetricsAggregator>.Instance);
        return (aggregator, persistence, tracker, observation);
    }

    [Fact]
    public async Task TickOnceAsync_PersistsExactlyOneSnapshot()
    {
        var (aggregator, persistence, _, observation) = Build(retention: 100);
        observation.RecordMatched();

        var snap = await aggregator.TickOnceAsync(CancellationToken.None);
        Assert.NotNull(snap);
        Assert.Single(persistence.Stored);
        Assert.True(persistence.Stored.ContainsKey(snap.Id));
        Assert.Equal(1, persistence.Stored[snap.Id].ObservationCounters["p2p"].Matched);
    }

    [Fact]
    public async Task TickOnceAsync_NoEviction_WhenBelowRetention()
    {
        var (aggregator, persistence, _, _) = Build(retention: 5);
        for (var i = 0; i < 3; i++)
        {
            await aggregator.TickOnceAsync(CancellationToken.None);
            // Distinct ms so the snapshot id (D14-padded unix ms) is
            // unique per tick.
            await Task.Delay(2);
        }

        Assert.Equal(3, persistence.Stored.Count);
        Assert.Empty(persistence.Deleted);
    }

    [Fact]
    public async Task TickOnceAsync_EvictsOldestExcess_WhenAboveRetention()
    {
        var (aggregator, persistence, _, _) = Build(retention: 3);
        // Generate 5 distinct snapshots with monotonic ids (real
        // clock progression). Brief sleep between calls so the
        // unix-ms-based id is unique.
        var ids = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var snap = await aggregator.TickOnceAsync(CancellationToken.None);
            ids.Add(snap.Id);
            await Task.Delay(2);
        }

        // After the last tick, eviction kicks in:
        //   stored count was 5, retention = 3 → delete oldest 2.
        Assert.Equal(3, persistence.Stored.Count);
        Assert.Equal(2, persistence.Deleted.Count);
        // The two deleted ids are the lex-smallest (oldest) two.
        var deletedSet = persistence.Deleted.ToHashSet();
        Assert.Contains(ids[0], deletedSet);
        Assert.Contains(ids[1], deletedSet);
        // The three remaining are the newest.
        Assert.Contains(ids[2], persistence.Stored.Keys);
        Assert.Contains(ids[3], persistence.Stored.Keys);
        Assert.Contains(ids[4], persistence.Stored.Keys);
    }

    [Fact]
    public async Task TickOnceAsync_TriggersTrackerEviction()
    {
        // The aggregator calls SourceVisibilityTracker.EvictStaleEntries
        // before Collect. Pin this by setting a tiny window, recording
        // an observation, sleeping past the window, and asserting the
        // tracker's InflightCount drops to 0 after the tick.
        var (aggregator, _, tracker, _) = Build(retention: 100);
        // We can't reach into the tracker's options here, but the
        // aggregator's tick still calls EvictStaleEntries(now); for a
        // default 5-min window any test-realistic Sleep won't cross
        // it. Verify the call WAS issued by inserting a far-past
        // observation that's already past the window.
        tracker.RecordObservation("ancient-tx", "p2p",
            System.DateTimeOffset.UtcNow.AddHours(-1));
        Assert.Equal(1, tracker.InflightCount);

        await aggregator.TickOnceAsync(CancellationToken.None);

        Assert.Equal(0, tracker.InflightCount);
    }

    [Fact]
    public async Task TickOnceAsync_DisabledConfig_StillExecutes()
    {
        // Enabled=false short-circuits the AUTO LOOP (StartAsync no-op),
        // but TickOnceAsync remains callable for tests / admin manual
        // trigger.
        var (aggregator, persistence, _, _) = Build(retention: 100);
        await aggregator.TickOnceAsync(CancellationToken.None);
        Assert.Single(persistence.Stored);
    }
}
