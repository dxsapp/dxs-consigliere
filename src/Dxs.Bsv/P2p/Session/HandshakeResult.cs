#nullable enable
using Dxs.Bsv.P2p.Messages;

namespace Dxs.Bsv.P2p.Session;

/// <summary>
/// Outcome of <see cref="PeerSession.ConnectAsync"/>.
/// </summary>
public sealed record HandshakeResult(bool Success, VersionMessage? PeerVersion, DisconnectReason FailureReason, string? FailureDetail)
{
    public static HandshakeResult Ok(VersionMessage peerVersion) =>
        new(true, peerVersion, DisconnectReason.None, null);

    public static HandshakeResult Failed(DisconnectReason reason, string? detail = null) =>
        new(false, null, reason, detail);
}

/// <summary>
/// A received frame after handshake — command + raw payload bytes. Same
/// shape as <see cref="Frame"/> but used as a value carried through the
/// inbound channel.
/// </summary>
public sealed record InboundFrame(string Command, byte[] Payload);
