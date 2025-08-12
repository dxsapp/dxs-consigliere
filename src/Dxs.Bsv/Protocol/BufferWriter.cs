using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;

namespace Dxs.Bsv.Protocol;

public class BufferWriter
{
    public BufferWriter(int size)
    {
        Bytes = new byte[size];
        Position = 0;
    }

    public BufferWriter(long size)
    {
        Bytes = new byte[size];
        Position = 0;
    }

    public int Position { get; private set; }
    public byte[] Bytes { get; }

    public void WriteByte(byte value)
    {
        Bytes[Position] = value;
        Position += 1;
    }

    public void WriteUInt16Le(ushort value)
    {
        WriteUInt16LittleEndian(sizeof(ushort), value);
    }

    public void WriteUInt32Le(uint value)
    {
        WriteUInt32LittleEndian(sizeof(uint), value);
    }

    public void WriteUInt64Le(ulong value)
    {
        WriteUInt64LittleEndian(sizeof(ulong), value);
    }

    public void WriteNumberLe(long value)
    {
        // if (value == 0)
        // {
        //     WriteByte((byte)OpCode.OP_0);
        //     return;
        // }
        //
        // if (value <= 16)
        // {
        //     WriteByte((byte)(0x50 + (byte)value));
        //     return;
        // }
        const int size = sizeof(ulong);

        Span<byte> tempBuf = stackalloc byte[size];
        BinaryPrimitives.WriteInt64LittleEndian(tempBuf, value);

        var actualSize = GetMinimumRequiredBytes(value);
        var slice = Bytes.AsSpan(Position, actualSize);

        for (var i = 0; i < actualSize; i++)
            slice[i] = tempBuf[i];

        Position += actualSize;
    }

    public void WriteInt32Be(int value)
    {
        WriteInt32BigEndian(sizeof(int), value);
    }

    /// <summary>
    /// write var int into <see cref="Bytes"/>
    /// <para>
    ///     1. VarInt: https://learnmeabitcoin.com/technical/varint
    /// </para>
    /// <para>
    ///     2. CompactSize Unsigned Integers: https://developer.bitcoin.org/reference/transactions.html#compactsize-unsigned-integers
    /// </para>
    /// </summary>
    /// <param name="value">value for writing</param>
    /// <returns><see cref="BufferWriter"/></returns>
    public BufferWriter WriteVarInt(ulong value)
    {
        switch (value)
        {
            case >= 0 and <= 0xFC:
                WriteByte((byte)value);
                break;
            case >= 0xFD and <= 0xFFFF:
            {
                WriteByte(0xFD);
                WriteUInt16LittleEndian(sizeof(ushort), (ushort)value);
                break;
            }
            case >= 0x10000 and <= 0xFFFFFFFF:
            {
                WriteByte(0xFE);
                WriteUInt32LittleEndian(sizeof(uint), (uint)value);
                break;
            }
            case >= 0x100000000 and <= 0xffffffffffffffff:
            {
                WriteByte(0xFF);
                WriteUInt64LittleEndian(sizeof(ulong), value);
                break;
            }
        }

        return this;
    }

    public BufferWriter Write(IEnumerable<byte> bytes)
    {
        foreach (var b in bytes)
        {
            WriteByte(b);
        }

        return this;
    }

    public BufferWriter WriteChunk(IList<byte> bytes)
    {
        return WriteVarInt((ulong)bytes.Count)
            .Write(bytes);
    }

    public BufferWriter WriteReverse(Span<byte> bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            WriteByte(bytes[bytes.Length - 1 - i]);
        }

        return this;
    }

    public BufferWriter WriteScriptToken(ScriptReadToken token)
    {
        WriteByte(token.OpCodeNum);

        switch (token.OpCodeNum)
        {
            case > 0 and < (byte)OpCode.OP_PUSHDATA1:
            {
                Write(token.Bytes.ToArray());

                break;
            }
            case (byte)OpCode.OP_PUSHDATA1:
            {
                var count = (byte)token.Bytes.Length;

                WriteByte(count);
                Write(token.Bytes.ToArray());

                break;
            }
            case (byte)OpCode.OP_PUSHDATA2:
            {
                var count = (ushort)token.Bytes.Length;

                WriteUInt16Le(count);
                Write(token.Bytes.ToArray());

                break;
            }
            case (byte)OpCode.OP_PUSHDATA4:
            {
                var count = (uint)token.Bytes.Length;

                WriteUInt32Le(count);
                Write(token.Bytes.ToArray());

                break;
            }
        }

        return this;
    }

    public static int GetChunkSize(int size) => GetVarIntLength(size) + size;
    public static int GetChunkSize(IList<byte> b) => GetVarIntLength(b.Count) + b.Count;

    public static int GetVarIntLength(int value) => GetVarIntLength((ulong)value);

    public static int GetVarIntLength(ulong value) =>
        value switch
        {
            < 0xFD => 1,
            <= 0xFFFF => 3, // sizeof(ushort) + 1,
            <= 0xFFFFFFFF => 5, //sizeof(uint) + 1,
            _ => 9 // sizeof(ulong) + 1
        };

    public static int GetMinimumRequiredBytes(long value) =>
        value switch
        {
            >= -128 and <= 127 => 1,
            >= -32768 and <= 32767 => 2,
            >= -8388608 and <= 8388607 => 3,
            >= -2147483648 and <= 2147483647 => 4,
            >= -549755813888 and <= 549755813887 => 5,
            >= -140737488355328 and <= 140737488355327 => 6,
            >= -36028797018963968 and <= 36028797018963967 => 7,
            _ => 8
        };

    public static int GetMinimumRequiredBytes(ulong value) =>
        value > long.MaxValue ? 8 : GetMinimumRequiredBytes((long)value);

    public static int GetNumberSize(ulong data) => data switch
    {
        <= 16 => 1,
        _ => GetVarIntLength(GetMinimumRequiredBytes(data)) + GetMinimumRequiredBytes(data)
    };

    public static byte[] GetNumberBufferLe(long value)
    {
        var buffer = new BufferWriter(GetMinimumRequiredBytes(value));
        buffer.WriteNumberLe(value);

        return buffer.Bytes;
    }

    private void WriteInt32BigEndian(int size, int value)
    {
        var res = Bytes.AsSpan(Position, size);
        BinaryPrimitives.WriteInt32BigEndian(res, value);

        Position += size;
    }

    private void WriteUInt16LittleEndian(int size, ushort value)
    {
        var res = Bytes.AsSpan(Position, size);
        BinaryPrimitives.WriteUInt16LittleEndian(res, value);

        Position += size;
    }

    private void WriteUInt32LittleEndian(int size, uint value)
    {
        var res = Bytes.AsSpan(Position, size);
        BinaryPrimitives.WriteUInt32LittleEndian(res, value);

        Position += size;
    }

    private void WriteUInt64LittleEndian(int size, ulong value)
    {
        var res = Bytes.AsSpan(Position, size);
        BinaryPrimitives.WriteUInt64LittleEndian(res, value);

        Position += size;
    }
}