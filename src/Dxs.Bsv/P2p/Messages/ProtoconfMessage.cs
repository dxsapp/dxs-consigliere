#nullable enable
using System;
using System.Linq;

using Dxs.Bsv.P2p.Codec;

namespace Dxs.Bsv.P2p.Messages;

/// <summary>
/// BSV-specific <c>protoconf</c> message — sent immediately after VERACK
/// to advertise our preferred receive payload limit and supported stream
/// policies. Wire format per <c>protocol.h::CProtoconf</c>:
/// CompactSize numberOfFields + uint32 maxRecvPayloadLength + var_str streamPolicies.
/// </summary>
public sealed record ProtoconfMessage(uint MaxRecvPayloadLength, string StreamPolicies)
{
    /// <summary>BSV-Node's <c>LEGACY_MAX_PROTOCOL_PAYLOAD_LENGTH</c> floor (peer disconnects below this).</summary>
    public const uint MinAcceptableMaxRecvPayloadLength = 1 * 1024 * 1024;

    /// <summary>Phase 1 default: advertise 2 MiB receive limit.</summary>
    public const uint DefaultMaxRecvPayloadLength = 2 * 1024 * 1024;

    /// <summary>Phase 1 default policies string.</summary>
    public const string DefaultStreamPolicies = "Default";

    /// <summary>The standard <c>protoconf</c> we send after VERACK.</summary>
    public static ProtoconfMessage PhaseOneDefault { get; } = new(DefaultMaxRecvPayloadLength, DefaultStreamPolicies);

    public byte[] Serialize()
    {
        var w = new P2pWriter(32);
        w.WriteVarInt(2);                          // numberOfFields
        w.WriteUInt32Le(MaxRecvPayloadLength);
        w.WriteVarStr(StreamPolicies);
        return w.ToArray();
    }

    public static ProtoconfMessage Parse(ReadOnlySpan<byte> payload)
    {
        var reader = new P2pReader(payload);
        var numberOfFields = reader.ReadVarInt("protoconf.number_of_fields");
        if (numberOfFields < 1)
            throw new P2pDecodeException($"protoconf must have at least 1 field, got {numberOfFields}");

        var maxRecv = reader.ReadUInt32Le("protoconf.max_recv_payload_length");

        // Optional second field: streamPolicies var_str. Older peers may omit.
        var streamPolicies = "Default";
        if (numberOfFields >= 2 && !reader.IsAtEnd)
        {
            streamPolicies = reader.ReadVarStr(256, "protoconf.stream_policies");
        }

        return new ProtoconfMessage(maxRecv, streamPolicies);
    }
}
