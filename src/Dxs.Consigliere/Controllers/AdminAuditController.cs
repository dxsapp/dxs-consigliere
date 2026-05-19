using Dxs.Consigliere.Data.Models.Audit;
using Dxs.Consigliere.Dto.Responses.Admin;
using Dxs.Consigliere.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Controllers;

/// <summary>
/// wave-A3 S3 — read-only audit log surface. No mutate / no
/// delete endpoints — the slice contract treats audit records
/// as append-only forensic state.
/// </summary>
[Route("api/admin/audit-log")]
[Authorize(Policy = AdminAuthDefaults.Policy)]
public sealed class AdminAuditController(IDocumentStore documentStore) : BaseController
{
    /// <summary>
    /// Hard ceiling on the number of entries a single response
    /// can return. Mirrors the wave-A1 alerts endpoint clamp.
    /// </summary>
    internal const int HardLastNCeiling = 1000;

    [HttpGet]
    [Produces(typeof(AdminAuditLogResponse))]
    public async Task<IActionResult> Get(
        [FromQuery] long? since,
        [FromQuery] string action,
        [FromQuery] string username,
        [FromQuery] int? lastN,
        CancellationToken cancellationToken)
    {
        var take = ClampLastN(lastN);
        using var session = documentStore.OpenAsyncSession();
        var query = session.Query<AuditLogEntry>();
        if (since is long s && s > 0)
            query = query.Where(x => x.UnixMs >= s);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(x => x.Action == action);
        if (!string.IsNullOrWhiteSpace(username))
            query = query.Where(x => x.Username == username);

        var ordered = query.OrderByDescending(x => x.UnixMs);
        var totalMatched = await ordered.CountAsync(cancellationToken);
        var page = take == 0
            ? []
            : await ordered.Take(take).ToArrayAsync(cancellationToken);

        return Ok(new AdminAuditLogResponse
        {
            TotalMatched = totalMatched,
            Entries = page.Select(MapEntry).ToArray(),
        });
    }

    internal static int ClampLastN(int? requested)
    {
        if (requested is not int n || n <= 0) return HardLastNCeiling;
        return Math.Min(n, HardLastNCeiling);
    }

    private static AdminAuditLogEntryResponse MapEntry(AuditLogEntry entry) => new()
    {
        Id = entry.Id,
        UnixMs = entry.UnixMs,
        Username = entry.Username,
        Action = entry.Action,
        TargetId = entry.TargetId,
        Context = entry.Context,
    };
}
