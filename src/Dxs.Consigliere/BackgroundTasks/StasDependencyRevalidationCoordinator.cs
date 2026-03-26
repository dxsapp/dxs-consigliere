using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;

using Raven.Client.Documents;

namespace Dxs.Consigliere.BackgroundTasks;

public sealed class StasDependencyRevalidationCoordinator(
    IDocumentStore documentStore,
    IMetaTransactionStore transactionStore,
    ILogger logger
)
{
    private readonly ITokenValidationDependencyStore _dependencyStore = new TokenValidationDependencyStore(documentStore);

    public async Task HandleTransactionChangedAsync(string txId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        using var session = documentStore.GetNoCacheNoTrackingSession();
        var transaction = await session.LoadAsync<MetaTransaction>(txId, cancellationToken);

        if (transaction is null)
        {
            await HandleTransactionDeletedAsync(txId, cancellationToken);
            return;
        }

        await _dependencyStore.UpsertAsync(transaction, cancellationToken);

        var dependents = await _dependencyStore.LoadDirectDependentsAsync(txId, cancellationToken);
        if (dependents.Count == 0)
            return;

        logger.LogInformation(
            "Revalidating {Count} direct dependents after transaction update: {TxId}",
            dependents.Count,
            txId
        );

        foreach (var dependentTxId in dependents.Where(x => !string.Equals(x, txId, StringComparison.Ordinal)))
            await transactionStore.UpdateStasAttributes(dependentTxId);
    }

    public async Task HandleTransactionDeletedAsync(string txId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(txId);

        var dependents = await _dependencyStore.LoadDirectDependentsAsync(txId, cancellationToken);
        await _dependencyStore.RemoveAsync(txId, cancellationToken);

        if (dependents.Count == 0)
            return;

        logger.LogInformation(
            "Revalidating {Count} direct dependents after transaction deletion: {TxId}",
            dependents.Count,
            txId
        );

        foreach (var dependentTxId in dependents.Where(x => !string.Equals(x, txId, StringComparison.Ordinal)))
            await transactionStore.UpdateStasAttributes(dependentTxId);
    }
}
