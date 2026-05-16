#nullable enable
namespace Dxs.Bsv.P2p.Session;

/// <summary>
/// Lifecycle of a <see cref="PeerSession"/>.
/// </summary>
public enum PeerSessionState
{
    /// <summary>Constructed, not yet started.</summary>
    Created,
    /// <summary>TCP connect in progress.</summary>
    Connecting,
    /// <summary>TCP connected; version/verack exchange in progress.</summary>
    Handshaking,
    /// <summary>Handshake complete; receiving and sending messages.</summary>
    Ready,
    /// <summary>Graceful shutdown initiated.</summary>
    Closing,
    /// <summary>Session ended (graceful or error).</summary>
    Closed,
}

/// <summary>
/// Why the session ended. Surfaces in <see cref="PeerSession.Completion"/>.
/// </summary>
public enum DisconnectReason
{
    /// <summary>Session is still alive.</summary>
    None,
    /// <summary>Caller called <c>DisposeAsync</c> / cancelled.</summary>
    Local,
    /// <summary>Peer closed gracefully (FIN).</summary>
    PeerClosed,
    /// <summary>Peer sent TCP RST.</summary>
    PeerReset,
    /// <summary>Connect timed out.</summary>
    ConnectTimeout,
    /// <summary>Handshake did not complete inside the configured deadline.</summary>
    HandshakeTimeout,
    /// <summary>20-min inactivity threshold crossed.</summary>
    IdleTimeout,
    /// <summary>Received message was malformed at frame level (bad magic / bad checksum / oversized).</summary>
    ProtocolViolation,
    /// <summary>Peer's <c>version</c> failed our policy (e.g. protocol too old).</summary>
    HandshakeRejected,
    /// <summary>Unexpected exception inside the I/O loop.</summary>
    InternalError,
}
