using System;
using System.Collections.Generic;
using System.Linq;

using Dxs.Bsv.P2p.Observer;

namespace Dxs.Bsv.Tests.P2p.Observer;

public class WatchlistMatcherTests
{
    private static byte[] Hash(byte fill)
    {
        var h = new byte[20];
        for (var i = 0; i < 20; i++) h[i] = fill;
        return h;
    }

    private static byte[] CollidingPrefix(byte[] basis, byte differAtIndex)
    {
        // Share the first 8 bytes; differ in the trailing 12.
        if (basis.Length != 20) throw new ArgumentException("basis must be 20 bytes");
        var clone = (byte[])basis.Clone();
        clone[differAtIndex] ^= 0xFF;
        return clone;
    }

    private static ParsedTx Tx(
        string txid = "tx-1",
        IReadOnlyList<byte[]>? outputs = null,
        IReadOnlyList<byte[]>? inputs = null,
        IReadOnlyList<string>? tokens = null)
        => new(
            txid,
            outputs ?? Array.Empty<byte[]>(),
            inputs ?? Array.Empty<byte[]>(),
            tokens ?? Array.Empty<string>());

    [Fact]
    public void Empty_Matcher_ReturnsNone()
    {
        var m = new WatchlistMatcher();
        var result = m.Match(Tx(outputs: new[] { Hash(0xAA) }));
        Assert.Same(MatchResult.None.Instance, result);
        Assert.False(m.IsLoaded);
        Assert.False(m.HasAnyTokens);
    }

    [Fact]
    public void AddAddress_OutputHit_ReturnsAddressHit()
    {
        var m = new WatchlistMatcher();
        var h = Hash(0xAB);
        m.AddAddress(h);

        var result = m.Match(Tx(outputs: new[] { h }));

        var hit = Assert.IsType<MatchResult.AddressHit>(result);
        Assert.Single(hit.Hash160s);
        Assert.Equal(h, hit.Hash160s[0]);
    }

    [Fact]
    public void AddAddress_InputHit_ReturnsAddressHit()
    {
        var m = new WatchlistMatcher();
        var h = Hash(0xCD);
        m.AddAddress(h);

        var result = m.Match(Tx(inputs: new[] { h }));

        var hit = Assert.IsType<MatchResult.AddressHit>(result);
        Assert.Single(hit.Hash160s);
    }

    [Fact]
    public void RemoveAddress_ThenMatch_ReturnsNone()
    {
        var m = new WatchlistMatcher();
        var h = Hash(0xEF);
        m.AddAddress(h);
        Assert.Equal(1, m.WatchedAddressCount);

        m.RemoveAddress(h);
        Assert.Equal(0, m.WatchedAddressCount);

        var result = m.Match(Tx(outputs: new[] { h }));
        Assert.Same(MatchResult.None.Instance, result);
    }

    [Fact]
    public void PrefixCollision_OnlyOneWatched_OtherReturnsNone()
    {
        // Audit W2 S2 fixture suite: two hash160s with same 8-byte
        // prefix; only one is watched. The unwatched one must NOT
        // be reported as a match (full-hash verify).
        var watched = Hash(0x55);
        var collider = CollidingPrefix(watched, differAtIndex: 12);
        Assert.Equal(watched[..8], collider[..8]);
        Assert.NotEqual(watched, collider);

        var m = new WatchlistMatcher();
        m.AddAddress(watched);

        // Watched still matches.
        Assert.IsType<MatchResult.AddressHit>(m.Match(Tx(outputs: new[] { watched })));

        // Colliding (unwatched) does NOT match — full-hash verify rejects.
        Assert.Same(MatchResult.None.Instance, m.Match(Tx(outputs: new[] { collider })));
    }

    [Fact]
    public void PrefixCollision_BothWatched_BothMatch()
    {
        var first = Hash(0x55);
        var second = CollidingPrefix(first, differAtIndex: 12);

        var m = new WatchlistMatcher();
        m.AddAddress(first);
        m.AddAddress(second);

        Assert.IsType<MatchResult.AddressHit>(m.Match(Tx(outputs: new[] { first })));
        Assert.IsType<MatchResult.AddressHit>(m.Match(Tx(outputs: new[] { second })));
        Assert.Equal(2, m.WatchedAddressCount);
    }

