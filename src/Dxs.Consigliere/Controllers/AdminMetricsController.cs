#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Metrics;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Setup;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Controllers;

/// <summary>
/// Wave 4 S5 — admin REST surface for the source-metrics snapshots
/// persisted by <see cref="Services.Metrics.SourceMetricsAggregator"/>.
/// Returns the latest snapshot (current state) and optionally the
/// last N historical snapshots (oldest-last) for trend rendering.
///
/// Frozen by W4 for W6 alarm rules + future external exporters.
/// </summary>
[ApiController]
[Route("api/admin/metrics")]
[Authorize(Policy = AdminAuthDefaults.Policy)]
public sealed class AdminMetricsController : ControllerBase
{
    /// <summary>
    /// A2 M2 cap: the endpoint refuses to scan more snapshots than
    /// this in a single response, regardless of any caller-provided
    /// <c>lastN</c>. 1440 = 12 hours @ 30 s snapshots, or 24 hours
    /// @ 60 s snapshots — a generous-but-bounded ceiling for any
    /// realistic dashboard use case. Defaults to the configured
    /// retention count (which is itself bounded by
    /// <see cref="SourceMetricsConfig"/>); falls back to this hard
    /// ceiling if retention is unconfigured. Documented for W6 +
    /// external consumers.
    /// </summary>
    internal const int HardLastNCeiling = 1440;

    /// <summary>
    /// A2-followup N2 fix: clamp logic extracted to a testable
    /// helper. Returns 0 when the caller requested no history (or a
    /// non-positive value); otherwise returns
    /// <c>min(requestedLastN, min(ceiling, HardLastNCeiling))</c>
    /// where <c>ceiling = retention &gt; 0 ? retention :
    /// HardLastNCeiling</c>.
    /// </summary>
    internal static int ClampLastN(int? requestedLastN, int retentionCount)
    {
        if (requestedLastN is not int n || n <= 0) return 0;
        var ceiling = retentionCount > 0 ? retentionCount : HardLastNCeiling;
        return Math.Min(n, Math.Min(ceiling, HardLastNCeiling));
    }

    private readonly IDocumentStore _documentStore;
    private readonly SourceMetricsConfig _config;

    public AdminMetricsController(IDocumentStore documentStore, IOptions<SourceMetricsConfig> config)
    {
        _documentStore = documentStore;
        _config = config.Value;
    }

    /// <summary>
    /// <c>GET /api/admin/metrics/sources</c> — returns the latest
    /// snapshot (or <c>null</c> if the aggregator hasn't written
    /// any yet). When <paramref name="lastN"/> is provided and
    /// &gt;= 1, also returns up to that many historical snapshots
    /// in oldest-first order.
    /// </summary>
    [HttpGet("sources")]
    public async Task<ActionResult<SourceMetricsResponse>> GetSourceMetrics(
        [FromQuery] int? lastN,
        CancellationToken cancellationToken)
    {
        using var session = _documentStore.GetNoCacheNoTrackingSession();

        // Latest = the highest-id snapshot (the BuildId format is
        // zero-padded so lex-order equals numeric-order).
        var latest = await session
            .Query<SourceMetricsSnapshot>()
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(token: cancellationToken);

        IReadOnlyList<SourceMetricsSnapshot> history = [];
        var bounded = ClampLastN(lastN, _config.SnapshotRetentionCount);
        if (bounded > 0)
        {
            // Take the most-recent N (bounded), then reverse to deliver
            // oldest-first for natural time-series rendering on the
            // SPA client.
            var window = await session
                .Query<SourceMetricsSnapshot>()
                .OrderByDescending(x => x.Id)
                .Take(bounded)
                .ToListAsync(token: cancellationToken);
            history = window.AsEnumerable().Reverse().ToList();
        }

        return Ok(new SourceMetricsResponse(latest, history));
    }
}

/// <summary>
/// Response DTO for <c>GET /api/admin/metrics/sources</c>. Frozen
/// for W6 + external consumers.
/// </summary>
public sealed record SourceMetricsResponse(
    // Null until the aggregator has written its first snapshot.
    SourceMetricsSnapshot? Latest,
    IReadOnlyList<SourceMetricsSnapshot> History);
