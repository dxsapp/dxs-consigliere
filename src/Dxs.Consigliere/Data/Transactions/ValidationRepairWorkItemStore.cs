#nullable enable
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Data.Transactions;

public sealed class ValidationRepairWorkItemStore(IDocumentStore documentStore) : IValidationRepairWorkItemStore
{
    private const int MaxAttemptsBeforeFailure = 10;

    public async Task<ValidationRepairWorkItemDocument> ScheduleAsync(
        string txId,
        string reason,
        IReadOnlyCollection<string> missingDependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        using var session = documentStore.GetSession();
        var documentId = ValidationRepairWorkItemDocument.GetId(txId);
        var document = await session.LoadAsync<ValidationRepairWorkItemDocument>(documentId, cancellationToken)
            ?? new ValidationRepairWorkItemDocument
            {
                Id = documentId,
                EntityId = txId,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

        document.EntityType = "transaction";
        document.EntityId = txId;
        document.Reasons = document.Reasons
            .Concat([string.IsNullOrWhiteSpace(reason) ? ValidationRepairReasons.MissingParentRepair : reason])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        document.State = ValidationRepairStates.Pending;
        document.MissingDependencies = Normalize(missingDependencies);
        document.NextAttemptAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        document.LastResolvedAt = null;
        document.LastStopReason = null;
        document.LastFetchCount = 0;
        document.LastVisitedCount = 0;
        document.LastTraversalDepth = 0;
        document.SetUpdate();

        await session.StoreAsync(document, document.Id, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task<ValidationRepairWorkItemDocument> MarkRunningAsync(
        string txId,
        IReadOnlyCollection<string> missingDependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        using var session = documentStore.GetSession();
        var document = await session.LoadAsync<ValidationRepairWorkItemDocument>(
            ValidationRepairWorkItemDocument.GetId(txId),
            cancellationToken)
            ?? throw new InvalidOperationException($"Validation repair work item `{txId}` was not found.");

        document.State = ValidationRepairStates.Running;
        document.MissingDependencies = Normalize(missingDependencies);
        document.AttemptCount += 1;
        document.LastAttemptAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        document.LastError = string.Empty;
        document.NextAttemptAt = null;
        document.LastResolvedAt = null;
        document.LastStopReason = null;
        document.SetUpdate();

        await session.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task<ValidationRepairWorkItemDocument?> MarkResolvedAsync(
        string txId,
        ValidationDependencyResolutionResult? resolution = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        using var session = documentStore.GetSession();
        var document = await session.LoadAsync<ValidationRepairWorkItemDocument>(
            ValidationRepairWorkItemDocument.GetId(txId),
            cancellationToken);

        if (document is null)
            return null;

        document.State = ValidationRepairStates.Resolved;
        document.MissingDependencies = [];
        document.LastError = string.Empty;
        document.NextAttemptAt = null;
        document.LastResolvedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ApplyResolution(document, resolution);
        document.SetUpdate();

        await session.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task<ValidationRepairWorkItemDocument?> MarkRetryAsync(
        string txId,
        IReadOnlyCollection<string> missingDependencies,
        string lastError,
        DateTimeOffset nextAttemptAt,
        bool failed,
        ValidationDependencyResolutionResult? resolution = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        using var session = documentStore.GetSession();
        var document = await session.LoadAsync<ValidationRepairWorkItemDocument>(
            ValidationRepairWorkItemDocument.GetId(txId),
            cancellationToken);

        if (document is null)
            return null;

        document.State = failed || document.AttemptCount >= MaxAttemptsBeforeFailure
            ? ValidationRepairStates.Failed
            : ValidationRepairStates.Pending;
        document.MissingDependencies = Normalize(missingDependencies);
        document.LastError = string.IsNullOrWhiteSpace(lastError) ? "validation_dependencies_still_missing" : lastError;
        document.NextAttemptAt = document.State == ValidationRepairStates.Pending
            ? nextAttemptAt.ToUnixTimeMilliseconds()
            : null;
        document.LastResolvedAt = null;
        ApplyResolution(document, resolution);
        document.SetUpdate();

        await session.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task<ValidationRepairWorkItemDocument?> MarkBlockedAsync(
        string txId,
        IReadOnlyCollection<string> missingDependencies,
        string lastError,
        ValidationDependencyResolutionResult? resolution = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        using var session = documentStore.GetSession();
        var document = await session.LoadAsync<ValidationRepairWorkItemDocument>(
            ValidationRepairWorkItemDocument.GetId(txId),
            cancellationToken);

        if (document is null)
            return null;

        document.State = ValidationRepairStates.Blocked;
        document.MissingDependencies = Normalize(missingDependencies);
        document.LastError = string.IsNullOrWhiteSpace(lastError) ? "validation_repair_blocked" : lastError;
        document.NextAttemptAt = null;
        document.LastResolvedAt = null;
        ApplyResolution(document, resolution);
        document.SetUpdate();

        await session.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task<ValidationRepairWorkItemDocument?> LoadAsync(string txId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        using var session = documentStore.GetSession(noTracking: true);
        return await session.LoadAsync<ValidationRepairWorkItemDocument>(ValidationRepairWorkItemDocument.GetId(txId), cancellationToken);
    }

    public async Task<IReadOnlyList<ValidationRepairWorkItemDocument>> LoadDueAsync(int take, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession(noTracking: true);
        var nowMs = now.ToUnixTimeMilliseconds();
        return await session.Query<ValidationRepairWorkItemDocument>()
            .Where(x => x.State == ValidationRepairStates.Pending)
            .Where(x => x.NextAttemptAt == null || x.NextAttemptAt <= nowMs)
            .OrderBy(x => x.NextAttemptAt)
            .ThenBy(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(token: cancellationToken);
    }

    public async Task<IReadOnlyList<ValidationRepairWorkItemDocument>> LoadActiveAsync(int take, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession(noTracking: true);
        return await session.Query<ValidationRepairWorkItemDocument>()
            .Where(x => x.State == ValidationRepairStates.Pending || x.State == ValidationRepairStates.Running || x.State == ValidationRepairStates.Failed || x.State == ValidationRepairStates.Blocked)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(take)
            .ToListAsync(token: cancellationToken);
    }

    public async Task RemoveAsync(string txId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        using var session = documentStore.GetSession();
        var document = await session.LoadAsync<ValidationRepairWorkItemDocument>(
            ValidationRepairWorkItemDocument.GetId(txId),
            cancellationToken);

        if (document is null)
            return;

        session.Delete(document);
        await session.SaveChangesAsync(cancellationToken);
    }

    private static string[] Normalize(IReadOnlyCollection<string> missingDependencies)
        => (missingDependencies ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

    private static void ApplyResolution(
        ValidationRepairWorkItemDocument document,
        ValidationDependencyResolutionResult? resolution)
    {
        if (resolution is null)
            return;

        document.LastFetchedDependencies = resolution.FetchedDependencies
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        document.LastStopReason = resolution.StopReason;
        document.LastFetchCount = Math.Max(0, resolution.FetchCount);
        document.LastVisitedCount = Math.Max(0, resolution.VisitedCount);
        document.LastTraversalDepth = Math.Max(0, resolution.MaxTraversalDepth);
    }
}
