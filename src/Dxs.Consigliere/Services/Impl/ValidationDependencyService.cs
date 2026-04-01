using Dxs.Bsv.Models;
using Dxs.Consigliere.Configs;
using Dxs.Infrastructure.Common;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Services.Impl;

public sealed class ValidationDependencyService(
    IUpstreamTransactionAcquisitionService acquisitionService,
    IMetaTransactionStore transactionStore,
    INetworkProvider networkProvider,
    ILogger<ValidationDependencyService> logger
) : IValidationDependencyService
{
    public async Task<ValidationDependencyResolutionResult> ResolveAsync(
        string entityId,
        IReadOnlyList<string> missingDependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        var fetched = new List<string>();
        var remaining = new List<string>();
        string? lastError = null;

        foreach (var dependencyTxId in missingDependencies.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
        {
            try
            {
                var result = await acquisitionService.TryGetAsync(dependencyTxId, ExternalChainCapability.ValidationFetch, cancellationToken);
                if (result?.Raw is not { Length: > 0 } raw)
                {
                    remaining.Add(dependencyTxId);
                    continue;
                }

                var transaction = Transaction.Parse(raw, networkProvider.Network);
                await transactionStore.SaveTransaction(
                    transaction,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    null,
                    null,
                    null,
                    null);
                fetched.Add(dependencyTxId);
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                remaining.Add(dependencyTxId);
                logger.LogWarning(exception, "Validation dependency acquisition failed for {DependencyTxId} while repairing {EntityId}", dependencyTxId, entityId);
            }
        }

        return new ValidationDependencyResolutionResult(
            fetched.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            remaining.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            lastError);
    }
}
