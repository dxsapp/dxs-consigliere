using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace Dxs.Bsv;

public static class BinaryHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte HexToByte(byte left, byte right)
    {
        return (byte)(((uint)left << 4) | right);
    }

    public static byte[] StreamToBytes(Stream stream)
    {
        var result = new byte[stream.Length / 2];
        var i = 0;
        while (stream.Position < stream.Length)
        {
            var left = IsDigit(stream.ReadByte());
            var right = IsDigit(stream.ReadByte());

            result[i] = (byte)(((uint)left << 4) | (uint)right);
            i++;
        }

        return result;
    }

    public static byte[] Reverse(ReadOnlySpan<byte> bytes)
    {
        return bytes.ToArray().Reverse();
    }
        
    public static byte[] Reverse(this byte[] bytes)
    {
        var mIdx = bytes.Length - 1;
        for (var i = 0; i < bytes.Length / 2; i++)
            (bytes[i], bytes[mIdx - i]) = (bytes[mIdx - i], bytes[i]);

        return bytes;
    }

    public static byte[] FromHexString(this string hex) => Convert.FromHexString(hex);

    public static string ToHexString(this ReadOnlySpan<byte> bytes) => ToHexStringPrivate(bytes);
    public static string ToHexString(this byte[] bytes) => ToHexStringPrivate(bytes);

    private static string ToHexStringPrivate(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;
        if (bytes.Length > int.MaxValue / 2)
            throw new ArgumentOutOfRangeException(nameof(bytes), "Input is too large");

        return HexConverter.ToString(bytes, HexConverter.Casing.Lower);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IsDigit(char c)
    {
        return c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IsDigit(int b)
    {
        return b switch
        {
            >= 48 and <= 57 => b - 48,
            >= 97 and <= 102 => b - 97 + 10,
            >= 65 and <= 70 => b - 65 + 10,
            _ => -1
        };
    }

    // TODO Refactor
    public static byte[][] Split(byte[] source, byte[] separator)
    {
        var segments = new List<byte[]>();
        var idx = 0;
        byte[] segment;

        for (var i = 0; i < source.Length; ++i)
        {
            if (!Equals(source, separator, i)) continue;

            segment = new byte[i - idx];
            Array.Copy(source, idx, segment, 0, segment.Length);
            segments.Add(segment);
            idx = i + separator.Length;
            i += separator.Length - 1;
        }

        segment = new byte[source.Length - idx];
        Array.Copy(source, idx, segment, 0, segment.Length);
        segments.Add(segment);

        return segments.ToArray();
    }

    public static bool Equals(byte[] source, byte[] separator, int index)
    {
        for (var i = 0; i < separator.Length; ++i)
        {
            if (index + i >= source.Length || source[index + i] != separator[i])
                return false;
        }

        return true;
    }
}