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
/// The Raven expiration bundle is enabled lazily on the first
/// write via <see cref="IAuditRetentionConfigurator"/> — older
/// operator installs may not have it on by default. S3-audit
/// M1 fix: bundle-enable failure is fail-stop (re-thrown by
/// the configurator and surfaced as <c>false</c>) so a
/// destructive broadcast cannot proceed without a retention
/// guarantee. The configurator re-arms its internal flag on
/// failure so the very next call retries.
/// </summary>
public sealed class AuditLogger(
    IDocumentStore documentStore,
    IAuditRetentionConfigurator retentionConfigurator,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditLogger> logger) : IAuditLogger
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(365);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            await retentionConfigurator.EnsureConfiguredAsync(cancellationToken);

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
}

/// <summary>
/// wave-A3 S3-audit M1: indirection so the audit logger's
/// fail-stop on Raven-expiration-bundle failure can be unit
/// tested without standing up a real
/// <see cref="Raven.Client.Documents.Operations.MaintenanceOperationExecutor"/>
/// (which is a sealed framework type). Production binds to
/// <see cref="RavenAuditRetentionConfigurator"/>; tests inject
/// a stub.
/// </summary>
public interface IAuditRetentionConfigurator
{
    Task EnsureConfiguredAsync(CancellationToken cancellationToken);
}

public sealed class RavenAuditRetentionConfigurator(IDocumentStore documentStore) : IAuditRetentionConfigurator
{
    private int _configured;

    public async Task EnsureConfiguredAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _configured, 1, 0) != 0)
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
        catch
        {
            // Re-arm so a future call retries — bundle-enable
            // failures are typically transient. Rethrow so the
            // upstream `AuditLogger.RecordAsync` returns `false`
            // and the destructive operation aborts.
            Interlocked.Exchange(ref _configured, 0);
            throw;
        }
    }
}
