using System;
using System.Buffers;
using System.IO;
using System.Threading;

namespace Dxs.Bsv.Protocol;

public class HashStream: Stream
{
    private const int BufferSize = 4096;
    private readonly byte[] _buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

    private int _bufferWriteCursor;
    private int _bufferReadCursor;

    private int _availableBytes;

    private bool _stopped;
    private bool _read;

    public override int Read(byte[] buffer, int offset, int count)
    {
        var sw = new SpinWait();

        while (_availableBytes != BufferSize)
        {
            if (_stopped)
                break;

            sw.SpinOnce();
        }

        _read = true;

        var readBytes = Math.Min(count, _availableBytes);
        readBytes = Math.Min(readBytes, BufferSize - _bufferReadCursor);

        Buffer.BlockCopy(_buffer, _bufferReadCursor, buffer, offset, readBytes);

        _bufferReadCursor += readBytes;

        if (_bufferReadCursor == BufferSize)
            _bufferReadCursor = 0;

        _availableBytes -= readBytes;
        _read = false;

        return readBytes;
    }

    public void Write(byte @byte)
    {
        var sw = new SpinWait();

        while (_availableBytes == BufferSize || _read)
            sw.SpinOnce();

        _buffer[_bufferWriteCursor++] = @byte;

        if (_bufferWriteCursor == BufferSize)
            _bufferWriteCursor = 0;

        _availableBytes++;
    }

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public void Stop() => _stopped = true;

    public void Reset()
    {
        _bufferWriteCursor = 0;
        _bufferReadCursor = 0;
        _availableBytes = 0;
        _stopped = false;
        _read = false;
    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc cref="Stream.CanRead"/>
    public override bool CanRead => true;

    /// <inheritdoc cref="Stream.CanSeek"/>
    public override bool CanSeek => false;

    /// <inheritdoc cref="Stream.CanWrite"/>
    public override bool CanWrite => true;

    /// <inheritdoc cref="Stream.Length"/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc cref="Stream.Position"/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        ArrayPool<byte>.Shared.Return(_buffer, clearArray: false);

        base.Dispose(disposing);
    }
}