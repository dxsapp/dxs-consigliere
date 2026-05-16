using System;

using Dxs.Bsv.P2p;

namespace Dxs.Bsv.Tests.P2p;

public class FrameCodecTests
{
    [Fact]
    public void Encode_VersionFrame_HasCorrectMagicCommandLengthChecksum()
    {
        var payload = new byte[] { 0xAA, 0xBB, 0xCC };
        var frame = FrameCodec.Encode(P2pNetwork.Mainnet, P2pCommands.Version, payload);

        // Magic wire bytes: e3 e1 f3 e8 (BSV pchMessageStart)
        Assert.Equal(0xE3, frame[0]);
        Assert.Equal(0xE1, frame[1]);
        Assert.Equal(0xF3, frame[2]);
        Assert.Equal(0xE8, frame[3]);

        // Command: "version" NUL-padded to 12
        var command = System.Text.Encoding.ASCII.GetString(frame, 4, 12);
        Assert.StartsWith("version\0\0\0\0\0", command);

        // Length: 3 (LE)
        Assert.Equal(3, frame[16]);
        Assert.Equal(0, frame[17]);
        Assert.Equal(0, frame[18]);
        Assert.Equal(0, frame[19]);

        // Payload
        Assert.Equal(0xAA, frame[24]);
        Assert.Equal(0xBB, frame[25]);
        Assert.Equal(0xCC, frame[26]);

        // Frame is header + payload
        Assert.Equal(Frame.HeaderSize + payload.Length, frame.Length);
    }

    [Fact]
    public void Encode_Decode_RoundTrip_EmptyPayload()
    {
        var encoded = FrameCodec.Encode(P2pNetwork.Mainnet, P2pCommands.Verack, ReadOnlySpan<byte>.Empty);

        var result = FrameCodec.TryDecode(P2pNetwork.Mainnet, encoded, Frame.LegacyMaxPayloadLength, out var frame, out var consumed);

        Assert.Equal(DecodeResult.Ok, result);
        Assert.NotNull(frame);
        Assert.Equal(P2pCommands.Verack, frame!.Command);
        Assert.Empty(frame.Payload);
        Assert.Equal(Frame.HeaderSize, consumed);
    }

    [Fact]
    public void Encode_Decode_RoundTrip_WithPayload()
    {
        var original = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var encoded = FrameCodec.Encode(P2pNetwork.Mainnet, P2pCommands.Ping, original);

        var result = FrameCodec.TryDecode(P2pNetwork.Mainnet, encoded, Frame.LegacyMaxPayloadLength, out var frame, out var consumed);

        Assert.Equal(DecodeResult.Ok, result);
        Assert.NotNull(frame);
        Assert.Equal(P2pCommands.Ping, frame!.Command);
        Assert.Equal(original, frame.Payload);
        Assert.Equal(encoded.Length, consumed);
    }

    [Fact]
    public void Decode_ShortBuffer_ReturnsNeedMore()
    {
        var partial = new byte[10];

        var result = FrameCodec.TryDecode(P2pNetwork.Mainnet, partial, Frame.LegacyMaxPayloadLength, out var frame, out var consumed);

        Assert.Equal(DecodeResult.NeedMore, result);
        Assert.Null(frame);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void Decode_PartialPayload_ReturnsNeedMore()
    {
        // Encode a frame with 100-byte payload, but only deliver the header + 10 bytes.
        var payload = new byte[100];
        var full = FrameCodec.Encode(P2pNetwork.Mainnet, P2pCommands.Tx, payload);
        var truncated = full.AsSpan(0, Frame.HeaderSize + 10);

        var result = FrameCodec.TryDecode(P2pNetwork.Mainnet, truncated, Frame.LegacyMaxPayloadLength, out var frame, out var consumed);

        Assert.Equal(DecodeResult.NeedMore, result);
        Assert.Null(frame);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void Decode_BadMagic_ReturnsBadMagic()
    {
        var buf = new byte[Frame.HeaderSize];
        buf[0] = 0xDE; buf[1] = 0xAD; buf[2] = 0xBE; buf[3] = 0xEF;
        buf[4] = (byte)'v'; buf[5] = (byte)'e'; buf[6] = (byte)'r'; buf[7] = (byte)'s';

        var result = FrameCodec.TryDecode(P2pNetwork.Mainnet, buf, Frame.LegacyMaxPayloadLength, out var frame, out _);

        Assert.Equal(DecodeResult.BadMagic, result);
        Assert.Null(frame);
    }

    [Fact]
    public void Decode_BadChecksum_ReturnsBadChecksum()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03 };
        var frame = FrameCodec.Encode(P2pNetwork.Mainnet, P2pCommands.Ping, payload);

        // Corrupt the checksum
        frame[20] ^= 0xFF;

        var result = FrameCodec.TryDecode(P2pNetwork.Mainnet, frame, Frame.LegacyMaxPayloadLength, out var decoded, out _);

        Assert.Equal(DecodeResult.BadChecksum, result);
        Assert.Null(decoded);
    }

    [Fact]
    public void Decode_OversizedPayload_ReturnsOversized()
    {
        // Header claims payload length = 10 MB; we cap at 1 MB.
        var buf = new byte[Frame.HeaderSize];

        // Magic
        buf[0] = 0xE3; buf[1] = 0xE1; buf[2] = 0xF3; buf[3] = 0xE8;
        // Command "ping" + NULs
        buf[4] = (byte)'p'; buf[5] = (byte)'i'; buf[6] = (byte)'n'; buf[7] = (byte)'g';
        // Length = 10_000_000 LE
        var len = (uint)10_000_000;
        buf[16] = (byte)(len & 0xFF);
        buf[17] = (byte)((len >> 8) & 0xFF);
        buf[18] = (byte)((len >> 16) & 0xFF);
        buf[19] = (byte)((len >> 24) & 0xFF);

        var result = FrameCodec.TryDecode(P2pNetwork.Mainnet, buf, maxPayloadLength: 1_000_000, out var frame, out _);

        Assert.Equal(DecodeResult.OversizedPayload, result);
        Assert.Null(frame);
    }

    [Fact]
    public void Decode_BadCommand_NonPrintable_ReturnsBadCommand()
    {
        var buf = new byte[Frame.HeaderSize];
        buf[0] = 0xE3; buf[1] = 0xE1; buf[2] = 0xF3; buf[3] = 0xE8;
        // Command contains 0x01 (non-printable)
        buf[4] = 0x01;

        var result = FrameCodec.TryDecode(P2pNetwork.Mainnet, buf, Frame.LegacyMaxPayloadLength, out var frame, out _);

        Assert.Equal(DecodeResult.BadCommand, result);
        Assert.Null(frame);
    }

    [Fact]
    public void Decode_BadCommand_NonNulAfterTerminator_ReturnsBadCommand()
    {
        var buf = new byte[Frame.HeaderSize];
        buf[0] = 0xE3; buf[1] = 0xE1; buf[2] = 0xF3; buf[3] = 0xE8;
        // Command "abc\0XYZ\0..." — NUL followed by non-NUL is illegal.
        buf[4] = (byte)'a'; buf[5] = (byte)'b'; buf[6] = (byte)'c'; buf[7] = 0;
        buf[8] = (byte)'X'; // illegal

        var result = FrameCodec.TryDecode(P2pNetwork.Mainnet, buf, Frame.LegacyMaxPayloadLength, out var frame, out _);

        Assert.Equal(DecodeResult.BadCommand, result);
        Assert.Null(frame);
    }
}
