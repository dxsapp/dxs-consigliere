#nullable enable
using System;
using System.Collections.Generic;

using Dxs.Bsv.P2p.Codec;

namespace Dxs.Bsv.P2p.Messages;

/// <summary>
/// One <c>addr</c> entry with the gossip timestamp. CADDR_TIME_VERSION-style:
/// 4-byte timestamp + 26-byte CAddress.
/// </summary>
public sealed record TimedAddress(uint TimestampUnixSeconds, P2pAddress Address)
{
    public void Write(P2pWriter writer)
    {
        writer.WriteUInt32Le(TimestampUnixSeconds);
        Address.Write(writer);
    }

    public static TimedAddress Read(ref P2pReader reader, string fieldName)
    {
        var ts = reader.ReadUInt32Le($"{fieldName}.timestamp");
        var addr = P2pAddress.Read(ref reader, $"{fieldName}.address");
        return new TimedAddress(ts, addr);
    }
}

/// <summary>
/// <c>addr</c>: list of known network addresses gossiped between peers.
/// Capped at 1000 entries (oversized → Misbehaving +20 per net_processing.cpp:2284).
/// </summary>
public sealed record AddrMessage(IReadOnlyList<TimedAddress> Addresses)
{
    public const int MaxAddresses = 1000;

    public byte[] Serialize()
    {
        var w = new P2pWriter(1 + Addresses.Count * (4 + P2pAddress.Size));
        w.WriteVarInt((ulong)Addresses.Count);
        foreach (var entry in Addresses)
            entry.Write(w);
        return w.ToArray();
    }

    public static AddrMessage Parse(ReadOnlySpan<byte> payload)
    {
        var reader = new P2pReader(payload);
        var count = reader.ReadVarInt("addr.count");
        if (count > (ulong)MaxAddresses)
            throw new P2pDecodeException($"addr count {count} exceeds max {MaxAddresses}");
        var list = new List<TimedAddress>((int)count);
        for (ulong i = 0; i < count; i++)
            list.Add(TimedAddress.Read(ref reader, $"addr[{i}]"));
        return new AddrMessage(list);
    }

    /// <summary>Convenience: empty <c>addr</c> response.</summary>
    public static AddrMessage Empty { get; } = new(Array.Empty<TimedAddress>());
}
