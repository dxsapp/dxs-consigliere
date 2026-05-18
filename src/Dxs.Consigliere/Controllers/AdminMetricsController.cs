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
    /// <c>lastN</c>. Defaults to the configured retention count
    /// (which is itself bounded by <see cref="SourceMetricsConfig"/>);
    /// falls back to <see cref="HardLastNCeiling"/> if retention is
    /// unconfigured. Documented for W6 + external consumers.
    /// </summary>
    private const int HardLastNCeiling = 1440;

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
        if (lastN is > 0)
        {
            // A2 M2 fix: clamp the caller's requested window. Cap at
            // the configured retention count (or the hard ceiling if
            // retention is unconfigured / extreme) so a single call
            // can never scan more than what's actually retained.
            var ceiling = _config.SnapshotRetentionCount > 0
                ? _config.SnapshotRetentionCount
                : HardLastNCeiling;
            var bounded = System.Math.Min(lastN.Value, System.Math.Min(ceiling, HardLastNCeiling));

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
    SourceMetricsSnapshot Latest,
    IReadOnlyList<SourceMetricsSnapshot> History);
