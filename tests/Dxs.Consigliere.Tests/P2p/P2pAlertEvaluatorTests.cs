using System;
using System.Collections.Generic;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Metrics;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Services.P2p;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// Wave 6 S2 — pins for <see cref="P2pAlertEvaluator"/>. Covers all
/// 4 rules from master.md §"Critical alert poller" + the A1 pass-2
/// H1 zero-sample-window suppression pin.
/// </summary>
public class P2pAlertEvaluatorTests
{
    private static readonly AlertConfig DefaultConfig = new()
    {
        Enabled = true,
        MinPoolSize = 5,
        MinRelayBackRate = 0.30,
        ReorgDepthWindowMs = 5 * 60 * 1000,
        SourceFirstDropoutWindowMs = 60 * 60 * 1000,
    };

    private static readonly DateTimeOffset T0 =
        new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly P2pAlertEvaluator _evaluator = new();

    [Fact]
    public void Rule1_PoolBelowThreshold_FiresEvent()
    {
        var input = Input(poolSize: 2);
        var events = _evaluator.Evaluate(input);
        var alert = Assert.Single(events);
        Assert.Equal(P2pAlertType.PoolSizeBelowThreshold, alert.Type);
        Assert.Equal("2", alert.Context["poolSize"]);
        Assert.Equal("5", alert.Context["threshold"]);
    }

    [Fact]
    public void Rule1_PoolAtOrAboveThreshold_NoFire()
    {
        Assert.Empty(_evaluator.Evaluate(Input(poolSize: 5)));
        _evaluator.ResetState();
        Assert.Empty(_evaluator.Evaluate(Input(poolSize: 8)));
    }

    [Fact]
    public void Rule2_RelayBackRate_FiresWhenBelowThreshold()
    {
        // Tick 1: baseline only — no fire even if the numbers look
        // bad, since there is no previous tick to diff against.
        var baseline = Input(
            peerTelemetry: new Dictionary<string, PeerTelemetry>
            {
                ["A"] = Telemetry(relayBack: 0, getData: 0),
            });
        Assert.Empty(_evaluator.Evaluate(baseline));

        // Tick 2: ΔrelayBack=1, ΔgetData=100 → rate 0.01 << 0.30.
        var follow = Input(
            now: T0.AddMinutes(1),
            peerTelemetry: new Dictionary<string, PeerTelemetry>
            {
                ["A"] = Telemetry(relayBack: 1, getData: 100),
            });
        var alert = Assert.Single(_evaluator.Evaluate(follow));
        Assert.Equal(P2pAlertType.RelayBackRateBelowThreshold, alert.Type);
        Assert.Equal("1", alert.Context["deltaRelayBack"]);
        Assert.Equal("100", alert.Context["deltaGetDataRequested"]);
    }

    [Fact]
    public void Rule2_RelayBackRate_ZeroSampleWindow_DoesNotFire()
    {
        // A1 pass-2 H1 pin: when no peer requested any inv during
        // the window (sum(ΔGetDataRequested) == 0), the rule is a
        // no-op even though sum(ΔRelayBackInv) is also 0 (which
        // would yield 0/1 = 0 under the old max(1, …) formula).
        var baseline = Input(peerTelemetry: new Dictionary<string, PeerTelemetry>
        {
            ["A"] = Telemetry(relayBack: 0, getData: 0),
        });
        _evaluator.Evaluate(baseline);

        var quietTick = Input(
            now: T0.AddMinutes(1),
            peerTelemetry: new Dictionary<string, PeerTelemetry>
            {
                ["A"] = Telemetry(relayBack: 0, getData: 0),
            });

        Assert.Empty(_evaluator.Evaluate(quietTick));
    }

    [Fact]
    public void Rule2_RelayBackRate_AboveThreshold_DoesNotFire()
    {
        // Baseline + healthy: 30 relay-back out of 100 requested = 0.30 = threshold (NOT below).
        var baseline = Input(peerTelemetry: new Dictionary<string, PeerTelemetry>
        {
            ["A"] = Telemetry(relayBack: 0, getData: 0),
        });
        _evaluator.Evaluate(baseline);

        var follow = Input(
            now: T0.AddMinutes(1),
            peerTelemetry: new Dictionary<string, PeerTelemetry>
            {
                ["A"] = Telemetry(relayBack: 30, getData: 100),
            });
        Assert.Empty(_evaluator.Evaluate(follow));
    }

