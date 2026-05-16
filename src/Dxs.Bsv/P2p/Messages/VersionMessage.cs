#nullable enable
using System;

using Dxs.Bsv.P2p.Codec;

namespace Dxs.Bsv.P2p.Messages;

/// <summary>
/// The Bitcoin P2P <c>version</c> message — first frame exchanged on any
/// new connection. Wire format verified against:
/// <list type="bullet">
/// <item>bitcoin-sv master, <c>src/net/net_processing.cpp::ProcessVersionMessage</c></item>
/// <item>Teranode <c>services/legacy/peer/peer.go::negotiateOutboundProtocol</c></item>
/// <item>bsv-p2p (kevinejohn), <c>src/messages/version.ts</c></item>
/// <item>Live captured frames from a real <c>/Bitcoin SV:1.2.1/</c> peer (Spike E)</item>
/// </list>
/// </summary>
public sealed record VersionMessage(
    int ProtocolVersion,
    ulong Services,
    long TimestampUnixSeconds,
    P2pAddress AddrRecv,
    P2pAddress AddrFrom,
    ulong Nonce,
    string UserAgent,
    int StartHeight,
    bool Relay,
    /// <summary>
    /// Optional BSV-specific <c>LIMITED_BYTE_VEC</c> association ID
    /// (on wire: var_bytes of [IDType(1) + 16-byte UUID]). Omitted by default
    /// in Phase 1 (matches Teranode and bsv-p2p reference impls).
    /// </summary>
    byte[]? AssociationId)
{
    /// <summary>Per <c>SV-Node</c> <c>MAX_SUBVERSION_LENGTH</c>.</summary>
    public const int MaxUserAgentLength = 256;

    /// <summary>Per <c>AssociationID::MAX_ASSOCIATION_ID_LENGTH</c> in SV-Node.</summary>
    public const int MaxAssociationIdLength = 129;

    /// <summary>Wire-format <c>protocol</c> field value for Phase 1.</summary>
    public const int CurrentProtocolVersion = 70016;

    /// <summary>
    /// Build the wire payload (without frame header).
    /// </summary>
    public byte[] Serialize()
    {
        var writer = new P2pWriter(initialCapacity: 128);
        writer.WriteInt32Le(ProtocolVersion);
        writer.WriteUInt64Le(Services);
        writer.WriteInt64Le(TimestampUnixSeconds);
        AddrRecv.Write(writer);
        AddrFrom.Write(writer);
        writer.WriteUInt64Le(Nonce);
        writer.WriteVarStr(UserAgent);
        writer.WriteInt32Le(StartHeight);
        writer.WriteByte(Relay ? (byte)0x01 : (byte)0x00);
        if (AssociationId is not null)
        {
            writer.WriteVarBytes(AssociationId);
        }
        return writer.ToArray();
    }

    /// <summary>
    /// Parse a <c>version</c> payload. Tolerantly handles older or newer
    /// payloads: addr_from / nonce / user_agent / start_height / relay /
    /// association_id are all optional in the wire format
    /// (see net_processing.cpp:1734-1794).
    /// </summary>
    public static VersionMessage Parse(ReadOnlySpan<byte> payload)
    {
        var reader = new P2pReader(payload);

        var protocolVersion = reader.ReadInt32Le("protocol_version");
        var services = reader.ReadUInt64Le("services");
        var timestamp = reader.ReadInt64Le("timestamp");
        var addrRecv = P2pAddress.Read(ref reader, "addr_recv");

        // Per net_processing.cpp:1734 — addr_from and following are optional.
        if (reader.IsAtEnd)
        {
            return new VersionMessage(
                protocolVersion, services, timestamp, addrRecv,
                AddrFrom: P2pAddress.Anonymous(),
                Nonce: 0UL,
                UserAgent: string.Empty,
                StartHeight: 0,
                Relay: true,
                AssociationId: null);
        }

        var addrFrom = P2pAddress.Read(ref reader, "addr_from");
        var nonce = reader.ReadUInt64Le("nonce");

        var userAgent = string.Empty;
        if (!reader.IsAtEnd)
            userAgent = reader.ReadVarStr(MaxUserAgentLength, "user_agent");

        var startHeight = 0;
        if (!reader.IsAtEnd)
            startHeight = reader.ReadInt32Le("start_height");

        var relay = true;
        if (!reader.IsAtEnd)
            relay = reader.ReadByte("relay") != 0;

        byte[]? associationId = null;
        if (!reader.IsAtEnd)
        {
            associationId = reader.ReadVarBytes(MaxAssociationIdLength, "association_id").ToArray();
        }

        return new VersionMessage(
            protocolVersion, services, timestamp, addrRecv,
            addrFrom, nonce, userAgent, startHeight, relay, associationId);
    }
}
