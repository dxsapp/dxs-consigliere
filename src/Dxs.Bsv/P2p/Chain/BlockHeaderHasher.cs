#nullable enable
using System;

using Dxs.Bsv.P2p.Messages;

namespace Dxs.Bsv.P2p.Chain;

/// <summary>
/// Computes the canonical BSV block hash: double-SHA-256 of the 80-byte
/// header. The output is in wire (little-endian) order, which is also the
/// byte order used for `prev_block` linking inside subsequent headers.
/// Display order (the hex strings shown by block explorers) is the
/// reverse of this.
/// </summary>
public static class BlockHeaderHasher
{
    /// <summary>Computes double-SHA-256 over the 80 header bytes (wire-order output).</summary>
    public static byte[] Hash(BlockHeader header)
    {
        if (header is null) throw new ArgumentNullException(nameof(header));
        if (header.Bytes80.Length != BlockHeader.Size)
            throw new ArgumentException($"BlockHeader must be {BlockHeader.Size} bytes", nameof(header));
        return Dxs.Bsv.Hash.Sha256Sha256(header.Bytes80);
    }

    /// <summary>
    /// The 32-byte `prev_block` field embedded inside the header at bytes
    /// 4..36. This is the wire-order hash of the parent block, directly
    /// comparable against <see cref="Hash"/> of the parent.
    /// </summary>
    public static ReadOnlySpan<byte> PrevBlock(BlockHeader header)
    {
        if (header.Bytes80.Length != BlockHeader.Size)
            throw new ArgumentException($"BlockHeader must be {BlockHeader.Size} bytes", nameof(header));
        return new ReadOnlySpan<byte>(header.Bytes80, 4, 32);
    }

    /// <summary>
    /// Returns the 4-byte compact-bits difficulty target from the header
    /// (bytes 72..76, little-endian).
    /// </summary>
    public static uint Bits(BlockHeader header) =>
        (uint)(header.Bytes80[72] | (header.Bytes80[73] << 8) | (header.Bytes80[74] << 16) | (header.Bytes80[75] << 24));

    /// <summary>
    /// The block header timestamp in unix seconds (bytes 68..72,
    /// little-endian).
    /// </summary>
    public static uint TimestampUnixSeconds(BlockHeader header) =>
        (uint)(header.Bytes80[68] | (header.Bytes80[69] << 8) | (header.Bytes80[70] << 16) | (header.Bytes80[71] << 24));

    /// <summary>
    /// Checks the proof-of-work commitment: header hash, interpreted as a
    /// little-endian 256-bit integer, must be less-than-or-equal to the
    /// target encoded by the compact `bits` field.
    /// Returns false on hash > target or malformed bits (mantissa with
    /// the sign bit set, or zero exponent and non-zero mantissa).
    /// </summary>
    public static bool MeetsTarget(BlockHeader header)
    {
        var hash = Hash(header);
        var bits = Bits(header);
        if (!TryExpandTarget(bits, out var target)) return false;
        // hash is wire-order (little-endian). Compare as 256-bit numbers:
        // start from the most-significant byte (last byte of the wire-order array).
        for (var i = 31; i >= 0; i--)
        {
            if (hash[i] < target[i]) return true;
            if (hash[i] > target[i]) return false;
        }
        return true; // equal
    }

    /// <summary>
    /// Expand a compact 4-byte target into a 32-byte little-endian target.
    /// Returns false if the encoding is invalid per Bitcoin consensus
    /// (sign-bit set on the mantissa, or zero mantissa).
    /// </summary>
    public static bool TryExpandTarget(uint compact, out byte[] target)
    {
        target = new byte[32];
        var exponent = (int)((compact >> 24) & 0xff);
        var mantissa = compact & 0x007fffffu;
        var signBit = (compact & 0x00800000u) != 0;
        if (signBit) return false;
        if (mantissa == 0) return false;

        // mantissa occupies the top 3 bytes (big-endian) at position
        // (exponent - 3); pad below with zeros. We write to the wire-order
        // (little-endian) buffer.
        if (exponent <= 3)
        {
            // mantissa >> (8 * (3 - exponent)) fits in low byte(s)
            var shifted = mantissa >> (8 * (3 - exponent));
            target[0] = (byte)(shifted & 0xff);
            target[1] = (byte)((shifted >> 8) & 0xff);
            target[2] = (byte)((shifted >> 16) & 0xff);
            return true;
        }

        var byteOffset = exponent - 3; // mantissa's low byte position in big-endian
        if (byteOffset + 3 > 32) return false; // overflow
        // Write in wire-order (LE): position `i` in LE == position `31 - i` in BE.
        // Big-endian: target[BE pos = 32-1-byteOffset]   = mantissa & 0xff   (low)
        //             target[BE pos = 32-1-byteOffset-1] = (mantissa >> 8)
        //             target[BE pos = 32-1-byteOffset-2] = (mantissa >> 16) (high)
        // Convert each BE pos to LE pos = 32-1-BE.
        target[byteOffset + 0] = (byte)(mantissa & 0xff);
        target[byteOffset + 1] = (byte)((mantissa >> 8) & 0xff);
        target[byteOffset + 2] = (byte)((mantissa >> 16) & 0xff);
        return true;
    }
}