    [Fact]
    public void Rule2_RelayBackRate_NegativeDelta_ClampedToZero_DoesNotPoisonRate()
    {
        // Counter reset on session reconnect: previous tick had 100
        // requests, this tick has 50 (reset). Delta clamps to 0; the
        // rate computation uses only positive contributions.
        var baseline = Input(peerTelemetry: new Dictionary<string, PeerTelemetry>
        {
            ["A"] = Telemetry(relayBack: 0, getData: 100),
        });
        _evaluator.Evaluate(baseline);

        // After reset: counters go down, no signal to evaluate.
        var resetTick = Input(
            now: T0.AddMinutes(1),
            peerTelemetry: new Dictionary<string, PeerTelemetry>
            {
                ["A"] = Telemetry(relayBack: 0, getData: 50),
            });

        // With deltas clamped to zero, sum(ΔGetData)=0 → no fire.
        Assert.Empty(_evaluator.Evaluate(resetTick));
    }

    [Fact]
    public void Rule3_ReorgDepth_FiresWhenRecent()
    {
        var input = Input(lastDegradedReorgAt: T0.AddMinutes(-1));
        var alert = Assert.Single(_evaluator.Evaluate(input));
        Assert.Equal(P2pAlertType.ReorgDepthExceeded, alert.Type);
        Assert.Contains("Degraded reorg observed", alert.Detail);
    }

    [Fact]
    public void Rule3_ReorgDepth_DoesNotFireWhenOutsideWindow()
    {
        var input = Input(lastDegradedReorgAt: T0.AddMinutes(-10));
        Assert.Empty(_evaluator.Evaluate(input));
    }

    [Fact]
    public void Rule3_ReorgDepth_NeverReorged_NoFire()
    {
        Assert.Empty(_evaluator.Evaluate(Input(lastDegradedReorgAt: null)));
    }

    [Fact]
    public void Rule4_SourceFirstDropout_FiresWhenOneSourceWentSilentButOthersDidNot()
    {
        var snapshots = new[]
        {
            Snapshot(T0.AddHours(-1), p2pFirstSeen: 100, bitailsFirstSeen: 100, junglebusFirstSeen: 100),
            Snapshot(T0, p2pFirstSeen: 100, bitailsFirstSeen: 110, junglebusFirstSeen: 130),
        };
        var events = _evaluator.Evaluate(Input(windowSnapshots: snapshots));

        var alert = Assert.Single(events);
        Assert.Equal(P2pAlertType.SourceFirstDropout, alert.Type);
        Assert.Equal(TxObservationSource.P2p, alert.Context["source"]);
    }

    [Fact]
    public void Rule4_SourceFirstDropout_SystemWideQuietWindow_DoesNotFire()
    {
        // Master.md A1-followup H1: a system-wide quiet period is
        // not a dropout. All three sources idle == network idle.
        var snapshots = new[]
        {
            Snapshot(T0.AddHours(-1), p2pFirstSeen: 100, bitailsFirstSeen: 100, junglebusFirstSeen: 100),
            Snapshot(T0, p2pFirstSeen: 100, bitailsFirstSeen: 100, junglebusFirstSeen: 100),
        };
        Assert.Empty(_evaluator.Evaluate(Input(windowSnapshots: snapshots)));
    }

    [Fact]
    public void Rule4_SourceFirstDropout_LessThanTwoSnapshots_NoFire()
    {
        Assert.Empty(_evaluator.Evaluate(Input(windowSnapshots: Array.Empty<SourceMetricsSnapshot>())));
        Assert.Empty(_evaluator.Evaluate(Input(windowSnapshots: new[]
        {
            Snapshot(T0, p2pFirstSeen: 100, bitailsFirstSeen: 110, junglebusFirstSeen: 130),
        })));
    }

    [Fact]
    public void Rule4_SourceFirstDropout_ShortSpanWindow_DoesNotFire()
    {
        // S1+S2 audit H1 pin: two snapshots 30 s apart cannot
        // assert "zero first-seen across 1 h" — the coverage isn't
        // there. Evaluator must short-circuit until the snapshot
        // span is at least the configured window.
        var snapshots = new[]
        {
            Snapshot(T0.AddSeconds(-30), p2pFirstSeen: 100, bitailsFirstSeen: 100, junglebusFirstSeen: 100),
            Snapshot(T0, p2pFirstSeen: 100, bitailsFirstSeen: 110, junglebusFirstSeen: 130),
        };
        Assert.Empty(_evaluator.Evaluate(Input(windowSnapshots: snapshots)));
    }

