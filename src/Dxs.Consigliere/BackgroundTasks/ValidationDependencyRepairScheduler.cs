using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.BackgroundTasks;

public sealed class ValidationDependencyRepairScheduler(
    IDocumentStore documentStore,
    ITokenValidationDependencyStore dependencyStore,
    IValidationRepairWorkItemStore workItemStore
) : IValidationDependencyRepairScheduler
{
    public async Task<ValidationRepairWorkItemDocument?> ScheduleTransactionAsync(
        string txId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        using var session = documentStore.GetNoCacheNoTrackingSession();
        var transaction = await session.LoadAsync<MetaTransaction>(txId, cancellationToken);
        if (transaction is null)
        {
            await workItemStore.MarkRetryAsync(
                txId,
                [],
                "transaction_not_found",
                DateTimeOffset.UtcNow,
                failed: true,
                cancellationToken);
            return null;
        }

        if (!transaction.IsStas)
        {
            await workItemStore.MarkResolvedAsync(txId, cancellationToken);
            return null;
        }

        await dependencyStore.UpsertAsync(transaction, cancellationToken);

        var missingDependencies = (transaction.MissingTransactions ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        if (missingDependencies.Length == 0)
        {
            await workItemStore.MarkResolvedAsync(txId, cancellationToken);
            return null;
        }

        foreach (var dependencyTxId in missingDependencies)
            await documentStore.AddOrUpdateEntity(new MissingTransaction { TxId = dependencyTxId });

        return await workItemStore.ScheduleAsync(txId, reason, missingDependencies, cancellationToken);
    }

    public Task<ValidationRepairWorkItemDocument?> GetScheduledTransactionAsync(
        string txId,
        CancellationToken cancellationToken = default)
        => workItemStore.LoadAsync(txId, cancellationToken);

    public async Task CancelTransactionAsync(
        string txId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);
        await workItemStore.MarkResolvedAsync(txId, cancellationToken);
    }
}
