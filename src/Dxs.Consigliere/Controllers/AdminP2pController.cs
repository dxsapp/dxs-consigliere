using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Chain;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.P2p;
using Dxs.Consigliere.Setup;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Controllers;

/// <summary>
/// Diagnostic surface for the Gate 2 BSV thin-node P2p subsystem.
/// Read-only; used by operators during the 24h soak test to track
/// peer-acceptance rate, ASN diversity, and per-peer state.
/// </summary>
[ApiController]
[Route("api/admin/p2p")]
[Authorize(Policy = AdminAuthDefaults.Policy)]
public class AdminP2pController(
    BsvP2pHealth health,
    BlockHeaderStore headerStore,
    IOptions<HeadersChainOptions> headersOptions,
    IAlertEventRepository alertRepository,
    IOptions<BsvP2pConfig> p2pOptions)
    : ControllerBase
{
    private readonly HeadersChainOptions _headersOptions = headersOptions.Value;
    private readonly AlertConfig _alertConfig = p2pOptions.Value.Alert;

    /// <summary>
    /// Wave 6 S3 (master.md A1-followup) — default page size for
    /// <c>GET /api/admin/p2p/alerts</c> when <c>lastN</c> is omitted.
    /// </summary>
    internal const int DefaultAlertLastN = 20;

    /// <summary>
    /// Wave 6 S3 — hard ceiling on the alert-history scan,
    /// regardless of caller-provided <c>lastN</c> or the configured
    /// retention. Mirrors the W5 A2 M2 / W4 metrics-controller
    /// pattern. 1440 = 24 h at 60 s cadence; bounded for any
    /// realistic dashboard.
    /// </summary>
    internal const int HardAlertLastNCeiling = 1440;

    /// <summary>
    /// Wave 6 S3 (W5 A2 M2 pattern) — clamp the caller-provided
    /// <c>lastN</c> against the alert retention budget and the
    /// hard ceiling. Negative / zero falls back to
    /// <see cref="DefaultAlertLastN"/>; positive values are
    /// upper-bounded by <c>min(retention, HardAlertLastNCeiling)</c>.
    /// </summary>
    internal static int ClampAlertLastN(int? requestedLastN, int retentionCount)
    {
        var requested = requestedLastN is int n && n > 0 ? n : DefaultAlertLastN;
        var ceiling = retentionCount > 0 ? retentionCount : HardAlertLastNCeiling;
        return Math.Min(requested, Math.Min(ceiling, HardAlertLastNCeiling));
    }
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

    /// <summary>
    /// Wave 1 S6 — current P2P chain tip. Returns 404 when the headers
    /// chain has not yet been populated (cold start before bootstrap or
    /// first inv/headers).
    ///
    /// Hash and PrevHash are returned in **display order** (audit A2 H3),
    /// matching what WhatsOnChain / Bitails / explorers show. Raven
    /// documents store wire-order internally; conversion happens here at
    /// the API boundary.
    /// </summary>
    [HttpGet("headers/tip")]
    public async Task<ActionResult<HeadersTipDto>> HeadersTip(CancellationToken ct)
    {
        var doc = await headerStore.GetTipAsync(ct);
        if (doc is null) return NotFound();
        return Ok(ToDisplayDto(doc));
    }

    /// <summary>
    /// Wave 1 S6 — most recent N headers (tip first, height-descending).
    /// Count is clamped to <see cref="HeadersChainOptions.RetainedHeaderCount"/>;
    /// values &lt;= 0 return an empty array. Same display-order convention
    /// as <c>headers/tip</c>.
    /// </summary>
    [HttpGet("headers/recent")]
    public async Task<ActionResult<HeadersTipDto[]>> HeadersRecent([FromQuery] int count, CancellationToken ct)
    {
        if (count <= 0) return Ok(Array.Empty<HeadersTipDto>());
        var clamped = Math.Min(count, _headersOptions.RetainedHeaderCount);
        var docs = await headerStore.RecentAsync(clamped, ct);
        var result = docs.Select(ToDisplayDto).ToArray();
        return Ok(result);
    }

    /// <summary>
    /// Wave 6 S3 — <c>GET /api/admin/p2p/alerts</c>: latest N
    /// alert events written by the W6 alert poller, newest-first.
    /// <paramref name="lastN"/> defaults to
    /// <see cref="DefaultAlertLastN"/> when omitted; clamped to
    /// the configured retention + the hard ceiling.
    /// <paramref name="since"/> (Unix ms) filters for alerts fired
    /// strictly after that timestamp — incremental polling for
    /// future dashboards.
    /// </summary>
    [HttpGet("alerts")]
    public async Task<ActionResult<P2pAlertResponse>> GetAlerts(
        [FromQuery] int? lastN,
        [FromQuery] long? since,
        CancellationToken ct)
    {
        var bounded = ClampAlertLastN(lastN, _alertConfig.AlertRetentionEvents);
        var events = await alertRepository.GetRecentAsync(bounded, since, ct);
        var dtos = events.Select(ToDto).ToList();
        return Ok(new P2pAlertResponse(dtos));
    }

    private static P2pAlertEventDto ToDto(P2pAlertEvent e) =>
        new(
            Id: e.Id,
            AlertUnixMs: e.AlertUnixMs,
            Type: e.Type.ToString(),
            Detail: e.Detail,
            Context: e.Context);

    private static HeadersTipDto ToDisplayDto(Data.Models.P2p.BlockHeaderDocument doc)
    {
        // doc.Hash / doc.PrevHash are wire-order hex; convert each to
        // display-order for the response.
        var hashDisplay = BlockHeaderHasher.ToDisplayHex(Convert.FromHexString(doc.Hash));
        var prevDisplay = string.IsNullOrEmpty(doc.PrevHash)
            ? doc.PrevHash
            : BlockHeaderHasher.ToDisplayHex(Convert.FromHexString(doc.PrevHash));
        return new HeadersTipDto(hashDisplay, doc.Height, doc.TimestampMs, prevDisplay);
    }
}

public sealed record P2pHealthDto(
    bool Bound,
    int PoolSize,
    int TargetPoolSize,
    int Subnet24Diversity,
    System.Collections.Generic.IReadOnlyCollection<string> ActivePeers);

public sealed record HeadersTipDto(
    string Hash,
    long Height,
    long TimestampMs,
    string PrevHash);

/// <summary>
/// Wave 6 S3 — frozen response shape for
/// <c>GET /api/admin/p2p/alerts</c>. Renames require a contract-
/// amendment slice per master.md handoff table.
/// </summary>
public sealed record P2pAlertResponse(IReadOnlyList<P2pAlertEventDto> Alerts);

/// <summary>
/// Wave 6 S3 — wire shape for a single alert. <c>Type</c> is the
/// <see cref="P2pAlertType"/> string name so a future enum addition
/// is forwards-compatible at the JSON layer.
/// </summary>
public sealed record P2pAlertEventDto(
    string Id,
    long AlertUnixMs,
    string Type,
    string Detail,
    IDictionary<string, string> Context);
