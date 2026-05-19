using System.Security.Claims;
using System.Text.Json;
using Dxs.Consigliere.Data.Models.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations.Expiration;

namespace Dxs.Consigliere.Services.Audit;

/// <summary>
/// wave-A3 S3 — Raven-backed audit logger. Each
/// <c>RecordAsync</c> call opens a short-lived session, stores
/// the entry with a 365-day <c>@expires</c> metadata header,
/// and saves. Failure surfaces as <c>false</c> so callers can
/// fail-stop. A logger-level error is also emitted so the
/// operator notices regardless of the upstream's behaviour.
///
/// The expiration bundle (<see cref="ConfigureExpirationOperation"/>)
/// is enabled lazily on the first write — older operator
/// installs may not have it on by default. Subsequent writes
/// short-circuit via a per-process flag.
/// </summary>
public sealed class AuditLogger(
    IDocumentStore documentStore,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditLogger> logger) : IAuditLogger
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(365);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private int _expirationConfigured;

    public async Task<bool> RecordAsync(
        string action,
        string targetId,
        object context,
        string username = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("action is required", nameof(action));

        username ??= ResolveUsername();
        var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var entry = new AuditLogEntry
        {
            Id = AuditLogEntry.IdPrefix + unixMs + "/" + Guid.NewGuid().ToString("N")[..8],
            UnixMs = unixMs,
            Username = username ?? "anonymous",
            Action = action,
            TargetId = targetId ?? string.Empty,
            Context = context is null ? null : JsonSerializer.Serialize(context, JsonOptions),
        };

        try
        {
            await EnsureExpirationConfiguredAsync(cancellationToken);

            using var session = documentStore.OpenAsyncSession();
            await session.StoreAsync(entry, entry.Id, cancellationToken);
            var metadata = session.Advanced.GetMetadataFor(entry);
            metadata[Raven.Client.Constants.Documents.Metadata.Expires] = DateTime.UtcNow.Add(Retention);
            await session.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[audit] failed to record {Action} on {TargetId} by {Username}", action, targetId, username);
            return false;
        }
    }

    private string ResolveUsername()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var name = user.FindFirstValue(ClaimTypes.Name);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        return null;
    }

    private async Task EnsureExpirationConfiguredAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _expirationConfigured, 1, 0) != 0)
            return;
        try
        {
            await documentStore.Maintenance.SendAsync(
                new ConfigureExpirationOperation(new ExpirationConfiguration
                {
                    Disabled = false,
                    DeleteFrequencyInSec = 60,
                }),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Re-arm so a future call retries — better to try
            // again than to silently drop the bundle.
            Interlocked.Exchange(ref _expirationConfigured, 0);
            logger.LogWarning(ex, "[audit] could not enable Raven expiration bundle; entries will accumulate until configured");
        }
    }
}
