using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Metrics;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.Metrics;
using Dxs.Consigliere.Services.P2p;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// Wave 6 S2 — integration pins for <see cref="P2pAlertPoller"/>.
/// Drives <c>TickOnceAsync</c> against a fake repository + fake
/// snapshot persistence and asserts the evaluator's events land
/// in the fake store. Done-when "alert fires when pool drops
/// below threshold in fixture".
/// </summary>
public class P2pAlertPollerTests
{
    private sealed class FakeAlertRepository : IAlertEventRepository
    {
        public readonly ConcurrentDictionary<string, P2pAlertEvent> Stored = new();
        public readonly ConcurrentBag<string> Deleted = new();

        public Task SaveAsync(P2pAlertEvent alertEvent, CancellationToken ct)
        {
            Stored[alertEvent.Id] = alertEvent;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetAllIdsOrderedAsync(CancellationToken ct)
        {
            var ids = Stored.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
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

        public Task<IReadOnlyList<P2pAlertEvent>> GetRecentAsync(
            int limit, long? sinceUnixMs, CancellationToken ct)
        {
            var hits = Stored.Values
                .Where(e => !sinceUnixMs.HasValue || e.AlertUnixMs > sinceUnixMs.Value)
                .OrderByDescending(e => e.Id)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<P2pAlertEvent>>(hits);
        }
    }

    private sealed class FakeSnapshotPersistence : ISnapshotPersistence
    {
        public IReadOnlyList<SourceMetricsSnapshot> WindowResponse { get; set; }
            = Array.Empty<SourceMetricsSnapshot>();
        public bool ThrowOnWindowRead { get; set; }

        public Task StoreAsync(SourceMetricsSnapshot s, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GetAllIdsOrderedAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task DeleteAsync(IReadOnlyList<string> ids, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<SourceMetricsSnapshot>> GetSnapshotsInWindowAsync(
            long fromUnixMs, long toUnixMs, CancellationToken ct)
        {
            if (ThrowOnWindowRead)
                throw new InvalidOperationException("simulated raven fault");
            return Task.FromResult(WindowResponse);
        }
    }

    private static (P2pAlertPoller poller,
                    FakeAlertRepository repo,
                    FakeSnapshotPersistence snapshots,
                    BsvP2pHealth health) Build(AlertConfig? cfg = null)
    {
        var repo = new FakeAlertRepository();
        var snapshots = new FakeSnapshotPersistence();
        var health = new BsvP2pHealth(); // unbound → PoolSize=0, no sessions, no reorg
        var config = new BsvP2pConfig { Alert = cfg ?? new AlertConfig { Enabled = true } };

        var poller = new P2pAlertPoller(
            new P2pAlertEvaluator(),
            repo,
            snapshots,
            health,
            Options.Create(config),
            NullLogger<P2pAlertPoller>.Instance);

        return (poller, repo, snapshots, health);
    }

    [Fact]
    public async Task TickOnceAsync_PoolBelowThreshold_FiresAndPersistsEvent()
    {
        var (poller, repo, _, _) = Build(new AlertConfig { Enabled = true, MinPoolSize = 5 });

        var fired = await poller.TickOnceAsync(CancellationToken.None);

        Assert.Equal(1, fired);
        var alert = Assert.Single(repo.Stored.Values);
        Assert.Equal(P2pAlertType.PoolSizeBelowThreshold, alert.Type);
        Assert.Equal("0", alert.Context["poolSize"]);
        Assert.StartsWith("p2p/alerts/", alert.Id);
    }

    [Fact]
    public async Task TickOnceAsync_NoConditions_PersistsNothing()
    {
        var (poller, repo, _, _) = Build(new AlertConfig
        {
            Enabled = true,
            MinPoolSize = 0,        // any pool size satisfies
            ReorgDepthWindowMs = 1, // narrow enough to never match the null
        });

        var fired = await poller.TickOnceAsync(CancellationToken.None);

        Assert.Equal(0, fired);
        Assert.Empty(repo.Stored);
    }

    [Fact]
    public async Task TickOnceAsync_EvictsExcessAlertsBeyondRetention()
    {
        var (poller, repo, _, _) = Build(new AlertConfig
        {
            Enabled = true,
            MinPoolSize = 5,             // forces fire each tick
            AlertRetentionEvents = 2,
        });

        // Seed repo over the retention budget.
        for (var i = 0; i < 5; i++)
        {
            await repo.SaveAsync(
                new P2pAlertEvent
                {
                    Id = $"p2p/alerts/{i:D14}",
                    AlertUnixMs = i,
                    Type = P2pAlertType.PoolSizeBelowThreshold,
                },
                CancellationToken.None);
        }

        await poller.TickOnceAsync(CancellationToken.None);

        // After the tick: 5 seeded + 1 new = 6 → retention=2 → keep 2 newest.
        Assert.Equal(2, repo.Stored.Count);
        // Oldest seeded entries (ids 0..3) are gone.
        Assert.DoesNotContain("p2p/alerts/00000000000000", repo.Stored.Keys);
        Assert.DoesNotContain("p2p/alerts/00000000000001", repo.Stored.Keys);
    }

    [Fact]
    public async Task TickOnceAsync_SnapshotStoreFault_DoesNotPoisonOtherRules()
    {
        // A poisoned snapshot persistence should not stop Pool /
        // RelayBack / Reorg rules from running. We fault the
        // snapshot store and assert the pool rule still fires.
        var (poller, repo, snapshots, _) = Build(new AlertConfig
        {
            Enabled = true,
            MinPoolSize = 5,
        });
        snapshots.ThrowOnWindowRead = true;

        var fired = await poller.TickOnceAsync(CancellationToken.None);

        Assert.Equal(1, fired);
        Assert.Equal(P2pAlertType.PoolSizeBelowThreshold,
            Assert.Single(repo.Stored.Values).Type);
    }
}
