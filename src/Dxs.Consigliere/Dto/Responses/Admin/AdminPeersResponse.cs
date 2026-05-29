#nullable enable
using System;

namespace Dxs.Consigliere.Dto.Responses.Admin;

/// <summary>
/// wave-A4 S3 — sealed response shape for
/// <c>GET /api/admin/p2p/peers</c>. Previously the controller
/// returned an anonymous object literal, so Swashbuckle typed the
/// endpoint as <c>object</c> (no schema) and the admin UI hand-
/// mirrored the shape. This DTO makes the contract first-class so
/// a field rename surfaces as a screen-side TS compile error.
/// </summary>
public sealed class AdminPeersResponse
{
    public int Total { get; set; }
    public int Successful { get; set; }
    public int Failed { get; set; }
    public int DistinctSubnets { get; set; }
    public AdminPeerRow[] Peers { get; set; } = [];
}

/// <summary>
/// One peer row. Mirrors the projection in
/// <see cref="Controllers.AdminP2pController.Peers"/>.
/// </summary>
public sealed class AdminPeerRow
{
    public string Endpoint { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public int? ProtocolVersion { get; set; }
    public ulong? Services { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    // FirstSeenUtc / LastSeenUtc are non-nullable DateTime on the
    // source PeerRecord, but kept nullable here per the wave-A4 S3
    // bias-to-nullable rule (the previous hand-mirror exposed them
    // as nullable and screens already tolerate absence).
    public DateTime? FirstSeen { get; set; }
    public DateTime? LastSeen { get; set; }
    public DateTime? LastConnected { get; set; }
    public DateTime? NegativeUntil { get; set; }
    public string? LastFailureReason { get; set; }
    public string Subnet24 { get; set; } = string.Empty;
}