    [Fact]
    public void AddToken_OutputHit_ReturnsTokenHit()
    {
        var m = new WatchlistMatcher();
        m.AddToken("token-abc");

        Assert.True(m.HasAnyTokens);

        var result = m.Match(Tx(tokens: new[] { "token-abc" }));
        var hit = Assert.IsType<MatchResult.TokenHit>(result);
        Assert.Single(hit.TokenIds);
        Assert.Equal("token-abc", hit.TokenIds[0]);
    }

    [Fact]
    public void RemoveToken_ThenMatch_ReturnsNone()
    {
        var m = new WatchlistMatcher();
        m.AddToken("token-xyz");
        m.RemoveToken("token-xyz");

        Assert.False(m.HasAnyTokens);
        Assert.Same(MatchResult.None.Instance, m.Match(Tx(tokens: new[] { "token-xyz" })));
    }

    [Fact]
    public void AddressHit_AndTokenHit_OnSameTx_ReturnsBoth()
    {
        var h = Hash(0x99);
        var m = new WatchlistMatcher();
        m.AddAddress(h);
        m.AddToken("t1");

        var result = m.Match(Tx(outputs: new[] { h }, tokens: new[] { "t1" }));
        var both = Assert.IsType<MatchResult.Both>(result);
        Assert.Single(both.Hash160s);
        Assert.Single(both.TokenIds);
    }

    [Fact]
    public void Add_Idempotent_OnDuplicateAddress()
    {
        var h = Hash(0x77);
        var m = new WatchlistMatcher();
        m.AddAddress(h);
        m.AddAddress(h);
        m.AddAddress(h);
        Assert.Equal(1, m.WatchedAddressCount);
    }

    [Fact]
    public void Add_RejectsWrongSizeHash()
    {
        var m = new WatchlistMatcher();
        Assert.Throws<ArgumentException>(() => m.AddAddress(new byte[19]));
        Assert.Throws<ArgumentException>(() => m.AddAddress(new byte[21]));
    }

    [Fact]
    public void Match_TenThousandAddresses_NoFalsePositives()
    {
        // Audit W2 S2 large-addset case. 10K random hash160; only
        // half watched. Match scans 100 tx (mix of watched / unwatched);
        // no false positive should escape the prefix index.
        var rng = new Random(1337);
        var addresses = new byte[10_000][];
        for (var i = 0; i < 10_000; i++)
        {
            addresses[i] = new byte[20];
            rng.NextBytes(addresses[i]);
        }
        var m = new WatchlistMatcher();
        for (var i = 0; i < 5_000; i++) m.AddAddress(addresses[i]);

        // 50 watched txes
        for (var i = 0; i < 50; i++)
        {
            var result = m.Match(Tx(outputs: new[] { addresses[i] }));
            Assert.IsType<MatchResult.AddressHit>(result);
        }
        // 50 unwatched txes
        for (var i = 5_000; i < 5_050; i++)
        {
            var result = m.Match(Tx(outputs: new[] { addresses[i] }));
            Assert.Same(MatchResult.None.Instance, result);
        }
    }

    [Fact]
    public void MarkLoaded_FlipsIsLoaded()
    {
        var m = new WatchlistMatcher();
        Assert.False(m.IsLoaded);
        m.MarkLoaded();
        Assert.True(m.IsLoaded);
        // Stays true even after deltas.
        m.AddAddress(Hash(0x01));
        m.RemoveAddress(Hash(0x01));
        Assert.True(m.IsLoaded);
    }

    [Fact]
    public void Match_NoHit_ReturnsNoneInstance_NotAllocation()
    {
        // Documents the allocation-free hot path: the None result
        // should be the static singleton on a no-hit, so the GC
        // sees no churn under steady-state miss traffic.
        var m = new WatchlistMatcher();
        var r1 = m.Match(Tx(outputs: new[] { Hash(0x11) }));
        var r2 = m.Match(Tx(outputs: new[] { Hash(0x22) }));
        Assert.Same(r1, r2);
        Assert.Same(MatchResult.None.Instance, r1);
    }
}
