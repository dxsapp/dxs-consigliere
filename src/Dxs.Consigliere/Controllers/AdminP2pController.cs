using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Services.P2p;
using Dxs.Consigliere.Setup;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Controllers;

/// <summary>
/// Diagnostic surface for the Gate 2 BSV thin-node P2p subsystem.
/// Read-only; used by operators during the 24h soak test to track
/// peer-acceptance rate, ASN diversity, and per-peer state.
/// </summary>
[ApiController]
[Route("api/admin/p2p")]
[Authorize(Policy = AdminAuthDefaults.Policy)]
public class AdminP2pController(BsvP2pHealth health) : ControllerBase
{
    /// <summary>Live pool overview — counts and diversity metrics.</summary>
    [HttpGet("health")]
    public ActionResult<P2pHealthDto> Health()
    {
        return Ok(new P2pHealthDto(
            Bound: health.Bound,
            PoolSize: health.PoolSize,
            TargetPoolSize: health.TargetPoolSize,
            Subnet24Diversity: health.Subnet24Diversity,
            ActivePeers: health.ActivePeerKeys));
    }

    /// <summary>Every peer we have seen, with stats. Useful for soak reports.</summary>
    [HttpGet("peers")]
    public async Task<ActionResult<object>> Peers(CancellationToken ct)
    {
        var all = await health.ListAllAsync(ct);
        var snapshot = all
            .OrderByDescending(r => r.LastConnectedUtc ?? DateTime.MinValue)
            .ThenByDescending(r => r.SuccessCount)
            .Select(r => new
            {
                endpoint = r.Key,
                source = r.Source.ToString(),
                userAgent = r.UserAgent,
                protocolVersion = r.ProtocolVersion,
                services = r.Services,
                successCount = r.SuccessCount,
                failCount = r.FailCount,
                firstSeen = r.FirstSeenUtc,
                lastSeen = r.LastSeenUtc,
                lastConnected = r.LastConnectedUtc,
                negativeUntil = r.NegativeUntilUtc,
                lastFailureReason = r.LastFailureReason,
                subnet24 = r.Subnet24,
            })
            .ToList();

        return Ok(new
        {
            total = snapshot.Count,
            successful = snapshot.Count(p => p.successCount > 0),
            failed = snapshot.Count(p => p.failCount > 0),
            distinctSubnets = snapshot.Select(p => p.subnet24).Distinct().Count(),
            peers = snapshot,
        });
    }
}

public sealed record P2pHealthDto(
    bool Bound,
    int PoolSize,
    int TargetPoolSize,
    int Subnet24Diversity,
    System.Collections.Generic.IReadOnlyCollection<string> ActivePeers);
