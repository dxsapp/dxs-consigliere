using System;
using System.Linq;

using Dxs.Bsv;
using Dxs.Bsv.P2p.Observer;
using Dxs.Bsv.Script;

namespace Dxs.Bsv.Tests.P2p.Observer;

/// <summary>
/// Wave 2 S7 — end-to-end watchlist correctness fixture suite.
/// Integrates the S1 <see cref="TxScriptParser"/> + S2
/// <see cref="WatchlistMatcher"/> across the scenarios called out
/// in slices.md §S7. Tests use real BSV addresses (deterministic
/// hash160 derivation) and synthesised scripts so they're fully
/// portable across CI / dev hosts.
/// </summary>
public class WatchlistFixtureSuiteTests
{
    private const string AddressA = "12UScFvnuA9FjeoapbRQ5YS963mgzrQkZo";
    private const string AddressB = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";

    private static byte[] P2pkhOutput(byte[] hash160)
    {
        var s = new byte[25];
        s[0] = (byte)OpCode.OP_DUP;
        s[1] = (byte)OpCode.OP_HASH160;
        s[2] = 0x14;
        Buffer.BlockCopy(hash160, 0, s, 3, 20);
        s[23] = (byte)OpCode.OP_EQUALVERIFY;
        s[24] = (byte)OpCode.OP_CHECKSIG;
        return s;
    }

    private static byte[] P2pkhScriptSig(byte[] signature, byte[] pubkey)
    {
        var s = new byte[1 + signature.Length + 1 + pubkey.Length];
        s[0] = (byte)signature.Length;
        Buffer.BlockCopy(signature, 0, s, 1, signature.Length);
        s[1 + signature.Length] = (byte)pubkey.Length;
        Buffer.BlockCopy(pubkey, 0, s, 2 + signature.Length, pubkey.Length);
        return s;
    }

    private static byte[] FakeSig(int n)
    {
        var sig = new byte[n];
        sig[0] = 0x30; sig[1] = (byte)(n - 2); sig[2] = 0x44;
        for (var i = 3; i < n; i++) sig[i] = (byte)(0xAB ^ i);
        return sig;
    }

    private static byte[] CompressedPubkey(byte tag = 0x02)
    {
        var p = new byte[33]; p[0] = tag;
        for (var i = 1; i < 33; i++) p[i] = (byte)(0x10 + i);
        return p;
    }

    [Fact]
    public void Fixture_AddressOutput_PaysWatchedAddress_Matches()
    {
        // Slice §S7: tx pays a watched address → match
        var watched = new Address(AddressA);
        var matcher = new WatchlistMatcher();
        matcher.AddAddress(watched.Hash160);

        var script = P2pkhOutput(watched.Hash160);
        Assert.True(TxScriptParser.TryParseP2pkhOutput(script, out var parsed));

        var ptx = new ParsedTx(
            TxId: "fixture-output",
            OutputHash160s: new[] { parsed.ToArray() },
            InputPayerHash160s: Array.Empty<byte[]>(),
            OutputTokenIds: Array.Empty<string>());

        var result = matcher.Match(ptx);
        var hit = Assert.IsType<MatchResult.AddressHit>(result);
        Assert.Equal(watched.Hash160, hit.Hash160s[0]);
    }

    [Fact]
    public void Fixture_AddressInput_SpendsFromWatched_Matches()
    {
        // Slice §S7: tx spends a UTXO whose scriptSig reveals the
        // watched address as payer → match
        var pubkey = CompressedPubkey();
        var watchedHash = Hash.Sha256Sha256Ripedm160(pubkey);

        var matcher = new WatchlistMatcher();
        matcher.AddAddress(watchedHash);

        var scriptSig = P2pkhScriptSig(FakeSig(71), pubkey);
        Assert.True(TxScriptParser.TryParseP2pkhInputPubkey(scriptSig, out var parsedHash));

        var ptx = new ParsedTx(
            TxId: "fixture-input",
            OutputHash160s: Array.Empty<byte[]>(),
            InputPayerHash160s: new[] { parsedHash! },
            OutputTokenIds: Array.Empty<string>());

        var result = matcher.Match(ptx);
        var hit = Assert.IsType<MatchResult.AddressHit>(result);
        Assert.Equal(watchedHash, hit.Hash160s[0]);
    }

