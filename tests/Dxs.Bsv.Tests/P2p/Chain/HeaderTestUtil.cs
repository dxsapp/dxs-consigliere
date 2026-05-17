using System;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;

namespace Dxs.Bsv.Tests.P2p.Chain;

/// <summary>
/// Test helper for fabricating BSV block headers with a chosen difficulty.
/// Defaults to bits=0x207fffff (regtest minimum) so any synthesized header
/// passes PoW — lets the linkage tests focus on TryExtend behavior without
/// mining real headers.
/// </summary>
internal static class HeaderTestUtil
{
    public const uint RegtestBits = 0x207fffffu;

    public static BlockHeader Build(
        byte[] prev,
        uint bits = RegtestBits,
        uint version = 1,
        uint timestamp = 1700000000,
        uint nonce = 0,
        byte merkleFill = 0xAA,
        bool searchForValidPow = true)
    {
        if (prev is null) throw new ArgumentNullException(nameof(prev));
        if (prev.Length != 32) throw new ArgumentException("prev must be 32 bytes", nameof(prev));
        var bytes = new byte[BlockHeader.Size];
        // version (4 LE)
        bytes[0] = (byte)(version & 0xff);
        bytes[1] = (byte)((version >> 8) & 0xff);
        bytes[2] = (byte)((version >> 16) & 0xff);
        bytes[3] = (byte)((version >> 24) & 0xff);
        // prev_block (32)
        Buffer.BlockCopy(prev, 0, bytes, 4, 32);
        // merkle_root (32) — fill so each header has distinct content
        for (var i = 36; i < 68; i++) bytes[i] = merkleFill;
        // timestamp (4 LE)
        bytes[68] = (byte)(timestamp & 0xff);
        bytes[69] = (byte)((timestamp >> 8) & 0xff);
        bytes[70] = (byte)((timestamp >> 16) & 0xff);
        bytes[71] = (byte)((timestamp >> 24) & 0xff);
        // bits (4 LE)
        bytes[72] = (byte)(bits & 0xff);
        bytes[73] = (byte)((bits >> 8) & 0xff);
        bytes[74] = (byte)((bits >> 16) & 0xff);
        bytes[75] = (byte)((bits >> 24) & 0xff);
        WriteNonce(bytes, nonce);

        if (!searchForValidPow) return new BlockHeader(bytes);

        // Regtest-bits target's high byte caps at 0x7f, so ~50% of random
        // hashes fail. Iterate nonce until PoW passes (usually < 10 tries).
        for (uint n = nonce; n < uint.MaxValue; n++)
        {
            WriteNonce(bytes, n);
            var header = new BlockHeader(bytes);
            if (BlockHeaderHasher.MeetsTarget(header)) return header;
        }
        throw new InvalidOperationException("Failed to find PoW-valid nonce in 2^32 attempts");
    }

    private static void WriteNonce(byte[] bytes, uint nonce)
    {
        bytes[76] = (byte)(nonce & 0xff);
        bytes[77] = (byte)((nonce >> 8) & 0xff);
        bytes[78] = (byte)((nonce >> 16) & 0xff);
        bytes[79] = (byte)((nonce >> 24) & 0xff);
    }

    /// <summary>Build a header that extends <paramref name="parent"/>.</summary>
    public static BlockHeader BuildChild(
        BlockHeader parent,
        uint bits = RegtestBits,
        uint timestamp = 1700000000,
        uint nonce = 0,
        byte merkleFill = 0xBB)
    {
        var parentHash = BlockHeaderHasher.Hash(parent);
        return Build(parentHash, bits, version: 1, timestamp, nonce, merkleFill);
    }
}
