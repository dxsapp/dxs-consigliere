using System;
using System.Linq;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;

namespace Dxs.Bsv.Tests.P2p.Chain;

public class HeadersChainTests
{
    [Fact]
    public void Empty_FirstHeader_BecomesTip_AtHeightZero()
    {
        var chain = new HeadersChain();
        var h0 = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x11);

        var result = chain.TryExtend(h0);

        var ext = Assert.IsType<ExtendResult.Extended>(result);
        Assert.Equal(0, ext.Height);
        Assert.Same(h0, chain.Tip);
        Assert.Equal(0, chain.TipHeight);
        Assert.Equal(1, chain.Count);
    }

    [Fact]
    public void ExtendsTip_WithValidChild_AdvancesHeight()
    {
        var chain = new HeadersChain();
        var h0 = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x11);
        chain.TryExtend(h0);

        var h1 = HeaderTestUtil.BuildChild(h0, merkleFill: 0x22);
        var result = chain.TryExtend(h1);

        var ext = Assert.IsType<ExtendResult.Extended>(result);
        Assert.Equal(1, ext.Height);
        Assert.Same(h1, chain.Tip);
        Assert.Equal(1, chain.TipHeight);
    }

    [Fact]
    public void DuplicateOfTip_ReturnsAlreadyKnown()
    {
        var chain = new HeadersChain();
        var h0 = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x11);
        chain.TryExtend(h0);

        var result = chain.TryExtend(h0);

        var ak = Assert.IsType<ExtendResult.AlreadyKnown>(result);
        Assert.Equal(0, ak.Height);
        Assert.Equal(1, chain.Count);
    }

    [Fact]
    public void DuplicateOfAncestor_ReturnsAlreadyKnown()
    {
        var chain = new HeadersChain();
        var h0 = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x11);
        var h1 = HeaderTestUtil.BuildChild(h0, merkleFill: 0x22);
        chain.TryExtend(h0);
        chain.TryExtend(h1);

        // Feed h0 again — it is now a non-tip ancestor.
        var result = chain.TryExtend(h0);
        var ak = Assert.IsType<ExtendResult.AlreadyKnown>(result);
        Assert.Equal(0, ak.Height);
        Assert.Equal(2, chain.Count);
    }

    [Fact]
    public void ForkOnAncestor_StoresFork_DoesNotPromoteTip()
    {
        var chain = new HeadersChain();
        var h0 = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x11);
        var h1 = HeaderTestUtil.BuildChild(h0, merkleFill: 0x22);
        chain.TryExtend(h0);
        chain.TryExtend(h1);

        // Fork at height 0 → competing tip candidate at height 1, different merkle.
        var h1Fork = HeaderTestUtil.BuildChild(h0, merkleFill: 0x33);
        var result = chain.TryExtend(h1Fork);

        var fork = Assert.IsType<ExtendResult.Fork>(result);
        Assert.Equal(0, fork.ParentHeight);
        // Tip is unchanged — still the original h1, recovery is W3's job.
        Assert.Same(h1, chain.Tip);
        Assert.Equal(1, chain.TipHeight);
        Assert.Equal(3, chain.Count);
    }

    [Fact]
    public void Orphan_PrevUnknown_ReturnsOrphanWithMissingHash()
    {
        var chain = new HeadersChain();
        var h0 = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x11);
        chain.TryExtend(h0);

        // Header whose parent is a completely unrelated hash.
        var unrelated = new byte[32];
        for (var i = 0; i < 32; i++) unrelated[i] = (byte)i;
        var orphan = HeaderTestUtil.Build(prev: unrelated, merkleFill: 0x99);

        var result = chain.TryExtend(orphan);
        var orph = Assert.IsType<ExtendResult.Orphan>(result);
        Assert.Equal(unrelated, orph.MissingParentHash);
        // Chain unchanged.
        Assert.Same(h0, chain.Tip);
        Assert.Equal(1, chain.Count);
    }

    [Fact]
    public void Invalid_BadSize_ReturnsInvalid()
    {
        var chain = new HeadersChain();
        var malformed = new BlockHeader(new byte[BlockHeader.Size - 1]);
        var result = chain.TryExtend(malformed);
        Assert.IsType<ExtendResult.Invalid>(result);
    }

    [Fact]
    public void Invalid_PowFails_ReturnsInvalid()
    {
        var chain = new HeadersChain();
        // Use mainnet difficulty (0x1d00ffff) with arbitrary content and
        // suppress nonce-search — vanishingly unlikely to satisfy target.
        var hard = HeaderTestUtil.Build(
            prev: new byte[32],
            bits: 0x1d00ffffu,
            merkleFill: 0x42,
            searchForValidPow: false);
        var result = chain.TryExtend(hard);
        var inv = Assert.IsType<ExtendResult.Invalid>(result);
        Assert.Contains("PoW", inv.Reason);
    }

    [Fact]
    public void LoadFromStore_PopulatesState_WithoutRevalidating()
    {
        // Pretend Raven persisted three headers whose link-relationship
        // we don't bother to set up — LoadFromStore just trusts the input
        // and picks the highest-height header as tip.
        var fakeBytes = new byte[BlockHeader.Size];
        var h0 = new BlockHeader((byte[])fakeBytes.Clone()); fakeBytes[40] = 1; // distinguish
        var h1 = new BlockHeader((byte[])fakeBytes.Clone()); fakeBytes[40] = 2;
        var h2 = new BlockHeader((byte[])fakeBytes.Clone());

        var chain = new HeadersChain();
        chain.LoadFromStore(new[] { (h0, 100L), (h1, 101L), (h2, 102L) });

        Assert.True(chain.IsLoaded);
        Assert.Equal(102, chain.TipHeight);
        Assert.Same(h2, chain.Tip);
        Assert.Equal(3, chain.Count);
    }

    [Fact]
    public void RecentHeaders_ReturnsTipFirst_InHeightDescendingOrder()
    {
        var chain = new HeadersChain();
        var h0 = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x11);
        var h1 = HeaderTestUtil.BuildChild(h0, merkleFill: 0x22);
        var h2 = HeaderTestUtil.BuildChild(h1, merkleFill: 0x33);
        chain.TryExtend(h0);
        chain.TryExtend(h1);
        chain.TryExtend(h2);

        var recent = chain.RecentHeaders(10);
        Assert.Equal(3, recent.Count);
        Assert.Equal(2, recent[0].Height);
        Assert.Equal(1, recent[1].Height);
        Assert.Equal(0, recent[2].Height);

        var topTwo = chain.RecentHeaders(2);
        Assert.Equal(2, topTwo.Count);
        Assert.Equal(2, topTwo[0].Height);

        Assert.Empty(chain.RecentHeaders(0));
    }

    [Fact]
    public void Prune_KeepsOnlyRetainedCount_AnchoredOnTip()
    {
        // Retain only 3 headers; build a chain of 5.
        var chain = new HeadersChain(new HeadersChainOptions { RetainedHeaderCount = 3 });
        var headers = new BlockHeader[5];
        headers[0] = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x10);
        chain.TryExtend(headers[0]);
        for (var i = 1; i < 5; i++)
        {
            headers[i] = HeaderTestUtil.BuildChild(headers[i - 1], merkleFill: (byte)(0x10 + i));
            chain.TryExtend(headers[i]);
        }

        Assert.Equal(3, chain.Count);
        Assert.Equal(4, chain.TipHeight);
        var recent = chain.RecentHeaders(10).Select(x => x.Height).ToArray();
        Assert.Equal(new long[] { 4, 3, 2 }, recent);
    }
}
