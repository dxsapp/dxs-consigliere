using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;

using Raven.Client.Documents;

namespace Dxs.Consigliere.BackgroundTasks;

public sealed class ValidationDependencyRepairProcessor(
    IDocumentStore documentStore,
    IValidationRepairWorkItemStore workItemStore,
    IValidationDependencyService validationDependencyService,
    IMetaTransactionStore transactionStore,
    IStasDependencyRevalidationCoordinator coordinator,
    ILogger<ValidationDependencyRepairProcessor> logger
) : IValidationDependencyRepairProcessor
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 5;

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var dueItems = await workItemStore.LoadDueAsync(BatchSize, DateTimeOffset.UtcNow, cancellationToken);
        foreach (var item in dueItems)
            await ProcessAsync(item, cancellationToken);

        return dueItems.Count;
    }

    private async Task ProcessAsync(ValidationRepairWorkItemDocument item, CancellationToken cancellationToken)
    {
        var txId = item.EntityId;

        using var session = documentStore.GetNoCacheNoTrackingSession();
        var target = await session.LoadAsync<MetaTransaction>(txId, cancellationToken);
        if (target is null || !target.IsStas)
        {
            if (target is null)
            {
                await workItemStore.MarkBlockedAsync(
                    txId,
                    item.MissingDependencies,
                    "target_transaction_missing",
                    cancellationToken: cancellationToken);
            }
            else
            {
                await workItemStore.MarkResolvedAsync(txId, cancellationToken: cancellationToken);
            }
            return;
        }

        var currentMissing = (target.MissingTransactions ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        if (currentMissing.Length == 0)
        {
            await workItemStore.MarkResolvedAsync(txId, cancellationToken: cancellationToken);
            return;
        }

        await workItemStore.MarkRunningAsync(txId, currentMissing, cancellationToken);

        var resolution = await validationDependencyService.ResolveAsync(txId, currentMissing, cancellationToken);

        await transactionStore.UpdateStasAttributes(txId);
        foreach (var fetchedDependency in resolution.FetchedDependencies)
            await coordinator.HandleTransactionChangedAsync(fetchedDependency, cancellationToken);

        using var refreshSession = documentStore.GetNoCacheNoTrackingSession();
        var refreshed = await refreshSession.LoadAsync<MetaTransaction>(txId, cancellationToken);
        var remainingMissing = refreshed is null
            ? []
            : (refreshed.MissingTransactions ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

        if (refreshed is not null && remainingMissing.Length == 0)
        {
            var resolvedStopReason = resolution.StopReason;
            if (string.IsNullOrWhiteSpace(resolvedStopReason))
            {
                resolvedStopReason = (refreshed.IllegalRoots?.Count ?? 0) > 0
                    ? ValidationRepairStopReasons.IllegalRootFound
                    : ValidationRepairStopReasons.ValidIssueReached;
            }

            await workItemStore.MarkResolvedAsync(
                txId,
                resolution with { StopReason = resolvedStopReason },
                cancellationToken);
            return;
        }

        var terminalFailure = item.AttemptCount >= MaxAttempts;
        var error = !string.IsNullOrWhiteSpace(resolution.LastError)
            ? resolution.LastError
            : resolution.FetchedDependencies.Count == 0
                ? "dependencies_still_missing"
                : "partial_dependency_resolution";
        await workItemStore.MarkRetryAsync(
            txId,
            remainingMissing,
            error,
            DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 15 * Math.Max(1, item.AttemptCount))),
            terminalFailure,
            resolution,
            cancellationToken);
    }
}
