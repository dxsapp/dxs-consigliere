using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace Dxs.Bsv.Protocol;

public class BitcoinStreamReader : IDisposable
{
    private readonly Stream _stream;

    public readonly HashStream HashStream = new();
    private bool _fillHashStream;

    private int _bytesRead;

    private readonly Stack<ICollection<byte>> _bytesReceivers = new();

    public BitcoinStreamReader(Stream stream)
    {
        _stream = stream;
    }

    public BitcoinStreamReader(byte[] buffer) : this(new MemoryStream(buffer)) { }

    public BitcoinStreamReader(string hex) : this(hex.FromHexString()) { }

    public int Position => _bytesRead;

    public void StopHashStream()
    {
        _fillHashStream = false;
        HashStream.Stop();
    }

    public void StartHashStream() => _fillHashStream = true;

    public void ResetHashStream() => HashStream.Reset();

    public void AddBytesReceiver(ICollection<byte> receiver) => _bytesReceivers.Push(receiver);

    public void RemoveBytesReceiver() => _bytesReceivers.Pop();

    public byte ReadByte()
    {
        var @byte = _stream.ReadByte();
        if (@byte == -1)
        {
            //TODO [Oleg] handle stream end
            throw new EndOfStreamException();
        }

        _bytesRead++;

        if (_fillHashStream) HashStream.Write((byte)@byte);

        foreach (var receiver in _bytesReceivers)
            receiver.Add((byte)@byte);

        return (byte)@byte;
    }

    public byte[] ReadNBytes(ulong count)
    {
        var result = new byte[count];

        for (ulong i = 0; i < count; i++)
            result[i] = ReadByte();

        return result;
    }

    public byte[] ReadNBytes(ushort count)
    {
        var result = new byte[count];

        for (var i = 0; i < count; i++)
            result[i] = ReadByte();

        return result;
    }

    public byte[] ReadNBytesLe(ulong count)
    {
        var result = new byte[count];

        for (ulong i = 0; i < count; i++)
            result[count - 1 - i] = ReadByte();

        return result;
    }

    public ushort ReadUInt16Le()
    {
        const int count = sizeof(ushort);
        Span<byte> bytes = stackalloc byte[count];
        for (var i = 0; i < count; i++)
            bytes[i] = ReadByte();

        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    public uint ReadUInt32Le()
    {
        const int count = sizeof(uint);
        Span<byte> bytes = stackalloc byte[count];
        for (var i = 0; i < count; i++)
            bytes[i] = ReadByte();

        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    public ulong ReadUInt64Le()
    {
        const int count = sizeof(ulong);
        Span<byte> bytes = stackalloc byte[count];
        for (var i = 0; i < count; i++)
            bytes[i] = ReadByte();

        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    public ulong ReadVarInt()
    {
        var first = ReadByte();

        return first switch
        {
            0xFD => ReadUInt16Le(),
            0xFE => ReadUInt32Le(),
            0Xff => ReadUInt64Le(),
            _ => first
        };
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
