using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Tracking.HistoryBackfill;
using Dxs.Consigliere.Extensions;
using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Options;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Services.Impl;

public sealed class HistoricalAddressBackfillRunner(
    IDocumentStore documentStore,
    IBitailsRestApiClient bitailsRestApiClient,
    ITxMessageBus txMessageBus,
    INetworkProvider networkProvider,
    IAdminProviderConfigService providerConfigService,
    IOptions<AppConfig> legacyConfig,
    IExternalChainProviderCatalog providerCatalog,
    ITrackedEntityLifecycleOrchestrator lifecycleOrchestrator,
    ILogger<HistoricalAddressBackfillRunner> logger
)
{
    private const int PageLimit = 50;

    public async Task<bool> RunNextAsync(CancellationToken cancellationToken = default)
    {
        AddressHistoryBackfillJobDocument job;
        using (var readSession = documentStore.GetSession())
        {
            job = await readSession.Query<AddressHistoryBackfillJobDocument>()
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
            ExternalChainCapability.HistoricalAddressScan,
            effectiveSources,
            legacyConfig.Value,
            providerCatalog);
        var provider = SourceCapabilityRouting.SelectForAttempt(route, job.AttemptCount);
        if (!string.Equals(provider, ExternalChainProviderName.Bitails, StringComparison.OrdinalIgnoreCase))
        {
            await MarkFailedAsync(job.Address, job.Id, "historical_address_scan_unavailable", cancellationToken);
            return true;
        }

        await lifecycleOrchestrator.MarkAddressHistoryBackfillRunningAsync(job.Address, cancellationToken);

        using var session = documentStore.GetSession();
        var liveJob = await session.LoadAsync<AddressHistoryBackfillJobDocument>(job.Id, cancellationToken);
        if (liveJob is null)
            return true;

        liveJob.Status = HistoryBackfillExecutionStatus.Running;
        liveJob.Provider = provider;
        liveJob.SourceCapability = ExternalChainCapability.HistoricalAddressScan;
        liveJob.StartedAt ??= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        liveJob.LastProgressAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        liveJob.AttemptCount += 1;
        liveJob.SetUpdate();
        await session.SaveChangesAsync(cancellationToken);

        try
        {
            var page = await bitailsRestApiClient.GetHistoryPageAsync(liveJob.Address, liveJob.Payload.Cursor, PageLimit, cancellationToken);
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
                oldest = oldest.HasValue
                    ? Math.Min(oldest.Value, details.BlockHeight)
                    : details.BlockHeight;
            }

            using var updateSession = documentStore.GetSession();
            liveJob = await updateSession.LoadAsync<AddressHistoryBackfillJobDocument>(job.Id, cancellationToken);
            if (liveJob is null)
                return true;

            liveJob.ItemsScanned = scanned;
            liveJob.ItemsApplied = applied;
            liveJob.LastProgressAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            liveJob.LastObservedHistoricalBlockHeight = oldest;
            liveJob.Payload.DiscoveredTransactionCount = scanned;
            liveJob.Payload.OldestCoveredBlockHeight = oldest;
            liveJob.Payload.Cursor = page.Pgkey;
            liveJob.Cursor = page.Pgkey;

            var completed = !page.Any() || string.IsNullOrWhiteSpace(page.Pgkey);
            liveJob.Status = completed ? HistoryBackfillExecutionStatus.Completed : HistoryBackfillExecutionStatus.Running;
            liveJob.CompletedAt = completed ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : null;
            liveJob.ErrorCode = null;
            liveJob.SetUpdate();
            await updateSession.SaveChangesAsync(cancellationToken);

            await lifecycleOrchestrator.UpdateAddressHistoryBackfillProgressAsync(liveJob.Address, scanned, applied, oldest, cancellationToken);
            if (completed)
                await lifecycleOrchestrator.MarkAddressHistoryBackfillCompletedAsync(liveJob.Address, cancellationToken);

            logger.LogInformation(
                "Historical address backfill processed {Address}: scanned={Scanned}, applied={Applied}, status={Status}",
                liveJob.Address,
                scanned,
                applied,
                liveJob.Status);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Historical address backfill failed for {Address}", job.Address);
            using var updateSession = documentStore.GetSession();
            liveJob = await updateSession.LoadAsync<AddressHistoryBackfillJobDocument>(job.Id, cancellationToken);
            if (liveJob is not null)
            {
                liveJob.Status = HistoryBackfillExecutionStatus.WaitingRetry;
                liveJob.LastProgressAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                liveJob.ErrorCode = "historical_address_scan_error";
                liveJob.SetUpdate();
                await updateSession.SaveChangesAsync(cancellationToken);
            }

            await lifecycleOrchestrator.MarkAddressHistoryBackfillWaitingRetryAsync(job.Address, "historical_address_scan_error", cancellationToken);
            return true;
        }
    }

    private async Task MarkFailedAsync(string address, string jobId, string errorCode, CancellationToken cancellationToken)
    {
        using var session = documentStore.GetSession();
        var job = await session.LoadAsync<AddressHistoryBackfillJobDocument>(jobId, cancellationToken);
        if (job is not null)
        {
            job.Status = HistoryBackfillExecutionStatus.Failed;
            job.ErrorCode = errorCode;
            job.LastProgressAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            job.SetUpdate();
            await session.SaveChangesAsync(cancellationToken);
        }

        await lifecycleOrchestrator.MarkAddressHistoryBackfillFailedAsync(address, errorCode, cancellationToken);
    }
}