    [Fact]
    public void Rule4_SourceFirstDropout_ExactlyWindowSpan_Fires()
    {
        // The boundary is inclusive: a span of exactly the window
        // length is enough coverage to evaluate.
        var snapshots = new[]
        {
            Snapshot(T0.AddMilliseconds(-DefaultConfig.SourceFirstDropoutWindowMs),
                p2pFirstSeen: 100, bitailsFirstSeen: 100, junglebusFirstSeen: 100),
            Snapshot(T0, p2pFirstSeen: 100, bitailsFirstSeen: 110, junglebusFirstSeen: 130),
        };
        var alert = Assert.Single(_evaluator.Evaluate(Input(windowSnapshots: snapshots)));
        Assert.Equal(P2pAlertType.SourceFirstDropout, alert.Type);
        Assert.Equal(TxObservationSource.P2p, alert.Context["source"]);
    }

    [Fact]
    public void Rule4_TwoSourcesDropOut_FiresTwoEvents()
    {
        var snapshots = new[]
        {
            Snapshot(T0.AddHours(-1), p2pFirstSeen: 100, bitailsFirstSeen: 100, junglebusFirstSeen: 100),
            Snapshot(T0, p2pFirstSeen: 100, bitailsFirstSeen: 100, junglebusFirstSeen: 130),
        };
        var events = _evaluator.Evaluate(Input(windowSnapshots: snapshots));
        Assert.Equal(2, events.Count);
        Assert.Equal(P2pAlertType.SourceFirstDropout, events[0].Type);
        Assert.Equal(P2pAlertType.SourceFirstDropout, events[1].Type);
    }

    [Fact]
    public void MultipleRules_FireOnSameTick_GetUniqueDocIds()
    {
        // Pool below threshold AND reorg recent — two events, two
        // distinct ids so the Raven save doesn't collide.
        var input = Input(poolSize: 2, lastDegradedReorgAt: T0.AddMinutes(-1));
        var events = _evaluator.Evaluate(input);
        Assert.Equal(2, events.Count);
        Assert.NotEqual(events[0].Id, events[1].Id);
    }

    [Fact]
    public void Reset_DropsBaseline_NextTickIsBaselineOnly()
    {
        // Establish then reset. The next tick must NOT fire
        // relay-back even if the numbers look bad (it's a fresh
        // baseline).
        _evaluator.Evaluate(Input(peerTelemetry: new Dictionary<string, PeerTelemetry>
        {
            ["A"] = Telemetry(relayBack: 0, getData: 0),
        }));
        _evaluator.ResetState();

        var freshTickWithBadNumbers = Input(peerTelemetry: new Dictionary<string, PeerTelemetry>
        {
            ["A"] = Telemetry(relayBack: 0, getData: 100),
        });
        Assert.Empty(_evaluator.Evaluate(freshTickWithBadNumbers));
    }

    // -------- helpers --------

    private static P2pAlertEvaluatorInput Input(
        int poolSize = 5,
        DateTimeOffset? lastDegradedReorgAt = null,
        IReadOnlyDictionary<string, PeerTelemetry>? peerTelemetry = null,
        IReadOnlyList<SourceMetricsSnapshot>? windowSnapshots = null,
        DateTimeOffset? now = null) =>
        new(
            PoolSize: poolSize,
            LastDegradedReorgAt: lastDegradedReorgAt,
            PeerTelemetry: peerTelemetry ?? new Dictionary<string, PeerTelemetry>(),
            WindowSnapshots: windowSnapshots ?? Array.Empty<SourceMetricsSnapshot>(),
            Config: DefaultConfig,
            Now: now ?? T0);

    private static PeerTelemetry Telemetry(long relayBack, long getData) => new(
        BytesIn: 0,
        BytesOut: 0,
        LastRecvUtc: null,
        LastSendUtc: null,
        PingRttP50Ms: 0.0,
        PingRttP95Ms: 0.0,
        PingSampleCount: 0,
        GetDataRequestedCount: getData,
        GetDataServedCount: 0,
        GetDataServeP50Ms: 0.0,
        GetDataServeP95Ms: 0.0,
        RelayBackInvCount: relayBack,
        RejectByClass: new Dictionary<RejectClass, long>(),
        ProtocolViolationCount: 0,
        LastDisconnectReason: null);

    private static SourceMetricsSnapshot Snapshot(
        DateTimeOffset at,
        long p2pFirstSeen,
        long bitailsFirstSeen,
        long junglebusFirstSeen) => new()
        {
            Id = $"metrics/sources/{at.ToUnixTimeMilliseconds():D14}",
            SnapshotUnixMs = at.ToUnixTimeMilliseconds(),
            VisibilityCounters = new Dictionary<string, SourceVisibilityCounters>(StringComparer.OrdinalIgnoreCase)
            {
                [TxObservationSource.P2p] = new() { FirstSeen = p2pFirstSeen },
                [TxObservationSource.Bitails] = new() { FirstSeen = bitailsFirstSeen },
                [TxObservationSource.JungleBus] = new() { FirstSeen = junglebusFirstSeen },
            },
        };
}
