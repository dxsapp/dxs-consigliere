#nullable enable
using System;
using System.Net;

using Dxs.Bsv.P2p.Codec;

namespace Dxs.Bsv.P2p;

/// <summary>
/// 26-byte network address as encoded inside a <c>version</c> message:
/// services(u64 LE) + ipv6(16) + port(u16 BE).
/// (No timestamp field — <c>INIT_PROTO_VERSION = 209</c> &lt; <c>CADDR_TIME_VERSION = 31402</c>.)
/// </summary>
public sealed record P2pAddress(ulong Services, byte[] RawIpv6, ushort Port)
{
    public const int Size = 26;
    public const int Ipv6Size = 16;

    /// <summary>An all-zero address ("we don't know our own external IP").</summary>
    public static P2pAddress Anonymous(ulong services = 0) =>
        new(services, new byte[Ipv6Size], 0);

    /// <summary>
    /// Build an IPv4-mapped address (i.e. 10 zero bytes + 0xFF 0xFF + 4 IPv4 bytes).
    /// </summary>
    public static P2pAddress FromIPv4(ulong services, string ipv4, ushort port)
    {
        var ipv6 = new byte[Ipv6Size];
        ipv6[10] = 0xFF;
        ipv6[11] = 0xFF;
        var parsed = IPAddress.Parse(ipv4);
        var v4Bytes = parsed.MapToIPv4().GetAddressBytes();
        if (v4Bytes.Length != 4)
            throw new ArgumentException($"'{ipv4}' is not a valid IPv4 address", nameof(ipv4));
        Buffer.BlockCopy(v4Bytes, 0, ipv6, 12, 4);
        return new P2pAddress(services, ipv6, port);
    }

    /// <summary>
    /// If this address is IPv4-mapped, return the IPv4 string; otherwise null.
    /// </summary>
    public string? TryGetIPv4()
    {
        if (RawIpv6 is null || RawIpv6.Length != Ipv6Size) return null;
        for (var i = 0; i < 10; i++)
            if (RawIpv6[i] != 0) return null;
        if (RawIpv6[10] != 0xFF || RawIpv6[11] != 0xFF) return null;
        return $"{RawIpv6[12]}.{RawIpv6[13]}.{RawIpv6[14]}.{RawIpv6[15]}";
    }

    public void Write(P2pWriter writer)
    {
        if (RawIpv6 is null || RawIpv6.Length != Ipv6Size)
            throw new InvalidOperationException($"{nameof(RawIpv6)} must be exactly {Ipv6Size} bytes");
        writer.WriteUInt64Le(Services);
        writer.WriteBytes(RawIpv6);
        writer.WriteUInt16Be(Port);
    }

    public static P2pAddress Read(ref P2pReader reader, string fieldName = "address")
    {
        var services = reader.ReadUInt64Le($"{fieldName}.services");
        var ipv6 = reader.ReadBytes(Ipv6Size, $"{fieldName}.ipv6").ToArray();
        var port = reader.ReadUInt16Be($"{fieldName}.port");
        return new P2pAddress(services, ipv6, port);
    }
}
