using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Dxs.Bsv.P2p.Codec;

/// <summary>
/// Append-only writer for Bitcoin P2P payloads. Wraps a growable
/// <see cref="MemoryStream"/> so callers do not need to pre-compute the
/// payload size. All multi-byte integers are little-endian unless suffixed
/// with <c>Be</c>.
/// </summary>
public sealed class P2pWriter
{
    private readonly MemoryStream _stream;

    public P2pWriter(int initialCapacity = 256)
    {
        _stream = new MemoryStream(initialCapacity);
    }

    public int Position => (int)_stream.Position;

    public void WriteByte(byte value)
    {
        _stream.WriteByte(value);
    }

    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        _stream.Write(bytes);
    }

    public void WriteInt16Le(short value)
    {
        Span<byte> buf = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16LittleEndian(buf, value);
        _stream.Write(buf);
    }

    public void WriteUInt16Le(ushort value)
    {
        Span<byte> buf = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, value);
        _stream.Write(buf);
    }

    public void WriteUInt16Be(ushort value)
    {
        Span<byte> buf = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(buf, value);
        _stream.Write(buf);
    }

    public void WriteInt32Le(int value)
    {
        Span<byte> buf = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buf, value);
        _stream.Write(buf);
    }

    public void WriteUInt32Le(uint value)
    {
        Span<byte> buf = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
        _stream.Write(buf);
    }

    public void WriteInt64Le(long value)
    {
        Span<byte> buf = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buf, value);
        _stream.Write(buf);
    }

    public void WriteUInt64Le(ulong value)
    {
        Span<byte> buf = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buf, value);
        _stream.Write(buf);
    }

    /// <summary>
    /// CompactSize / VarInt encoding used by the Bitcoin P2P protocol.
    /// </summary>
    public void WriteVarInt(ulong value)
    {
        if (value < 0xFD)
        {
            WriteByte((byte)value);
            return;
        }
        if (value <= 0xFFFF)
        {
            WriteByte(0xFD);
            WriteUInt16Le((ushort)value);
            return;
        }
        if (value <= 0xFFFFFFFFu)
        {
            WriteByte(0xFE);
            WriteUInt32Le((uint)value);
            return;
        }
        WriteByte(0xFF);
        WriteUInt64Le(value);
    }

    /// <summary>
    /// var_bytes: CompactSize length prefix + raw bytes.
    /// Used for association ID, etc.
    /// </summary>
    public void WriteVarBytes(ReadOnlySpan<byte> bytes)
    {
        WriteVarInt((ulong)bytes.Length);
        WriteBytes(bytes);
    }

    /// <summary>
    /// var_str: CompactSize length prefix + ASCII bytes. User-agent etc.
    /// </summary>
    public void WriteVarStr(string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        WriteVarBytes(bytes);
    }

    public byte[] ToArray() => _stream.ToArray();
}
