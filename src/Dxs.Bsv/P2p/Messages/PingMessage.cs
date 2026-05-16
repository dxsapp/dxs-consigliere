#nullable enable
using System;

using Dxs.Bsv.P2p.Codec;

namespace Dxs.Bsv.P2p.Messages;

/// <summary>
/// <c>ping</c>: 8-byte nonce. Peer is expected to reply with <c>pong</c>
/// carrying the same nonce.
/// </summary>
public sealed record PingMessage(ulong Nonce)
{
    public byte[] Serialize()
    {
        var w = new P2pWriter(8);
        w.WriteUInt64Le(Nonce);
        return w.ToArray();
    }

    public static PingMessage Parse(ReadOnlySpan<byte> payload)
    {
        var reader = new P2pReader(payload);
        return new PingMessage(reader.ReadUInt64Le("ping.nonce"));
    }
}

/// <summary>
/// <c>pong</c>: echoes peer's ping nonce. Identical wire format to <see cref="PingMessage"/>.
/// </summary>
public sealed record PongMessage(ulong Nonce)
{
    public byte[] Serialize()
    {
        var w = new P2pWriter(8);
        w.WriteUInt64Le(Nonce);
        return w.ToArray();
    }

    public static PongMessage Parse(ReadOnlySpan<byte> payload)
    {
        var reader = new P2pReader(payload);
        return new PongMessage(reader.ReadUInt64Le("pong.nonce"));
    }
}
