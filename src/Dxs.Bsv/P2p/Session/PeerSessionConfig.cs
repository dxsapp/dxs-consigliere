#nullable enable
using System;

namespace Dxs.Bsv.P2p.Session;

/// <summary>
/// Tunables for a single <see cref="PeerSession"/>.
/// Defaults match BSV-Node and Teranode conventions.
/// </summary>
public sealed record PeerSessionConfig
{
    /// <summary>TCP connect timeout. BSV-Node uses 5s out-of-the-box.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Time allowed from socket connect to receiving peer's <c>verack</c>.
    /// BSV-Node default is 60s (<c>DEFAULT_P2P_HANDSHAKE_TIMEOUT_INTERVAL</c>).
    /// </summary>
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long to wait for any incoming traffic before declaring the peer dead.
    /// BSV-Node default is 1200s.
    /// </summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(1200);

    /// <summary>
    /// How often we send our own <c>ping</c>. Defaults to half of <see cref="IdleTimeout"/>
    /// so that idle peers never trip the watchdog under normal conditions.
    /// </summary>
    public TimeSpan PingInterval { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Maximum payload length the codec will accept for an inbound frame
    /// before we negotiate a higher limit via <c>protoconf</c>. Matches BSV-Node's
    /// <c>LEGACY_MAX_PROTOCOL_PAYLOAD_LENGTH</c> default (2 MiB).
    /// </summary>
    public int InitialMaxRecvPayloadLength { get; init; } = Frame.LegacyMaxPayloadLength;

    /// <summary>
    /// Capacity of the inbound message channel handed to the caller. Provides
    /// backpressure: if the caller does not consume, the receive loop blocks.
    /// </summary>
    public int InboundChannelCapacity { get; init; } = 256;

    /// <summary>
    /// Set <c>true</c> to write a <c>protoconf</c> immediately after our <c>verack</c>.
    /// Matches BSV-Node behaviour and is required for full protocol parity.
    /// </summary>
    public bool SendProtoconfAfterVerack { get; init; } = true;
}
