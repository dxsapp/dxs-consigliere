#nullable enable
using System.Net;

using Dxs.Bsv.Models;
using Dxs.Common.Exceptions;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;
using Dxs.Infrastructure.Common;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Services.Impl;

public sealed class ValidationDependencyService(
    IDocumentStore documentStore,
    IUpstreamTransactionAcquisitionService acquisitionService,
    IMetaTransactionStore transactionStore,
    INetworkProvider networkProvider,
    ILogger<ValidationDependencyService> logger
) : IValidationDependencyService
{
    private const int MaxFetchesPerRepair = 250;
    private const int MaxTraversalDepth = 128;

    public async Task<ValidationDependencyResolutionResult> ResolveAsync(
        string entityId,
        IReadOnlyList<string> missingDependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var fetched = new HashSet<string>(StringComparer.Ordinal);
        var remaining = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new Stack<TraversalCandidate>(
            Normalize(missingDependencies)
                .Reverse()
                .Select(x => new TraversalCandidate(x, 1)));

        string? lastError = null;
        string? stopReason = null;
        var fetchCount = 0;
        var maxDepthReached = 0;

        while (frontier.Count > 0)
        {
            var current = frontier.Pop();
            maxDepthReached = Math.Max(maxDepthReached, current.Depth);

            if (!visited.Add(current.TxId))
            {
                stopReason ??= ValidationRepairStopReasons.AlreadyVisited;
                continue;
            }

            if (current.Depth > MaxTraversalDepth || fetchCount >= MaxFetchesPerRepair)
            {
                remaining.Add(current.TxId);
                remaining.UnionWith(frontier.Select(x => x.TxId));
                stopReason = ValidationRepairStopReasons.BudgetExceeded;
                break;
            }

            try
            {
                var result = await acquisitionService.TryGetAsync(current.TxId, ExternalChainCapability.ValidationFetch, cancellationToken);
                if (result?.Raw is not { Length: > 0 } raw)
                {
                    remaining.Add(current.TxId);
                    stopReason ??= ValidationRepairStopReasons.MissingDependency;
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

                fetchCount += 1;
                fetched.Add(current.TxId);
                remaining.Remove(current.TxId);

                var meta = await LoadMetaTransactionAsync(current.TxId, cancellationToken);
                if (meta is null)
                {
                    remaining.Add(current.TxId);
                    stopReason ??= ValidationRepairStopReasons.MissingDependency;
                    continue;
                }

                var branchStopReason = GetBranchStopReason(meta);
                if (!string.IsNullOrWhiteSpace(branchStopReason))
                {
                    stopReason = branchStopReason;
                    continue;
                }

                var nextDependencies = Normalize(meta.MissingTransactions);
                if (nextDependencies.Length == 0)
                    continue;

                foreach (var dependencyTxId in nextDependencies.Reverse())
                {
                    if (visited.Contains(dependencyTxId))
                    {
                        stopReason ??= ValidationRepairStopReasons.AlreadyVisited;
                        continue;
                    }

                    remaining.Add(dependencyTxId);
                    frontier.Push(new TraversalCandidate(dependencyTxId, current.Depth + 1));
                }
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                remaining.Add(current.TxId);
                remaining.UnionWith(frontier.Select(x => x.TxId));
                stopReason = GetProviderStopReason(exception);
                logger.LogWarning(
                    exception,
                    "Validation dependency reverse-lineage fetch failed for {DependencyTxId} while repairing {EntityId}",
                    current.TxId,
                    entityId);
                break;
            }
        }

        return new ValidationDependencyResolutionResult(
            fetched.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            remaining.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            lastError,
            stopReason,
            fetchCount,
            visited.Count,
            maxDepthReached);
    }

    private async Task<MetaTransaction?> LoadMetaTransactionAsync(string txId, CancellationToken cancellationToken)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        return await session.LoadAsync<MetaTransaction>(txId, cancellationToken);
    }

    private static string? GetBranchStopReason(MetaTransaction transaction)
    {
        if ((transaction.IllegalRoots?.Count ?? 0) > 0)
            return ValidationRepairStopReasons.IllegalRootFound;

        if (transaction.IsIssue && transaction.IsValidIssue)
            return ValidationRepairStopReasons.ValidIssueReached;

        return null;
    }

    private static string GetProviderStopReason(Exception exception)
    {
        if (exception is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests })
            return ValidationRepairStopReasons.ProviderRateLimited;

        if (exception is DetailedHttpRequestException { StatusCode: HttpStatusCode.TooManyRequests })
            return ValidationRepairStopReasons.ProviderRateLimited;

        return ValidationRepairStopReasons.ProviderError;
    }

    private static string[] Normalize(IEnumerable<string>? txIds) =>
        (txIds ?? [])
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(x => x, StringComparer.Ordinal)
        .ToArray();

    private readonly record struct TraversalCandidate(string TxId, int Depth);
}
