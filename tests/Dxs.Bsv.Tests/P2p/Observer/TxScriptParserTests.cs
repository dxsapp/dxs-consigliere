using System;

using Dxs.Bsv;
using Dxs.Bsv.P2p.Observer;
using Dxs.Bsv.Script;

namespace Dxs.Bsv.Tests.P2p.Observer;

public class TxScriptParserTests
{
    /// <summary>
    /// Real mainnet P2PKH address — the one used during Gate-3
    /// end-to-end validation (see project memory). Hash160 below
    /// is base58check-decoded from the address string.
    /// </summary>
    private const string KnownAddress = "12UScFvnuA9FjeoapbRQ5YS963mgzrQkZo";

    private static byte[] BuildP2pkhOutput(byte[] hash160)
    {
        if (hash160.Length != 20) throw new ArgumentException("hash160 must be 20 bytes");
        var script = new byte[25];
        script[0] = (byte)OpCode.OP_DUP;
        script[1] = (byte)OpCode.OP_HASH160;
        script[2] = 0x14; // 20-byte push
        Buffer.BlockCopy(hash160, 0, script, 3, 20);
        script[23] = (byte)OpCode.OP_EQUALVERIFY;
        script[24] = (byte)OpCode.OP_CHECKSIG;
        return script;
    }

    private static byte[] BuildP2pkhScriptSig(byte[] signature, byte[] pubkey)
    {
        // <sig> <pubkey>: two pushes
        if (signature.Length > 0x4b) throw new ArgumentException("signature too long for direct push (test simplification)");
        if (pubkey.Length > 0x4b) throw new ArgumentException("pubkey too long for direct push (test simplification)");
        var script = new byte[1 + signature.Length + 1 + pubkey.Length];
        script[0] = (byte)signature.Length;
        Buffer.BlockCopy(signature, 0, script, 1, signature.Length);
        script[1 + signature.Length] = (byte)pubkey.Length;
        Buffer.BlockCopy(pubkey, 0, script, 2 + signature.Length, pubkey.Length);
        return script;
    }

    private static byte[] FakeSignature(int length)
    {
        // Canonical-looking DER signature length 71 (typical low-S 71-72).
        var sig = new byte[length];
        sig[0] = 0x30; sig[1] = (byte)(length - 2); sig[2] = 0x44;
        for (var i = 3; i < length; i++) sig[i] = (byte)(0xAB ^ i);
        return sig;
    }

    private static byte[] FakeCompressedPubkey()
    {
        // 33 bytes, leading 0x02 (compressed even-y marker).
        var pk = new byte[33];
        pk[0] = 0x02;
        for (var i = 1; i < 33; i++) pk[i] = (byte)(0x10 + i);
        return pk;
    }

    private static byte[] FakeUncompressedPubkey()
    {
        // 65 bytes, leading 0x04 (uncompressed marker).
        var pk = new byte[65];
        pk[0] = 0x04;
        for (var i = 1; i < 65; i++) pk[i] = (byte)(0x20 + i);
        return pk;
    }

    [Fact]
    public void TryParseP2pkhOutput_ValidScript_ExtractsHash160()
    {
        var addr = new Address(KnownAddress);
        Assert.Equal(20, addr.Hash160.Length);
        var script = BuildP2pkhOutput(addr.Hash160);

        var ok = TxScriptParser.TryParseP2pkhOutput(script, out var parsed);

        Assert.True(ok);
        Assert.Equal(addr.Hash160, parsed.ToArray());
    }

