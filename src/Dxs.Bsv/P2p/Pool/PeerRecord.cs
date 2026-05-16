#nullable enable
using System;
using System.Net;

namespace Dxs.Bsv.P2p.Pool;

/// <summary>
/// Tracking information about one peer endpoint we have either contacted
/// or learned about (via DNS seed / addr-gossip).
/// </summary>
public sealed record PeerRecord(IPEndPoint EndPoint)
{
    public PeerSource Source { get; init; } = PeerSource.Unknown;

    /// <summary>First time we ever encountered this peer.</summary>
    public DateTime FirstSeenUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Most recent time we observed this peer (inbound or outbound).</summary>
    public DateTime LastSeenUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Most recent time we successfully completed a handshake with this peer.</summary>
    public DateTime? LastConnectedUtc { get; init; }

    public string? UserAgent { get; init; }
    public int? ProtocolVersion { get; init; }
    public ulong? Services { get; init; }
    public uint? MaxRecvPayloadLength { get; init; }
    public long? MinFeePerKbSat { get; init; }

    public int SuccessCount { get; init; }
    public int FailCount { get; init; }
    public double? MeanLatencyMs { get; init; }

    /// <summary>When set in the future, the manager must NOT attempt to connect to this peer.</summary>
    public DateTime? NegativeUntilUtc { get; init; }

    public string? LastFailureReason { get; init; }

    public string Key => $"{EndPoint.Address}:{EndPoint.Port}";

    /// <summary>The "/24 subnet" string used for diversity-bounding in the pool.</summary>
    public string Subnet24
    {
        get
        {
            var bytes = EndPoint.Address.GetAddressBytes();
            if (bytes.Length == 4)
                return $"{bytes[0]}.{bytes[1]}.{bytes[2]}/24";
            // IPv6: bucket by /64 (8 bytes)
            return string.Join(":", new[] { bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7] });
        }
    }
}

public enum PeerSource
{
    Unknown,
    Config,
    DnsSeed,
    AddrGossip,
    Inbound,
}
