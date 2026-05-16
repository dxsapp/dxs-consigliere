using System;
using System.Buffers.Binary;
using System.Text;

namespace Dxs.Bsv.P2p.Codec;

/// <summary>
/// Forward-only reader over a P2P payload byte buffer. Throws
/// <see cref="P2pDecodeException"/> on malformed input — never an unhandled
/// <see cref="IndexOutOfRangeException"/> or similar.
/// </summary>
public ref struct P2pReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _position;

    public P2pReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    public int Position => _position;
    public int Remaining => _buffer.Length - _position;
    public bool IsAtEnd => _position >= _buffer.Length;

    private void EnsureAvailable(int count, string field)
    {
        if (_position + count > _buffer.Length)
            throw new P2pDecodeException($"Unexpected end of payload while reading {field} (need {count}, have {_buffer.Length - _position})");
    }

    public byte ReadByte(string field = "byte")
    {
        EnsureAvailable(1, field);
        return _buffer[_position++];
    }

    public ReadOnlySpan<byte> ReadBytes(int count, string field = "bytes")
    {
        EnsureAvailable(count, field);
        var slice = _buffer.Slice(_position, count);
        _position += count;
        return slice;
    }

    public short ReadInt16Le(string field = "int16")
    {
        EnsureAvailable(sizeof(short), field);
        var value = BinaryPrimitives.ReadInt16LittleEndian(_buffer.Slice(_position));
        _position += sizeof(short);
        return value;
    }

    public ushort ReadUInt16Le(string field = "uint16")
    {
        EnsureAvailable(sizeof(ushort), field);
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Slice(_position));
        _position += sizeof(ushort);
        return value;
    }

    public ushort ReadUInt16Be(string field = "uint16_be")
    {
        EnsureAvailable(sizeof(ushort), field);
        var value = BinaryPrimitives.ReadUInt16BigEndian(_buffer.Slice(_position));
        _position += sizeof(ushort);
        return value;
    }

    public int ReadInt32Le(string field = "int32")
    {
        EnsureAvailable(sizeof(int), field);
        var value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(_position));
        _position += sizeof(int);
        return value;
    }

    public uint ReadUInt32Le(string field = "uint32")
    {
        EnsureAvailable(sizeof(uint), field);
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Slice(_position));
        _position += sizeof(uint);
        return value;
    }

    public long ReadInt64Le(string field = "int64")
    {
        EnsureAvailable(sizeof(long), field);
        var value = BinaryPrimitives.ReadInt64LittleEndian(_buffer.Slice(_position));
        _position += sizeof(long);
        return value;
    }

    public ulong ReadUInt64Le(string field = "uint64")
    {
        EnsureAvailable(sizeof(ulong), field);
        var value = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.Slice(_position));
        _position += sizeof(ulong);
        return value;
    }

    /// <summary>CompactSize / VarInt decoding per Bitcoin P2P spec.</summary>
    public ulong ReadVarInt(string field = "varint")
    {
        var first = ReadByte(field);
        return first switch
        {
            0xFD => ReadUInt16Le(field),
            0xFE => ReadUInt32Le(field),
            0xFF => ReadUInt64Le(field),
            _ => first,
        };
    }

    /// <summary>
    /// var_bytes: read a CompactSize length, then that many bytes.
    /// Bounded by <paramref name="maxLength"/> to prevent malicious oversized claims.
    /// </summary>
    public ReadOnlySpan<byte> ReadVarBytes(int maxLength, string field = "var_bytes")
    {
        var size = ReadVarInt(field);
        if (size > (ulong)maxLength)
            throw new P2pDecodeException($"{field} length {size} exceeds max {maxLength}");
        return ReadBytes((int)size, field);
    }

    /// <summary>
    /// var_str: read CompactSize length, then that many ASCII bytes as string.
    /// </summary>
    public string ReadVarStr(int maxLength, string field = "var_str")
    {
        var bytes = ReadVarBytes(maxLength, field);
        return Encoding.ASCII.GetString(bytes);
    }

    /// <summary>Discard remaining unread bytes (e.g. unknown trailing fields).</summary>
    public ReadOnlySpan<byte> ReadRemaining()
    {
        var slice = _buffer.Slice(_position);
        _position = _buffer.Length;
        return slice;
    }
}