    [Fact]
    public void TryParseP2pkhOutput_WrongSize_ReturnsFalse()
    {
        Assert.False(TxScriptParser.TryParseP2pkhOutput(new byte[24], out _));
        Assert.False(TxScriptParser.TryParseP2pkhOutput(new byte[26], out _));
        Assert.False(TxScriptParser.TryParseP2pkhOutput(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void TryParseP2pkhOutput_WrongOpcodes_ReturnsFalse()
    {
        var addr = new Address(KnownAddress);
        var script = BuildP2pkhOutput(addr.Hash160);

        // Mutate the first byte (OP_DUP → 0x00) — should reject
        var tampered = (byte[])script.Clone();
        tampered[0] = 0x00;
        Assert.False(TxScriptParser.TryParseP2pkhOutput(tampered, out _));

        // Mutate the push-length byte (0x14 → 0x13)
        tampered = (byte[])script.Clone();
        tampered[2] = 0x13;
        Assert.False(TxScriptParser.TryParseP2pkhOutput(tampered, out _));

        // Mutate OP_CHECKSIG → OP_CHECKMULTISIG
        tampered = (byte[])script.Clone();
        tampered[24] = 0xae; // OP_CHECKMULTISIG
        Assert.False(TxScriptParser.TryParseP2pkhOutput(tampered, out _));
    }

    [Fact]
    public void TryParseP2pkhInputPubkey_CompressedPubkey_DerivesHash160()
    {
        var sig = FakeSignature(71);
        var pubkey = FakeCompressedPubkey();
        var scriptSig = BuildP2pkhScriptSig(sig, pubkey);

        var ok = TxScriptParser.TryParseP2pkhInputPubkey(scriptSig, out var hash160);

        Assert.True(ok);
        Assert.NotNull(hash160);
        Assert.Equal(20, hash160!.Length);
        // Cross-check against the canonical Address derivation from the same pubkey.
        var expected = Hash.Sha256Sha256Ripedm160(pubkey);
        Assert.Equal(expected, hash160);
    }

    [Fact]
    public void TryParseP2pkhInputPubkey_UncompressedPubkey_DerivesHash160()
    {
        var sig = FakeSignature(72);
        var pubkey = FakeUncompressedPubkey();
        var scriptSig = BuildP2pkhScriptSig(sig, pubkey);

        var ok = TxScriptParser.TryParseP2pkhInputPubkey(scriptSig, out var hash160);

        Assert.True(ok);
        Assert.NotNull(hash160);
        Assert.Equal(Hash.Sha256Sha256Ripedm160(pubkey), hash160);
    }

    [Fact]
    public void TryParseP2pkhInputPubkey_WrongPubkeySize_ReturnsFalse()
    {
        // 32-byte pubkey isn't canonical for secp256k1 P2PKH; should reject.
        var sig = FakeSignature(71);
        var pubkey = new byte[32]; pubkey[0] = 0x02;
        var scriptSig = BuildP2pkhScriptSig(sig, pubkey);

        Assert.False(TxScriptParser.TryParseP2pkhInputPubkey(scriptSig, out _));
    }

    [Fact]
    public void TryParseP2pkhInputPubkey_BadCompressedMarker_ReturnsFalse()
    {
        // 33-byte but marker not 0x02/0x03 → reject.
        var sig = FakeSignature(71);
        var pubkey = new byte[33]; pubkey[0] = 0x05; // not a real marker
        var scriptSig = BuildP2pkhScriptSig(sig, pubkey);

        Assert.False(TxScriptParser.TryParseP2pkhInputPubkey(scriptSig, out _));
    }

    [Fact]
    public void TryParseP2pkhInputPubkey_SinglePushOnly_ReturnsFalse()
    {
        // Only one push (no pubkey) → not a P2PKH unlocking script.
        var pubkey = FakeCompressedPubkey();
        var scriptSig = new byte[1 + pubkey.Length];
        scriptSig[0] = (byte)pubkey.Length;
        Buffer.BlockCopy(pubkey, 0, scriptSig, 1, pubkey.Length);

        Assert.False(TxScriptParser.TryParseP2pkhInputPubkey(scriptSig, out _));
    }

    [Fact]
    public void TryParseP2pkhInputPubkey_ThreePushes_ReturnsFalse()
    {
        // Multisig-like shape: <0> <sigA> <sigB> — three pushes → not standard P2PKH.
        var script = new byte[] { 0x01, 0x00, 0x01, 0xAA, 0x01, 0xBB };
        Assert.False(TxScriptParser.TryParseP2pkhInputPubkey(script, out _));
    }

    [Fact]
    public void TryParseP2pkhInputPubkey_Empty_ReturnsFalse()
    {
        Assert.False(TxScriptParser.TryParseP2pkhInputPubkey(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void TryParseP2pkhInputPubkey_Oversized_ReturnsFalse()
    {
        var oversize = new byte[TxScriptParser.MaxParseableScriptBytes + 1];
        Assert.False(TxScriptParser.TryParseP2pkhInputPubkey(oversize, out _));
    }

    [Fact]
    public void TryReadPush_DirectPush_ReadsPayload()
    {
        var script = new byte[] { 0x03, 0xAA, 0xBB, 0xCC, 0xFF }; // 3-byte push then OP_INVALIDOPCODE
        var ok = TxScriptParser.TryReadPush(script, 0, out var payload, out var next);
        Assert.True(ok);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, payload.ToArray());
        Assert.Equal(4, next);
    }

    [Fact]
    public void TryReadPush_Pushdata1_ReadsPayload()
    {
        // OP_PUSHDATA1 0x05 0xAA 0xBB 0xCC 0xDD 0xEE
        var script = new byte[] { 0x4c, 0x05, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE };
        var ok = TxScriptParser.TryReadPush(script, 0, out var payload, out var next);
        Assert.True(ok);
        Assert.Equal(5, payload.Length);
        Assert.Equal(7, next);
    }

    [Fact]
    public void TryReadPush_TruncatedPush_ReturnsFalse()
    {
        // Direct push claims 10 bytes but buffer only has 4 more.
        var script = new byte[] { 0x0a, 0x01, 0x02, 0x03, 0x04 };
        Assert.False(TxScriptParser.TryReadPush(script, 0, out _, out _));
    }

    [Fact]
    public void TryParseTokenId_NonTokenScript_ReturnsFalse()
    {
        var addr = new Address(KnownAddress);
        var script = BuildP2pkhOutput(addr.Hash160);

        var ok = TxScriptParser.TryParseTokenId(script, Network.Mainnet, out var tokenId);

        Assert.False(ok);
        Assert.Null(tokenId);
    }

    [Fact]
    public void TryParseTokenId_Empty_ReturnsFalse()
    {
        var ok = TxScriptParser.TryParseTokenId(ReadOnlySpan<byte>.Empty, Network.Mainnet, out var tokenId);
        Assert.False(ok);
        Assert.Null(tokenId);
    }

    [Fact]
    public void TryParseTokenId_Oversized_ReturnsFalse()
    {
        var oversize = new byte[TxScriptParser.MaxParseableScriptBytes + 1];
        Assert.False(TxScriptParser.TryParseTokenId(oversize, Network.Mainnet, out _));
    }

    [Fact]
    public void PrefixCollision_ReturnedHashIsFull20Bytes_MatcherWillRejectAtFullVerify()
    {
        // S1 returns the full Hash160; the matcher's 8-byte prefix
        // index can produce a collision but the full-hash verify
        // (S2) rejects. This test pins the parser-side contract: the
        // full 20 bytes are returned, so S2 has the data it needs.
        var addrA = new Address(KnownAddress);
        Assert.Equal(20, addrA.Hash160.Length);

        // Fabricate a hash160 that shares the first 8 bytes but
        // differs in the remaining 12. Watchlist will get a prefix
        // hit but full-hash mismatch → no match.
        var collidingHash = (byte[])addrA.Hash160.Clone();
        for (var i = 8; i < 20; i++) collidingHash[i] = (byte)(collidingHash[i] ^ 0xFF);
        Assert.False(addrA.Hash160.AsSpan().SequenceEqual(collidingHash));

        var script = BuildP2pkhOutput(collidingHash);
        Assert.True(TxScriptParser.TryParseP2pkhOutput(script, out var parsed));
        Assert.Equal(collidingHash, parsed.ToArray());
        // Crucial: parser returns the FULL 20 bytes (not just the prefix),
        // so S2 can do the full-hash verify that resolves the collision.
        Assert.Equal(20, parsed.Length);
    }
}
