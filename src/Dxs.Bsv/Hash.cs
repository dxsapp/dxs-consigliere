using System;
using System.IO;
using System.Security.Cryptography;

using Org.BouncyCastle.Crypto.Digests;

namespace Dxs.Bsv;

public static class Hash
{
    public static byte[] Ripedm160(ReadOnlySpan<byte> bytes)
    {
        var digest = new RipeMD160Digest();
        var result = new byte[digest.GetDigestSize()];

        digest.BlockUpdate(bytes.ToArray(), 0, bytes.Length);
        digest.DoFinal(result, 0);

        return result;
    }

    public static byte[] Sha256(ReadOnlySpan<byte> bytes)
    {
        using var sha256 = SHA256.Create();

        Span<byte> result = stackalloc byte[32];
        if (sha256.TryComputeHash(bytes, result, out _))
            return result.ToArray();

        throw new Exception($"Unable to hash bytes: {Convert.ToHexString(bytes)}");
    }

    public static byte[] Sha256(Stream stream)
    {
        using var sha256 = SHA256.Create();

        return sha256.ComputeHash(stream);
    }

    public static byte[] Sha256Sha256(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash2 = stackalloc byte[32];

        Sha256Sha256(bytes, hash2);

        return hash2.ToArray();
    }

    public static void Sha256Sha256(ReadOnlySpan<byte> bytes, Span<byte> result)
    {
        if (result.Length != 32)
            throw new Exception("Result must be 32 length");

        using var sha256 = SHA256.Create();
        Span<byte> hash1 = stackalloc byte[32];

        if (!sha256.TryComputeHash(bytes, hash1, out _))
            throw new Exception($"Unable to get hash from {bytes.ToArray()}");

        sha256.TryComputeHash(hash1, result, out _);
    }

    public static byte[] Sha256Sha256Ripedm160(ReadOnlySpan<byte> bytes) => Ripedm160(Sha256(bytes));
}
