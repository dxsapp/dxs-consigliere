using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.Metrics;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Setup;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    private readonly IDocumentStore _documentStore;

    public AdminMetricsController(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
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
            // Take the most-recent N, then reverse to deliver
            // oldest-first for natural time-series rendering on the
            // SPA client.
            var window = await session
                .Query<SourceMetricsSnapshot>()
                .OrderByDescending(x => x.Id)
                .Take(lastN.Value)
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
