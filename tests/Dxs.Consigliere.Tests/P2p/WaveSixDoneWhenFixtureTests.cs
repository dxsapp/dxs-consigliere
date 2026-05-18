using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Pool;
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
/// Wave 6 S6 — fixture validation suite. Pins every program-stated
/// done-when from <c>docs/stream-tasks/production-ops-wave/master.md</c>
/// §Definition of Done. The test names below match the DoD list
/// VERBATIM; renames require a master.md amendment.
///
/// <para>Each test drives the production poller (via the W6 S6
/// test-input ctor seam) or the production planner against
/// deterministic input.</para>
/// </summary>
public class WaveSixDoneWhenFixtureTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PeerRotationPlanner_EvictsLowestScoringPeer()
    {
        // S1 done-when. The planner returns a single-element evict
        // list with the lowest-scoring peer when any peer is below
        // the retain floor.
        var planner = new PeerRotationPlanner();
        var active = new[]
        {
            new ScoredPeer("ok:8333", new PeerScore(80)),
            new ScoredPeer("worst:8333", new PeerScore(5)),
            new ScoredPeer("mid:8333", new PeerScore(45)),
        };
        var decision = planner.Plan(active, new PeerRotationPolicy());
        Assert.Equal("worst:8333", Assert.Single(decision.EvictKeys));
    }

    [Fact]
    public async Task P2pAlertPoller_PoolBelowThreshold_FiresEvent()
    {
        // S2 / S6 done-when. PoolSize=1 < MinPoolSize=5 → one alert
        // persisted with the documented Context shape.
        var (poller, repo) = BuildPoller(builder: (now, _) => Task.FromResult(
            BuildInput(poolSize: 1, now: now)));

        var fired = await poller.TickOnceAsync(CancellationToken.None);

        Assert.Equal(1, fired);
        var alert = Assert.Single(repo.Stored.Values);
        Assert.Equal(P2pAlertType.PoolSizeBelowThreshold, alert.Type);
        Assert.Equal("1", alert.Context["poolSize"]);
        Assert.Equal("5", alert.Context["threshold"]);
    }

    [Fact]
    public async Task P2pAlertPoller_RelayBackRateBelowThreshold_FiresEvent()
    {
        // S2 / S6 done-when. The evaluator carries previous-tick
        // state inside the same poller instance; we feed two ticks
        // with growing PeerTelemetry counters. Tick 1 sets baseline;
        // tick 2 yields Δ relay-back = 1, Δ getData = 100 → 1 %
        // rate < 30 % threshold → fire.
        var peerKey = "peer-a:8333";
        var counterAt = new Dictionary<DateTimeOffset, (long Relay, long GetData)>
        {
            [T0] = (0, 0),
            [T0.AddMinutes(1)] = (1, 100),
        };

        var (poller, repo) = BuildPoller(builder: (now, _) =>
        {
            var (relay, get) = counterAt[now];
            return Task.FromResult(BuildInput(
                poolSize: 8, // suppress pool rule
                now: now,
                peerTelemetry: new Dictionary<string, PeerTelemetry>
                {
                    [peerKey] = Tel(relay, get),
                }));
        });

        await poller.TickOnceAsync(CancellationToken.None, T0);
        Assert.Empty(repo.Stored.Values
            .Where(e => e.Type == P2pAlertType.RelayBackRateBelowThreshold));

        await poller.TickOnceAsync(CancellationToken.None, T0.AddMinutes(1));

        var relayAlert = Assert.Single(repo.Stored.Values
            .Where(e => e.Type == P2pAlertType.RelayBackRateBelowThreshold));
        Assert.Equal("1", relayAlert.Context["deltaRelayBack"]);
        Assert.Equal("100", relayAlert.Context["deltaGetDataRequested"]);
    }

    [Fact]
    public async Task P2pAlertPoller_RelayBackRate_ZeroSampleWindow_DoesNotFire()
    {
        // S6 / A1 pass-2 H1 regression pin. Two ticks with zero
        // Δ getData (quiet network). The rule MUST NOT fire — no
        // signal to evaluate.
        var peerKey = "peer-quiet:8333";
        var counterAt = new Dictionary<DateTimeOffset, (long Relay, long GetData)>
        {
            [T0] = (0, 0),
            [T0.AddMinutes(1)] = (0, 0),
        };

        var (poller, repo) = BuildPoller(builder: (now, _) =>
        {
            var (relay, get) = counterAt[now];
            return Task.FromResult(BuildInput(
                poolSize: 8,
                now: now,
                peerTelemetry: new Dictionary<string, PeerTelemetry>
                {
                    [peerKey] = Tel(relay, get),
                }));
        });

        await poller.TickOnceAsync(CancellationToken.None, T0);
        await poller.TickOnceAsync(CancellationToken.None, T0.AddMinutes(1));

        Assert.Empty(repo.Stored.Values
            .Where(e => e.Type == P2pAlertType.RelayBackRateBelowThreshold));
    }

    [Fact]
    public async Task P2pAlertPoller_ReorgDepthExceeded_FiresEvent()
    {
        // S2 / S6 done-when. LastDegradedReorgAt within the window
        // → fire.
        var (poller, repo) = BuildPoller(builder: (now, _) => Task.FromResult(
            BuildInput(
                poolSize: 8,
                now: now,
                lastDegradedReorgAt: now.AddMinutes(-1))));

        await poller.TickOnceAsync(CancellationToken.None);

        var alert = Assert.Single(repo.Stored.Values
            .Where(e => e.Type == P2pAlertType.ReorgDepthExceeded));
        Assert.Contains("Degraded reorg observed", alert.Detail);
    }

    [Fact]
    public async Task P2pAlertPoller_SourceFirstDropout_FiresEvent()
    {
        // S2 / S6 done-when. One source idle across the snapshot
        // window while the other two move → fire.
        var (poller, repo) = BuildPoller(builder: (now, _) =>
        {
            var snapshots = new[]
            {
                Snapshot(now.AddHours(-1), p2p: 100, bitails: 100, junglebus: 100),
                Snapshot(now, p2p: 100, bitails: 110, junglebus: 130),
            };
            return Task.FromResult(BuildInput(
                poolSize: 8,
                now: now,
                windowSnapshots: snapshots));
        });

        await poller.TickOnceAsync(CancellationToken.None);

        var alert = Assert.Single(repo.Stored.Values
            .Where(e => e.Type == P2pAlertType.SourceFirstDropout));
        Assert.Equal(TxObservationSource.P2p, alert.Context["source"]);
    }

    // -------- shared fixture helpers --------

    private static (P2pAlertPoller poller, FakeAlertRepository repo) BuildPoller(
        Func<DateTimeOffset, CancellationToken, Task<P2pAlertEvaluatorInput>> builder)
    {
        var repo = new FakeAlertRepository();
        var snapshots = new FakeSnapshotPersistence();
        var health = new BsvP2pHealth();
        var cfg = new BsvP2pConfig { Alert = new AlertConfig { Enabled = true } };
        var poller = new P2pAlertPoller(
            new P2pAlertEvaluator(),
            repo,
            snapshots,
            health,
            Options.Create(cfg),
            NullLogger<P2pAlertPoller>.Instance,
            testInputBuilder: builder);
        return (poller, repo);
    }

    private static P2pAlertEvaluatorInput BuildInput(
        int poolSize,
        DateTimeOffset now,
        DateTimeOffset? lastDegradedReorgAt = null,
        IReadOnlyDictionary<string, PeerTelemetry>? peerTelemetry = null,
        IReadOnlyList<SourceMetricsSnapshot>? windowSnapshots = null) =>
        new(
            PoolSize: poolSize,
            LastDegradedReorgAt: lastDegradedReorgAt,
            PeerTelemetry: peerTelemetry ?? new Dictionary<string, PeerTelemetry>(),
            WindowSnapshots: windowSnapshots ?? Array.Empty<SourceMetricsSnapshot>(),
            Config: new AlertConfig
            {
                Enabled = true,
                MinPoolSize = 5,
                MinRelayBackRate = 0.30,
                ReorgDepthWindowMs = 5 * 60 * 1000,
                SourceFirstDropoutWindowMs = 60 * 60 * 1000,
            },
            Now: now);

    private static PeerTelemetry Tel(long relayBack, long getData) => new(
        BytesIn: 0, BytesOut: 0, LastRecvUtc: null, LastSendUtc: null,
        PingRttP50Ms: 0.0, PingRttP95Ms: 0.0, PingSampleCount: 0,
        GetDataRequestedCount: getData, GetDataServedCount: 0,
        GetDataServeP50Ms: 0.0, GetDataServeP95Ms: 0.0,
        RelayBackInvCount: relayBack,
        RejectByClass: new Dictionary<RejectClass, long>(),
        ProtocolViolationCount: 0, LastDisconnectReason: null);

    private static SourceMetricsSnapshot Snapshot(
        DateTimeOffset at,
        long p2p,
        long bitails,
        long junglebus) => new()
        {
            Id = $"metrics/sources/{at.ToUnixTimeMilliseconds():D14}",
            SnapshotUnixMs = at.ToUnixTimeMilliseconds(),
            VisibilityCounters = new Dictionary<string, SourceVisibilityCounters>(StringComparer.OrdinalIgnoreCase)
            {
                [TxObservationSource.P2p] = new() { FirstSeen = p2p },
                [TxObservationSource.Bitails] = new() { FirstSeen = bitails },
                [TxObservationSource.JungleBus] = new() { FirstSeen = junglebus },
            },
        };

    private sealed class FakeAlertRepository : IAlertEventRepository
    {
        public readonly ConcurrentDictionary<string, P2pAlertEvent> Stored = new();
        public Task SaveAsync(P2pAlertEvent ev, CancellationToken ct)
        {
            Stored[ev.Id] = ev;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<string>> GetAllIdsOrderedAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(
                Stored.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList());
        public Task DeleteAsync(IReadOnlyList<string> ids, CancellationToken ct)
        {
            foreach (var id in ids) Stored.TryRemove(id, out _);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<P2pAlertEvent>> GetRecentAsync(int limit, long? sinceUnixMs, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<P2pAlertEvent>>(
                Stored.Values
                    .Where(e => !sinceUnixMs.HasValue || e.AlertUnixMs > sinceUnixMs.Value)
                    .OrderByDescending(e => e.Id)
                    .Take(limit)
                    .ToList());
    }

    private sealed class FakeSnapshotPersistence : ISnapshotPersistence
    {
        public Task StoreAsync(SourceMetricsSnapshot s, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GetAllIdsOrderedAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task DeleteAsync(IReadOnlyList<string> ids, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<SourceMetricsSnapshot>> GetSnapshotsInWindowAsync(
            long fromUnixMs, long toUnixMs, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SourceMetricsSnapshot>>(Array.Empty<SourceMetricsSnapshot>());
    }
}
