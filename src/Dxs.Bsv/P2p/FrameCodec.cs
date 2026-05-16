#nullable enable
using System;
using System.Buffers.Binary;
using System.Text;

namespace Dxs.Bsv.P2p;

/// <summary>
/// Encode and decode Bitcoin P2P wire frames. Pure byte-level; no I/O.
/// </summary>
public static class FrameCodec
{
    /// <summary>
    /// Build a complete wire frame: 24-byte header + payload. The checksum is
    /// <c>SHA256(SHA256(payload))[0..4]</c>.
    /// </summary>
    /// <param name="network">Magic-byte source.</param>
    /// <param name="command">ASCII command (e.g. <c>"version"</c>). Must be ≤ 12 chars.</param>
    /// <param name="payload">Message payload, may be empty.</param>
    public static byte[] Encode(P2pNetwork network, string command, ReadOnlySpan<byte> payload)
    {
        if (network is null) throw new ArgumentNullException(nameof(network));
        if (command is null) throw new ArgumentNullException(nameof(command));
        if (command.Length > P2pCommands.CommandSize)
            throw new ArgumentException($"Command '{command}' exceeds {P2pCommands.CommandSize} chars", nameof(command));

        var frame = new byte[Frame.HeaderSize + payload.Length];

        // magic (LE)
        BinaryPrimitives.WriteUInt32LittleEndian(
            frame.AsSpan(Frame.MagicOffset, Frame.MagicSize), network.Magic);

        // command (ASCII, NUL-padded to 12 bytes)
        var commandBytes = Encoding.ASCII.GetBytes(command);
        commandBytes.CopyTo(frame.AsSpan(Frame.CommandOffset, Frame.CommandSize));
        // Remaining bytes are already zero from `new byte[]`.

        // payload length (LE u32)
        BinaryPrimitives.WriteUInt32LittleEndian(
            frame.AsSpan(Frame.LengthOffset, Frame.LengthSize), (uint)payload.Length);

        // checksum = sha256(sha256(payload))[0..4]
        Span<byte> doubleSha = stackalloc byte[32];
        Hash.Sha256Sha256(payload, doubleSha);
        doubleSha.Slice(0, Frame.ChecksumSize).CopyTo(frame.AsSpan(Frame.ChecksumOffset, Frame.ChecksumSize));

        // payload
        payload.CopyTo(frame.AsSpan(Frame.HeaderSize));

        return frame;
    }

    /// <summary>
    /// Attempt to decode a single frame from <paramref name="buffer"/>. Designed
    /// for use in a streaming reader: returns <see cref="DecodeResult.NeedMore"/>
    /// when the buffer is short, leaving <paramref name="consumed"/> at 0 so the
    /// caller can keep accumulating bytes.
    /// </summary>
    public static DecodeResult TryDecode(
        P2pNetwork network,
        ReadOnlySpan<byte> buffer,
        int maxPayloadLength,
        out Frame? frame,
        out int consumed)
    {
        frame = null;
        consumed = 0;

        if (network is null) throw new ArgumentNullException(nameof(network));

        if (buffer.Length < Frame.HeaderSize)
            return DecodeResult.NeedMore;

        // 1. Magic
        var magic = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(Frame.MagicOffset, Frame.MagicSize));
        if (magic != network.Magic)
            return DecodeResult.BadMagic;

        // 2. Command (ASCII, must be NUL-terminated/padded, printable)
        var commandSpan = buffer.Slice(Frame.CommandOffset, Frame.CommandSize);
        if (!TryParseCommand(commandSpan, out var command))
            return DecodeResult.BadCommand;

        // 3. Payload length
        var length = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(Frame.LengthOffset, Frame.LengthSize));
        if (length > (uint)maxPayloadLength)
            return DecodeResult.OversizedPayload;

        // 4. Have we got the full payload yet?
        if (buffer.Length < Frame.HeaderSize + (int)length)
            return DecodeResult.NeedMore;

        var payload = buffer.Slice(Frame.HeaderSize, (int)length);

        // 5. Checksum
        Span<byte> doubleSha = stackalloc byte[32];
        Hash.Sha256Sha256(payload, doubleSha);
        var expectedChecksum = buffer.Slice(Frame.ChecksumOffset, Frame.ChecksumSize);
        if (!doubleSha.Slice(0, Frame.ChecksumSize).SequenceEqual(expectedChecksum))
            return DecodeResult.BadChecksum;

        frame = new Frame(command, payload.ToArray());
        consumed = Frame.HeaderSize + (int)length;
        return DecodeResult.Ok;
    }

    private static bool TryParseCommand(ReadOnlySpan<byte> bytes, out string command)
    {
        command = string.Empty;

        // Find first NUL; all bytes after must be NUL too. All non-NUL bytes
        // must be printable ASCII (0x20..0x7E). Per protocol.cpp CheckCommandFormat.
        var nameLength = bytes.Length;
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == 0)
            {
                nameLength = i;
                for (var j = i + 1; j < bytes.Length; j++)
                {
                    if (bytes[j] != 0) return false;
                }
                break;
            }
            if (bytes[i] < 0x20 || bytes[i] > 0x7E) return false;
        }

        command = Encoding.ASCII.GetString(bytes.Slice(0, nameLength));
        return true;
    }
}

/// <summary>
/// Result of attempting to decode a single frame from a buffer.
/// </summary>
public enum DecodeResult
{
    /// <summary>Frame was successfully decoded.</summary>
    Ok,
    /// <summary>Buffer is too short to contain a full frame; caller should keep reading.</summary>
    NeedMore,
    /// <summary>First 4 bytes do not match the expected network magic.</summary>
    BadMagic,
    /// <summary>Command field is not valid ASCII / not NUL-padded correctly.</summary>
    BadCommand,
    /// <summary>Payload length exceeds caller-specified maximum.</summary>
    OversizedPayload,
    /// <summary>Frame checksum does not match the double-SHA256 of the payload.</summary>
    BadChecksum,
}
