using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Extensions;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Data.Runtime;

public sealed class ValidationRepairStatusReader(IDocumentStore documentStore) : IValidationRepairStatusReader
{
    public async Task<ValidationRepairStatusResponse> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var totalCount = await session.Query<ValidationRepairWorkItemDocument>()
            .CountAsync(token: cancellationToken);
        var pendingCount = await session.Query<ValidationRepairWorkItemDocument>()
            .CountAsync(x => x.State == ValidationRepairStates.Pending, token: cancellationToken);
        var runningCount = await session.Query<ValidationRepairWorkItemDocument>()
            .CountAsync(x => x.State == ValidationRepairStates.Running, token: cancellationToken);
        var failedCount = await session.Query<ValidationRepairWorkItemDocument>()
            .CountAsync(x => x.State == ValidationRepairStates.Failed, token: cancellationToken);
        var blockedCount = await session.Query<ValidationRepairWorkItemDocument>()
            .CountAsync(x => x.State == ValidationRepairStates.Blocked, token: cancellationToken);
        var resolvedCount = await session.Query<ValidationRepairWorkItemDocument>()
            .CountAsync(x => x.State == ValidationRepairStates.Resolved, token: cancellationToken);

        var items = await session.Query<ValidationRepairWorkItemDocument>()
            .OrderByDescending(x => x.UpdatedAt)
            .Take(20)
            .ToListAsync(token: cancellationToken);

        var oldestUnresolved = await session.Query<ValidationRepairWorkItemDocument>()
            .Where(x => x.State != ValidationRepairStates.Resolved)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(token: cancellationToken);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new ValidationRepairStatusResponse
        {
            TotalCount = totalCount,
            PendingCount = pendingCount,
            RunningCount = runningCount,
            FailedCount = failedCount,
            BlockedCount = blockedCount,
            ResolvedCount = resolvedCount,
            OldestUnresolvedCreatedAt = oldestUnresolved?.CreatedAt,
            OldestUnresolvedAgeSeconds = oldestUnresolved is null ? null : (int?)Math.Max(0, (now - oldestUnresolved.CreatedAt) / 1000),
            Items = items.Select(x => new ValidationRepairItemResponse
            {
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                State = x.State,
                Reasons = x.Reasons,
                MissingDependencies = x.MissingDependencies,
                AttemptCount = x.AttemptCount,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                LastAttemptAt = x.LastAttemptAt,
                NextAttemptAt = x.NextAttemptAt,
                LastError = x.LastError,
                LastFetchedDependencies = x.LastFetchedDependencies
            }).ToArray()
        };
    }
}
