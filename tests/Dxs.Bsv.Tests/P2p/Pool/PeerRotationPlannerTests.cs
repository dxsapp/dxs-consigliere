using System.Linq;

using Dxs.Bsv.P2p.Pool;

namespace Dxs.Bsv.Tests.P2p.Pool;

/// <summary>
/// Wave 6 S1 — pins for <see cref="PeerRotationPlanner"/>. This is the
/// done-when fixture for "peer rotation evicts low-scoring peer in
/// fixture" (master.md §S6). The planner is pure logic — these tests
/// open no sockets, touch no telemetry sinks.
/// </summary>
public class PeerRotationPlannerTests
{
    private static readonly PeerRotationPolicy DefaultPolicy = new();
    private readonly PeerRotationPlanner _planner = new();

    [Fact]
    public void EmptyPool_ReturnsNoOp()
    {
        var decision = _planner.Plan(System.Array.Empty<ScoredPeer>(), DefaultPolicy);
        Assert.Empty(decision.EvictKeys);
    }

    [Fact]
    public void AllPeersAboveFloor_NoEviction()
    {
        var active = new[]
        {
            new ScoredPeer("1.2.3.4:8333", new PeerScore(95)),
            new ScoredPeer("5.6.7.8:8333", new PeerScore(60)),
            new ScoredPeer("9.10.11.12:8333", new PeerScore(31)),  // just above 30 floor
        };

        var decision = _planner.Plan(active, DefaultPolicy);

        Assert.Empty(decision.EvictKeys);
    }

    [Fact]
    public void LowestScoringPeerBelowFloor_IsEvicted()
    {
        var active = new[]
        {
            new ScoredPeer("good:8333", new PeerScore(80)),
            new ScoredPeer("worst:8333", new PeerScore(10)),
            new ScoredPeer("mid:8333", new PeerScore(45)),
        };

        var decision = _planner.Plan(active, DefaultPolicy);

        var evicted = Assert.Single(decision.EvictKeys);
        Assert.Equal("worst:8333", evicted);
    }

    [Fact]
    public void OnlyOnePeerEvictedPerTick_WhenMultipleBelowFloor()
    {
        // Core Rule §5 — single-peer-per-tick. Pool with three
        // sub-floor peers must yield exactly one eviction (the
        // lowest); the rest wait for the next tick.
        var active = new[]
        {
            new ScoredPeer("a:8333", new PeerScore(5)),
            new ScoredPeer("b:8333", new PeerScore(15)),
            new ScoredPeer("c:8333", new PeerScore(25)),
            new ScoredPeer("d:8333", new PeerScore(99)),
        };

        var decision = _planner.Plan(active, DefaultPolicy);

        var evicted = Assert.Single(decision.EvictKeys);
        Assert.Equal("a:8333", evicted);
    }

    [Fact]
    public void PeerExactlyAtFloor_IsNotEvicted()
    {
        // Floor is "minimum to RETAIN" — equality keeps the peer.
        var active = new[]
        {
            new ScoredPeer("borderline:8333", new PeerScore(30)),
        };

        var decision = _planner.Plan(active, DefaultPolicy);

        Assert.Empty(decision.EvictKeys);
    }

    [Fact]
    public void CustomFloor_AppliesToDecision()
    {
        // Tightening the floor opens otherwise-healthy peers up to
        // eviction. With floor 50, the 45-scoring peer drops out.
        var policy = new PeerRotationPolicy { MinimumScoreToRetain = 50 };
        var active = new[]
        {
            new ScoredPeer("good:8333", new PeerScore(80)),
            new ScoredPeer("mid:8333", new PeerScore(45)),
        };

        var decision = _planner.Plan(active, policy);

        Assert.Equal("mid:8333", Assert.Single(decision.EvictKeys));
    }

    [Fact]
    public void FloorAtZero_NeverEvicts()
    {
        // Disabling proactive rotation: floor = 0 means the planner
        // is a no-op. PeerScore can never be negative (clamped at 0
        // by its constructor) and "< 0" never holds, so no peer is
        // ever picked.
        var policy = new PeerRotationPolicy { MinimumScoreToRetain = 0 };
        var active = new[]
        {
            new ScoredPeer("anywhere:8333", new PeerScore(0)),
        };

        var decision = _planner.Plan(active, policy);

        Assert.Empty(decision.EvictKeys);
    }

    [Fact]
    public void Decision_NoOp_Sentinel_IsReused()
    {
        var d1 = _planner.Plan(System.Array.Empty<ScoredPeer>(), DefaultPolicy);
        var d2 = _planner.Plan(
            new[] { new ScoredPeer("ok:8333", new PeerScore(80)) },
            DefaultPolicy);

        Assert.Same(RotationDecision.NoOp, d1);
        Assert.Same(RotationDecision.NoOp, d2);
    }

    [Fact]
    public void TiedLowestScores_FirstByInsertionOrderWins()
    {
        // Stable tie-break: the planner's single pass keeps the
        // first-seen minimum, mirroring "evict the oldest bad peer"
        // semantics rather than churning ties non-deterministically.
        var active = new[]
        {
            new ScoredPeer("first-low:8333", new PeerScore(10)),
            new ScoredPeer("good:8333", new PeerScore(80)),
            new ScoredPeer("second-low:8333", new PeerScore(10)),
        };

        var decision = _planner.Plan(active, DefaultPolicy);

        Assert.Equal("first-low:8333", Assert.Single(decision.EvictKeys));
    }
}
