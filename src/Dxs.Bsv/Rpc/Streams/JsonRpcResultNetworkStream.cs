using System;
using System.Collections.Generic;
using System.IO;

namespace Dxs.Bsv.Rpc.Streams;

public class JsonRpcResultNetworkStream(Stream stream) : Stream
{
    private static readonly Dictionary<byte, byte> Utf8ToHex = new()
    {
        //Numbers
        { 0x30, 0 },
        { 0x31, 1 },
        { 0x32, 2 },
        { 0x33, 3 },
        { 0x34, 4 },
        { 0x35, 5 },
        { 0x36, 6 },
        { 0x37, 7 },
        { 0x38, 8 },
        { 0x39, 9 },

        //Capital Letters
        { 0x41, 10 },
        { 0x42, 11 },
        { 0x43, 12 },
        { 0x44, 13 },
        { 0x45, 14 },
        { 0x46, 15 },

        //small Letters
        { 0x61, 10 },
        { 0x62, 11 },
        { 0x63, 12 },
        { 0x64, 13 },
        { 0x65, 14 },
        { 0x66, 15 },
    };

    private static readonly byte[] JsonPropertyName = "{\"result\": \""u8.ToArray();
    private static readonly byte JsonValueClosingQuote = "\""u8.ToArray()[0];

    private const int MaxBufferSize = 1024 * 4 * 2;

    private readonly byte[] _buffer = new byte[MaxBufferSize];

    private readonly byte[] _payloadBuffer = new byte[MaxBufferSize / 2];
    private int _payloadBufferReadCursor;
    private int _payloadBufferWriteCursor;

    private readonly byte[] _hexCharBuffer = new byte[2];
    private int _hexCharIdx;

    private bool _payloadStarted;
    private bool _resultStarted;
    private bool _payloadReadFinished;
    private bool _networkStreamFinished;

    private int _availableBytes;


    public override void Flush() => stream.Flush();

    private bool ReadStream()
    {
        if (_networkStreamFinished)
            return false;

        try
        {
            var actualCount = stream.Read(_buffer, 0, _buffer.Length);

            if (actualCount is 0 or -1)
            {
                _networkStreamFinished = true;

                if (!_payloadReadFinished)
                    throw new Exception("Stream ended unexpectedly");

                return false;
            }

            var startOffset = 0;

            for (var i = 0; i < actualCount; i++)
            {
                var b = _buffer[i];

                if (!_payloadStarted)
                {
                    if (JsonPropertyName[startOffset] == b)
                    {
                        if (!_resultStarted)
                            _resultStarted = true;

                        startOffset++;

                        // found json property name and quote right before payload starts '"result": "'
                        if (startOffset == JsonPropertyName.Length)
                        {
                            _payloadStarted = true;
                        }
                    }
                    else
                    {
                        _payloadStarted = false;
                    }
                }
                else
                {
                    if (b == JsonValueClosingQuote)
                    {
                        if (_hexCharIdx == 1)
                            throw new Exception("Stream ended unexpectedly");

                        _payloadReadFinished = true;
                        return false;
                    }

                    _hexCharBuffer[_hexCharIdx] = Utf8ToHex[b];

                    if (_hexCharIdx == 1)
                    {
                        _payloadBuffer[_payloadBufferWriteCursor] = BinaryHelpers.HexToByte(_hexCharBuffer[0], _hexCharBuffer[1]);

                        _payloadBufferWriteCursor++;
                        if (_payloadBufferWriteCursor == _payloadBuffer.Length)
                            _payloadBufferWriteCursor = 0;

                        _availableBytes++;
                        _hexCharIdx = 0;
                    }
                    else
                    {
                        _hexCharIdx = 1;
                    }
                }
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0)
        {
            throw new Exception("Too low count");
        }
            
        var readBytes = 0;

        if (_availableBytes > 0)
        {
            readBytes = Math.Min(count, _availableBytes);
            Buffer.BlockCopy(_payloadBuffer, _payloadBufferReadCursor, buffer, offset, readBytes);

            SyncPayloadReadCursor(readBytes);
        }

        var needMore = count - readBytes;

        if (needMore > 0)
        {
            while (_availableBytes < needMore && ReadStream()) {}

            var moreBytes = Math.Min(needMore, _availableBytes);

            if (moreBytes > 0)
            {
                Buffer.BlockCopy(_payloadBuffer, _payloadBufferReadCursor, buffer, offset + readBytes, moreBytes);

                SyncPayloadReadCursor(moreBytes);
                readBytes += moreBytes;
            }
        }

        return readBytes;
    }

    private void SyncPayloadReadCursor(int readBytes)
    {
        IncreasePayloadReadCursor(readBytes);
        _availableBytes -= readBytes;

        Position += readBytes;
    }

    private void IncreasePayloadReadCursor(int readBytes)
    {
        _payloadBufferReadCursor += readBytes;

        if (_payloadBufferReadCursor >= _payloadBuffer.Length)
            _payloadBufferReadCursor -= _payloadBuffer.Length;
    }

    public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

    public override void SetLength(long value) => stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => stream.Write(buffer, offset, count);

    public override bool CanRead => true;
    public override bool CanSeek => stream.CanSeek;

    public override bool CanWrite => stream.CanWrite;
    public override long Length => stream.Length;

    public override long Position { get; set; }
}