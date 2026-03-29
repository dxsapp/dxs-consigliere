using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Tracking.HistoryBackfill;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Data.Tokens;
using Dxs.Consigliere.Extensions;
using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Options;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Services.Impl;

public sealed class HistoricalTokenBackfillRunner(
    IDocumentStore documentStore,
    IBitailsRestApiClient bitailsRestApiClient,
    ITxMessageBus txMessageBus,
    INetworkProvider networkProvider,
    IAdminProviderConfigService providerConfigService,
    IOptions<AppConfig> legacyConfig,
    IExternalChainProviderCatalog providerCatalog,
    ITrackedEntityLifecycleOrchestrator lifecycleOrchestrator,
    ILogger<HistoricalTokenBackfillRunner> logger
)
{
    private const int PageLimit = 50;

    public async Task<bool> RunNextAsync(CancellationToken cancellationToken = default)
    {
        TokenHistoryBackfillJobDocument job;
        using (var readJobSession = documentStore.GetSession())
        {
            job = await readJobSession.Query<TokenHistoryBackfillJobDocument>()
                .Where(x => x.Status == HistoryBackfillExecutionStatus.Queued
                    || x.Status == HistoryBackfillExecutionStatus.Running
                    || x.Status == HistoryBackfillExecutionStatus.WaitingRetry)
                .OrderBy(x => x.RequestedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (job is null)
            return false;

        var effectiveSources = await providerConfigService.GetEffectiveSourcesConfigAsync(cancellationToken);
        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.HistoricalTokenScan,
            effectiveSources,
            legacyConfig.Value,
            providerCatalog);
        var provider = SourceCapabilityRouting.SelectForAttempt(route, job.AttemptCount);
        if (!string.Equals(provider, ExternalChainProviderName.Bitails, StringComparison.OrdinalIgnoreCase))
        {
            await MarkFailedAsync(job.TokenId, job.Id, "historical_token_scan_unavailable", cancellationToken);
            return true;
        }

        await lifecycleOrchestrator.MarkTokenHistoryBackfillRunningAsync(job.TokenId, cancellationToken);

        using var updateJobSession = documentStore.GetSession();
        var liveJob = await updateJobSession.LoadAsync<TokenHistoryBackfillJobDocument>(job.Id, cancellationToken);
        if (liveJob is null)
            return true;

        liveJob.Status = HistoryBackfillExecutionStatus.Running;
        liveJob.Provider = provider;
        liveJob.SourceCapability = ExternalChainCapability.HistoricalTokenScan;
        liveJob.StartedAt ??= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        liveJob.LastProgressAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        liveJob.AttemptCount += 1;
        liveJob.Payload ??= new TokenHistoryBackfillPayload();
        liveJob.Payload.TrustedRoots = TrackedTokenRootedHistoryEvaluator.Normalize(liveJob.Payload.TrustedRoots);
        liveJob.SetUpdate();
        await updateJobSession.SaveChangesAsync(cancellationToken);

        try
        {
            using var readSession = documentStore.GetNoCacheNoTrackingSession();
            var status = await readSession.LoadAsync<TrackedTokenStatusDocument>(
                TrackedTokenStatusDocument.GetId(job.TokenId),
                cancellationToken);
            var trustedRoots = TrackedTokenRootedHistoryEvaluator.Normalize(
                liveJob.Payload.TrustedRoots.Length > 0
                    ? liveJob.Payload.TrustedRoots
                    : status?.HistorySecurity?.TrustedRoots);
            if (trustedRoots.Length == 0)
            {
                await MarkFailedAsync(job.TokenId, job.Id, "trusted_roots_required", cancellationToken);
                return true;
            }

            var transactions = await readSession.Query<MetaTransaction>()
                .Where(x => x.TokenIds.Contains(job.TokenId))
                .ToListAsync(cancellationToken);
            var evaluation = TrackedTokenRootedHistoryEvaluator.Evaluate(job.TokenId, trustedRoots, transactions);
            await lifecycleOrchestrator.UpdateTokenHistorySecurityAsync(job.TokenId, new TrackedTokenHistorySecurityState
            {
                TrustedRoots = evaluation.TrustedRoots,
                CompletedTrustedRootCount = evaluation.CompletedTrustedRootCount,
                UnknownRootFindings = evaluation.UnknownRoots,
                RootedHistorySecure = evaluation.RootedHistorySecure,
                BlockingUnknownRoot = evaluation.BlockingUnknownRoot
            }, cancellationToken);

            var missingTrustedRoot = trustedRoots.FirstOrDefault(rootId =>
                !transactions.Any(x => string.Equals(x.Id, rootId, StringComparison.OrdinalIgnoreCase))
                && !(liveJob.Payload.HydratedRoots ?? []).Contains(rootId, StringComparer.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(missingTrustedRoot))
            {
                await HydrateTransactionAsync(missingTrustedRoot, liveJob, cancellationToken);
                return true;
            }

            liveJob.Payload.HydratedRoots = (liveJob.Payload.HydratedRoots ?? [])
                .Union(evaluation.CompletedTrustedRoots, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            liveJob.Payload.UnknownRoots = evaluation.UnknownRoots;

            MergeAddressFrontier(liveJob.Payload, evaluation.FrontierAddresses);
            var nextAddress = liveJob.Payload.AddressCursors.FirstOrDefault(x => !x.Completed);

            if (nextAddress is not null)
            {
                await ScanAddressPageAsync(liveJob, nextAddress, cancellationToken);
                return true;
            }

            liveJob.Payload.LineageBoundaryReached = evaluation.CompletedTrustedRootCount == evaluation.TrustedRootCount;
            liveJob.Payload.HistoryBoundaryReached = liveJob.Payload.AddressCursors.All(x => x.Completed);
            liveJob.LastProgressAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            liveJob.LastObservedHistoricalBlockHeight = liveJob.Payload.OldestCoveredBlockHeight;

            if (evaluation.BlockingUnknownRoot || evaluation.HasMissingDependencies)
            {
                liveJob.Status = HistoryBackfillExecutionStatus.Failed;
                liveJob.ErrorCode = evaluation.BlockingUnknownRoot
                    ? "blocking_unknown_root_detected"
                    : "token_history_missing_dependencies";
                liveJob.SetUpdate();
                using var finalizeSession = documentStore.GetSession();
                var finalJob = await finalizeSession.LoadAsync<TokenHistoryBackfillJobDocument>(liveJob.Id, cancellationToken);
                if (finalJob is not null)
                {
                    finalJob.Payload = liveJob.Payload;
                    finalJob.LastObservedHistoricalBlockHeight = liveJob.LastObservedHistoricalBlockHeight;
                    finalJob.LastProgressAt = liveJob.LastProgressAt;
                    finalJob.Status = liveJob.Status;
                    finalJob.ErrorCode = liveJob.ErrorCode;
                    finalJob.SetUpdate();
                    await finalizeSession.SaveChangesAsync(cancellationToken);
                }

                await lifecycleOrchestrator.MarkTokenHistoryBackfillFailedAsync(job.TokenId, liveJob.ErrorCode, cancellationToken);
                return true;
            }

            using (var finalizeSession = documentStore.GetSession())
            {
                var finalJob = await finalizeSession.LoadAsync<TokenHistoryBackfillJobDocument>(liveJob.Id, cancellationToken);
                if (finalJob is not null)
                {
                    finalJob.Payload = liveJob.Payload;
                    finalJob.LastObservedHistoricalBlockHeight = liveJob.Payload.OldestCoveredBlockHeight;
                    finalJob.LastProgressAt = liveJob.LastProgressAt;
                    finalJob.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    finalJob.Status = HistoryBackfillExecutionStatus.Completed;
                    finalJob.ErrorCode = null;
                    finalJob.SetUpdate();
                    await finalizeSession.SaveChangesAsync(cancellationToken);
                }
            }

            await lifecycleOrchestrator.MarkTokenHistoryBackfillCompletedAsync(job.TokenId, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Historical token backfill failed for {TokenId}", job.TokenId);
            using var updateSession = documentStore.GetSession();
            liveJob = await updateSession.LoadAsync<TokenHistoryBackfillJobDocument>(job.Id, cancellationToken);
            if (liveJob is not null)
            {
                liveJob.Status = HistoryBackfillExecutionStatus.WaitingRetry;
                liveJob.LastProgressAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                liveJob.ErrorCode = "historical_token_scan_error";
                liveJob.SetUpdate();
                await updateSession.SaveChangesAsync(cancellationToken);
            }

            await lifecycleOrchestrator.MarkTokenHistoryBackfillWaitingRetryAsync(job.TokenId, "historical_token_scan_error", cancellationToken);
            return true;
        }
    }

    private async Task HydrateTransactionAsync(
        string txId,
        TokenHistoryBackfillJobDocument liveJob,
        CancellationToken cancellationToken
    )
    {
        var details = await bitailsRestApiClient.GetTransactionDetails(txId, cancellationToken);
        var raw = await bitailsRestApiClient.GetTransactionRawOrNullAsync(txId, cancellationToken);
        if (raw is null)
            throw new InvalidOperationException($"Trusted root transaction `{txId}` was not found.");

        var transaction = Transaction.Parse(raw, networkProvider.Network);
        txMessageBus.Post(TxMessage.FoundInBlock(
            transaction,
            details.Timestamp,
            TxObservationSource.Bitails,
            details.BlockHash,
            details.BlockHeight,
            details.Idx));

        using var session = documentStore.GetSession();
        var stored = await session.LoadAsync<TokenHistoryBackfillJobDocument>(liveJob.Id, cancellationToken);
        if (stored is null)
            return;

        stored.ItemsScanned += 1;
        stored.ItemsApplied += 1;
        stored.LastObservedHistoricalBlockHeight = stored.LastObservedHistoricalBlockHeight.HasValue
            ? Math.Min(stored.LastObservedHistoricalBlockHeight.Value, details.BlockHeight)
            : details.BlockHeight;
        stored.LastProgressAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        stored.Payload ??= new TokenHistoryBackfillPayload();
        stored.Payload.HydratedRoots = (stored.Payload.HydratedRoots ?? [])
            .Append(txId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        stored.Payload.DiscoveredTransactionCount = stored.ItemsScanned;
        stored.Payload.OldestCoveredBlockHeight = stored.LastObservedHistoricalBlockHeight;
        stored.SetUpdate();
        await session.SaveChangesAsync(cancellationToken);

        await lifecycleOrchestrator.UpdateTokenHistoryBackfillProgressAsync(
            liveJob.TokenId,
            stored.ItemsScanned,
            stored.ItemsApplied,
            stored.LastObservedHistoricalBlockHeight,
            cancellationToken);
    }

    private async Task ScanAddressPageAsync(
        TokenHistoryBackfillJobDocument liveJob,
        TokenHistoryAddressCursor addressCursor,
        CancellationToken cancellationToken
    )
    {
        var page = await bitailsRestApiClient.GetHistoryPageAsync(addressCursor.Address, addressCursor.Cursor, PageLimit, cancellationToken);
        var scanned = liveJob.ItemsScanned;
        var applied = liveJob.ItemsApplied;
        var oldest = liveJob.Payload.OldestCoveredBlockHeight;

        foreach (var entry in page)
        {
            scanned += 1;
            var details = await bitailsRestApiClient.GetTransactionDetails(entry.TxId, cancellationToken);
            var raw = await bitailsRestApiClient.GetTransactionRawOrNullAsync(entry.TxId, cancellationToken);
            if (raw is null)
                continue;

            var transaction = Transaction.Parse(raw, networkProvider.Network);
            txMessageBus.Post(TxMessage.FoundInBlock(
                transaction,
                details.Timestamp,
                TxObservationSource.Bitails,
                details.BlockHash,
                details.BlockHeight,
                details.Idx));

            applied += 1;
            oldest = oldest.HasValue ? Math.Min(oldest.Value, details.BlockHeight) : details.BlockHeight;
        }

        using var session = documentStore.GetSession();
        var stored = await session.LoadAsync<TokenHistoryBackfillJobDocument>(liveJob.Id, cancellationToken);
        if (stored is null)
            return;

        var liveCursor = (stored.Payload.AddressCursors ?? [])
            .FirstOrDefault(x => string.Equals(x.Address, addressCursor.Address, StringComparison.OrdinalIgnoreCase));
        if (liveCursor is not null)
        {
            liveCursor.Cursor = page.Pgkey;
            liveCursor.Completed = !page.Any() || string.IsNullOrWhiteSpace(page.Pgkey);
        }

        stored.ItemsScanned = scanned;
        stored.ItemsApplied = applied;
        stored.LastObservedHistoricalBlockHeight = oldest;
        stored.LastProgressAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        stored.Payload.DiscoveredTransactionCount = scanned;
        stored.Payload.OldestCoveredBlockHeight = oldest;
        stored.Payload.HistoryBoundaryReached = stored.Payload.AddressCursors.All(x => x.Completed);
        stored.SetUpdate();
        await session.SaveChangesAsync(cancellationToken);

        await lifecycleOrchestrator.UpdateTokenHistoryBackfillProgressAsync(
            liveJob.TokenId,
            scanned,
            applied,
            oldest,
            cancellationToken);
    }

    private static void MergeAddressFrontier(TokenHistoryBackfillPayload payload, IEnumerable<string> addresses)
    {
        var existing = (payload.AddressCursors ?? [])
            .ToDictionary(x => x.Address, StringComparer.OrdinalIgnoreCase);

        foreach (var address in addresses.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (!existing.ContainsKey(address))
                existing[address] = new TokenHistoryAddressCursor { Address = address };
        }

        payload.AddressCursors = existing.Values
            .OrderBy(x => x.Address, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task MarkFailedAsync(string tokenId, string jobId, string errorCode, CancellationToken cancellationToken)
    {
        using var session = documentStore.GetSession();
        var job = await session.LoadAsync<TokenHistoryBackfillJobDocument>(jobId, cancellationToken);
        if (job is not null)
        {
            job.Status = HistoryBackfillExecutionStatus.Failed;
            job.ErrorCode = errorCode;
            job.LastProgressAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            job.SetUpdate();
            await session.SaveChangesAsync(cancellationToken);
        }

        await lifecycleOrchestrator.MarkTokenHistoryBackfillFailedAsync(tokenId, errorCode, cancellationToken);
    }
}