    [Fact]
    public void Fixture_TokenOutput_MatchesWatchedToken()
    {
        const string tokenId = "abcdef0123456789";
        var matcher = new WatchlistMatcher();
        matcher.AddToken(tokenId);

        var ptx = new ParsedTx(
            TxId: "fixture-token",
            OutputHash160s: Array.Empty<byte[]>(),
            InputPayerHash160s: Array.Empty<byte[]>(),
            OutputTokenIds: new[] { tokenId });

        var hit = Assert.IsType<MatchResult.TokenHit>(matcher.Match(ptx));
        Assert.Equal(tokenId, hit.TokenIds[0]);
    }

    [Fact]
    public void Fixture_DeleteDuringObservation_NoFalseMatch()
    {
        // Slice §S7: address watched, observed, then removed
        // mid-stream → next tx with that address returns None.
        var watched = new Address(AddressA);
        var matcher = new WatchlistMatcher();
        matcher.AddAddress(watched.Hash160);

        var ptx = new ParsedTx(
            TxId: "tx-1",
            OutputHash160s: new[] { watched.Hash160 },
            InputPayerHash160s: Array.Empty<byte[]>(),
            OutputTokenIds: Array.Empty<string>());

        Assert.IsType<MatchResult.AddressHit>(matcher.Match(ptx));

        // Remove the address — subsequent tx with the same output
        // must NOT match.
        matcher.RemoveAddress(watched.Hash160);
        Assert.Same(MatchResult.None.Instance, matcher.Match(ptx));
    }

    [Fact]
    public void Fixture_PrefixCollision_OnlyExactWatchedMatches()
    {
        // Slice §S7: two distinct hash160s sharing the 8-byte
        // prefix; exactly one is watched; the unwatched tx returns
        // None despite the prefix hit.
        var watched = new Address(AddressA).Hash160;
        var collider = (byte[])watched.Clone();
        for (var i = 8; i < 20; i++) collider[i] ^= 0xFF;
        Assert.Equal(watched[..8], collider[..8]);
        Assert.NotEqual(watched, collider);

        var matcher = new WatchlistMatcher();
        matcher.AddAddress(watched);

        var hit = new ParsedTx("hit", new[] { watched },
            Array.Empty<byte[]>(), Array.Empty<string>());
        var miss = new ParsedTx("miss", new[] { collider },
            Array.Empty<byte[]>(), Array.Empty<string>());

        Assert.IsType<MatchResult.AddressHit>(matcher.Match(hit));
        Assert.Same(MatchResult.None.Instance, matcher.Match(miss));
    }

    [Fact]
    public void Fixture_TwoWatchedAddresses_OnSameTx_BothReported()
    {
        var a = new Address(AddressA);
        var b = new Address(AddressB);
        var matcher = new WatchlistMatcher();
        matcher.AddAddress(a.Hash160);
        matcher.AddAddress(b.Hash160);

        var ptx = new ParsedTx("multi", new[] { a.Hash160, b.Hash160 },
            Array.Empty<byte[]>(), Array.Empty<string>());

        var hit = Assert.IsType<MatchResult.AddressHit>(matcher.Match(ptx));
        Assert.Equal(2, hit.Hash160s.Count);
        Assert.Contains(a.Hash160, hit.Hash160s);
        Assert.Contains(b.Hash160, hit.Hash160s);
    }

    [Fact]
    public void Fixture_AddressAndTokenOnSameTx_ReturnsBoth()
    {
        var a = new Address(AddressA);
        const string tokenId = "fb-token-id";
        var matcher = new WatchlistMatcher();
        matcher.AddAddress(a.Hash160);
        matcher.AddToken(tokenId);

        var ptx = new ParsedTx("dual",
            new[] { a.Hash160 },
            Array.Empty<byte[]>(),
            new[] { tokenId });

        var both = Assert.IsType<MatchResult.Both>(matcher.Match(ptx));
        Assert.Single(both.Hash160s);
        Assert.Single(both.TokenIds);
    }
}
