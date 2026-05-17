using System;
using System.Linq;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;

namespace Dxs.Bsv.Tests.P2p.Chain;

/// <summary>
/// Wave 3 S1 — pins the <see cref="ReorgDetector"/> behaviour for
/// every Core Rule §4 scenario: no-fork, equal-height (active wins),
/// 1-deep / 2-deep / 5-deep forks, and fork-point-below-retention
/// (degraded).
/// </summary>
public class ReorgDetectorTests
{
    private const int SmallWindow = 10;

    private static HeadersChain BuildSeededChain(int retentionWindow, out BlockHeader genesis)
    {
        var chain = new HeadersChain(new HeadersChainOptions { RetainedHeaderCount = retentionWindow });
        genesis = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x01);
        chain.Seed(genesis, height: 0);
        return chain;
    }

    /// <summary>
    /// Build N headers extending <paramref name="parent"/>; feed each
    /// through <c>TryExtend</c> so the chain stores them. Returns the
    /// list of headers in connect order (oldest-first).
    /// </summary>
    private static BlockHeader[] ExtendActiveChain(HeadersChain chain, BlockHeader parent, int count, byte merkleFill)
    {
        var built = new BlockHeader[count];
        var p = parent;
        for (var i = 0; i < count; i++)
        {
            var h = HeaderTestUtil.BuildChild(p, merkleFill: merkleFill);
            var ex = chain.TryExtend(h);
            Assert.IsType<ExtendResult.Extended>(ex);
            built[i] = h;
            p = h;
        }
        return built;
    }

    /// <summary>
    /// Build a fork-side chain that branches off <paramref name="forkParent"/>
    /// (which MUST already be in <paramref name="chain"/>) and feed the
    /// headers through <c>TryExtend</c>. The first header returns a
    /// <see cref="ExtendResult.Fork"/>; subsequent headers extend the
    /// fork-side ancestor and also produce <see cref="ExtendResult.Fork"/>
    /// results because the active chain remains the tip-bearing one.
    /// </summary>
    private static BlockHeader[] ExtendForkChain(HeadersChain chain, BlockHeader forkParent, int count, byte merkleFill)
    {
        var built = new BlockHeader[count];
        var p = forkParent;
        for (var i = 0; i < count; i++)
        {
            var h = HeaderTestUtil.BuildChild(p, merkleFill: merkleFill, timestamp: 1700000001);
            var ex = chain.TryExtend(h);
            Assert.IsType<ExtendResult.Fork>(ex);
            built[i] = h;
            p = h;
        }
        return built;
    }

    private static string DisplayHex(BlockHeader header)
    {
        var wire = BlockHeaderHasher.Hash(header);
        return BlockHeaderHasher.ToDisplayHex(wire);
    }

    [Fact]
    public void Detect_ChainEmpty_ReturnsNull()
    {
        var chain = new HeadersChain(new HeadersChainOptions { RetainedHeaderCount = SmallWindow });
        var fakeHeader = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x01);
        var detector = new ReorgDetector();

        Assert.Null(detector.TryDetect(chain, fakeHeader));
    }

    [Fact]
    public void Detect_NoFork_ReturnsNull()
    {
        var chain = BuildSeededChain(SmallWindow, out var genesis);
        var active = ExtendActiveChain(chain, genesis, count: 3, merkleFill: 0xA1);
        var detector = new ReorgDetector();

        // The current tip is the active tip — no fork to detect.
        Assert.Null(detector.TryDetect(chain, chain.Tip!));
    }

    [Fact]
    public void Detect_EqualHeight_FirstSeenWins_ReturnsNull()
    {
        // active chain: genesis → A1 (height 1; current tip)
        // fork side:    genesis → B1 (height 1; equal)
        var chain = BuildSeededChain(SmallWindow, out var genesis);
        var _active = ExtendActiveChain(chain, genesis, count: 1, merkleFill: 0xA1);
        var fork = ExtendForkChain(chain, genesis, count: 1, merkleFill: 0xB1);

        var detector = new ReorgDetector();
        Assert.Null(detector.TryDetect(chain, fork[0]));
    }

    [Fact]
    public void Detect_OneDeepFork_ReturnsPlan_WithOneOrphan()
    {
        // active chain: genesis → A1 (height 1; tip)
        // fork side:    genesis → B1 → B2 (height 2; longer)
        var chain = BuildSeededChain(SmallWindow, out var genesis);
        var active = ExtendActiveChain(chain, genesis, count: 1, merkleFill: 0xA1);
        var fork = ExtendForkChain(chain, genesis, count: 2, merkleFill: 0xB1);

        var detector = new ReorgDetector();
        var plan = detector.TryDetect(chain, fork[1]);

        Assert.NotNull(plan);
        Assert.False(plan!.IsDegraded);
        Assert.Equal(DisplayHex(genesis), plan.CommonAncestorHash);
        Assert.Equal(0, plan.CommonAncestorHeight);
        Assert.Equal(new[] { DisplayHex(active[0]) }, plan.OrphanedHashes);
        Assert.Equal(new[] { DisplayHex(fork[0]), DisplayHex(fork[1]) }, plan.NewChainHashes);
        Assert.Equal(DisplayHex(fork[1]), plan.NewTipHash);
        Assert.Equal(2, plan.NewTipHeight);
    }

    [Fact]
    public void Detect_TwoDeepFork_OrphansInDisconnectOrder_NewestFirst()
    {
        // active: genesis → A1 → A2 (height 2; tip)
        // fork:   genesis → B1 → B2 → B3 (height 3; longer)
        var chain = BuildSeededChain(SmallWindow, out var genesis);
        var active = ExtendActiveChain(chain, genesis, count: 2, merkleFill: 0xA2);
        var fork = ExtendForkChain(chain, genesis, count: 3, merkleFill: 0xB2);

        var detector = new ReorgDetector();
        var plan = detector.TryDetect(chain, fork[2]);

        Assert.NotNull(plan);
        Assert.False(plan!.IsDegraded);
        Assert.Equal(DisplayHex(genesis), plan.CommonAncestorHash);
        // Disconnect order: newest first → A2, then A1.
        Assert.Equal(new[] { DisplayHex(active[1]), DisplayHex(active[0]) }, plan.OrphanedHashes);
        // Connect order: oldest first.
        Assert.Equal(
            new[] { DisplayHex(fork[0]), DisplayHex(fork[1]), DisplayHex(fork[2]) },
            plan.NewChainHashes);
    }

    [Fact]
    public void Detect_FiveDeepFork_ReturnsPlan_WithFiveOrphans()
    {
        var chain = BuildSeededChain(retentionWindow: 30, out var genesis);
        var active = ExtendActiveChain(chain, genesis, count: 5, merkleFill: 0xA5);
        var fork = ExtendForkChain(chain, genesis, count: 6, merkleFill: 0xB5);

        var detector = new ReorgDetector();
        var plan = detector.TryDetect(chain, fork[5]);

        Assert.NotNull(plan);
        Assert.False(plan!.IsDegraded);
        Assert.Equal(5, plan.OrphanedHashes.Count);
        Assert.Equal(6, plan.NewChainHashes.Count);
        Assert.Equal(6, plan.NewTipHeight);
        Assert.Equal(DisplayHex(active[4]), plan.OrphanedHashes[0]); // newest active first
        Assert.Equal(DisplayHex(active[0]), plan.OrphanedHashes[4]);  // oldest active last
        Assert.Equal(DisplayHex(fork[0]), plan.NewChainHashes[0]);    // oldest fork first
        Assert.Equal(DisplayHex(fork[5]), plan.NewChainHashes[5]);    // newest fork last
    }

    /// <summary>
    /// Build a chain + fork + post-fork-active-advance such that the
    /// fork's deepest ancestor falls out of the retention window. The
    /// recipe (retention=3): build active to height 5, fork off height 3
    /// for 4 blocks (heights 4-7 fork-side), then advance the active
    /// chain to height 6 — this prunes height 3 and breaks the fork's
    /// walk-back to its common ancestor.
    /// </summary>
    private static (HeadersChain chain, BlockHeader forkTip, BlockHeader[] activeChain, BlockHeader[] forkChain)
        BuildBelowRetentionScenario()
    {
        var chain = new HeadersChain(new HeadersChainOptions { RetainedHeaderCount = 3 });
        var genesis = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x01);
        chain.Seed(genesis, height: 0);

        var activeChain = new BlockHeader[5];
        var p = genesis;
        for (var i = 0; i < 5; i++)
        {
            var h = HeaderTestUtil.BuildChild(p, merkleFill: 0xA0);
            Assert.IsType<ExtendResult.Extended>(chain.TryExtend(h));
            activeChain[i] = h;
            p = h;
        }
        // After the active build: retention=3, tipHeight=5, _byHash =
        // heights {3,4,5}. activeChain[2] is height 3 (still in window).

        var forkParent = activeChain[2];
        var forkChain = new BlockHeader[4];
        var fp = forkParent;
        for (var i = 0; i < 4; i++) // fork heights 4,5,6,7
        {
            var h = HeaderTestUtil.BuildChild(fp, merkleFill: 0xB0, timestamp: 1700000005);
            Assert.IsType<ExtendResult.Fork>(chain.TryExtend(h));
            forkChain[i] = h;
            fp = h;
        }
        // _byHash now has heights {3,4,5,4-fork,5-fork,6-fork,7-fork}.

        // Advance active by one block (height 6). cutoff becomes
        // 6-3+1=4; the active height-3 entry (the fork's common
        // ancestor) gets pruned. Fork's deepest ancestor by hash is
        // 4-fork, whose prev_block points at active height-3 — no
        // longer in the dict.
        var active6 = HeaderTestUtil.BuildChild(activeChain[4], merkleFill: 0xA0);
        Assert.IsType<ExtendResult.Extended>(chain.TryExtend(active6));
        var extendedActive = new BlockHeader[6];
        Array.Copy(activeChain, extendedActive, 5);
        extendedActive[5] = active6;

        return (chain, forkChain[3], extendedActive, forkChain);
    }

    [Fact]
    public void Detect_ForkPointBelowRetention_ReturnsDegraded()
    {
        var (chain, forkTip, _, _) = BuildBelowRetentionScenario();

        var detector = new ReorgDetector();
        var plan = detector.TryDetect(chain, forkTip);

        Assert.NotNull(plan);
        Assert.True(plan!.IsDegraded,
            $"expected degraded plan but got non-degraded "
            + $"(common ancestor was {plan.CommonAncestorHash} at height {plan.CommonAncestorHeight})");
        Assert.Empty(plan.OrphanedHashes);
        Assert.Empty(plan.NewChainHashes);
        Assert.Equal(string.Empty, plan.CommonAncestorHash);
    }

    [Fact]
    public void Detect_ForkTipHashUnknown_ReturnsNull()
    {
        // Caller hasn't fed the fork tip through TryExtend yet.
        var chain = BuildSeededChain(SmallWindow, out var genesis);
        ExtendActiveChain(chain, genesis, count: 3, merkleFill: 0xA1);
        var orphanHeader = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0xFF);

        var detector = new ReorgDetector();
        Assert.Null(detector.TryDetect(chain, orphanHeader));
    }

    [Fact]
    public void Detect_DegradedPlan_PreservesNewTipFields()
    {
        // Same setup as the "below retention" test but check that the
        // NewTip hash + height are populated in the degraded plan (the
        // emitter still wants them for the OnReorg DTO).
        var (chain, forkTip, _, _) = BuildBelowRetentionScenario();

        var plan = new ReorgDetector().TryDetect(chain, forkTip);

        Assert.NotNull(plan);
        Assert.True(plan!.IsDegraded);
        Assert.Equal(DisplayHex(forkTip), plan.NewTipHash);
        Assert.Equal(7, plan.NewTipHeight);
    }

    [Fact]
    public void Detect_ForkTipEqualsActiveTip_ReturnsNull()
    {
        var chain = BuildSeededChain(SmallWindow, out var genesis);
        ExtendActiveChain(chain, genesis, count: 2, merkleFill: 0xA1);

        var detector = new ReorgDetector();
        Assert.Null(detector.TryDetect(chain, chain.Tip!));
    }

    [Fact]
    public void Detect_AllHashesAreDisplayOrder_NotWireOrder()
    {
        // Pin Core Rule: ReorgPlan hashes are display-order
        // (BlockHeaderHasher.ToDisplayHex convention). Compare against
        // an independently-computed display hex to lock the byte order.
        var chain = BuildSeededChain(SmallWindow, out var genesis);
        var active = ExtendActiveChain(chain, genesis, count: 1, merkleFill: 0xA1);
        var fork = ExtendForkChain(chain, genesis, count: 2, merkleFill: 0xB1);

        var plan = new ReorgDetector().TryDetect(chain, fork[1]);
        Assert.NotNull(plan);

        var expectedNewTipDisplay = BlockHeaderHasher.ToDisplayHex(BlockHeaderHasher.Hash(fork[1]));
        Assert.Equal(expectedNewTipDisplay, plan!.NewTipHash);

        var expectedOrphanDisplay = BlockHeaderHasher.ToDisplayHex(BlockHeaderHasher.Hash(active[0]));
        Assert.Equal(expectedOrphanDisplay, plan.OrphanedHashes[0]);

        var expectedAncestorDisplay = BlockHeaderHasher.ToDisplayHex(BlockHeaderHasher.Hash(genesis));
        Assert.Equal(expectedAncestorDisplay, plan.CommonAncestorHash);
    }
}
