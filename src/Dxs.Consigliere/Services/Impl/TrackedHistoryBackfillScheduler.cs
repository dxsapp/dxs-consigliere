using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Tracking.HistoryBackfill;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Services.Impl;

public sealed class TrackedHistoryBackfillScheduler(
    IDocumentStore documentStore,
    ITrackedEntityLifecycleOrchestrator lifecycleOrchestrator
) : ITrackedHistoryBackfillScheduler
{
    public async Task<bool> QueueAddressFullHistoryAsync(string address, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var tracked = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address), cancellationToken);
        var status = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(address), cancellationToken);
        if (tracked is null || status is null || !tracked.Tracked || status.IsTombstoned)
            return false;

        var job = await session.LoadAsync<AddressHistoryBackfillJobDocument>(AddressHistoryBackfillJobDocument.GetId(address), cancellationToken);
        if (job is null)
        {
            job = new AddressHistoryBackfillJobDocument
            {
                Id = AddressHistoryBackfillJobDocument.GetId(address),
                EntityType = TrackedEntityType.Address,
                EntityId = address,
                Address = address,
                HistoryMode = TrackedEntityHistoryMode.FullHistory,
                SourceCapability = Dxs.Infrastructure.Common.ExternalChainCapability.HistoricalAddressScan,
                Payload = new AddressHistoryBackfillPayload
                {
                    AnchorBlockHeight = status.HistoryAnchorBlockHeight,
                    OldestCoveredBlockHeight = status.HistoryCoverage?.AuthoritativeFromBlockHeight,
                }
            };
            await session.StoreAsync(job, job.Id, cancellationToken);
        }
        else if (!string.Equals(job.Status, HistoryBackfillExecutionStatus.Completed, StringComparison.Ordinal)
                 && !string.Equals(job.Status, HistoryBackfillExecutionStatus.Running, StringComparison.Ordinal)
                 && !string.Equals(job.Status, HistoryBackfillExecutionStatus.Queued, StringComparison.Ordinal))
        {
            job.Status = HistoryBackfillExecutionStatus.Queued;
            job.ErrorCode = null;
            job.SetUpdate();
        }

        await session.SaveChangesAsync(cancellationToken);
        await lifecycleOrchestrator.MarkAddressHistoryBackfillQueuedAsync(address, cancellationToken);
        return true;
    }

    public async Task<bool> QueueTokenFullHistoryAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var tracked = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId), cancellationToken);
        var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), cancellationToken);
        if (tracked is null || status is null || !tracked.Tracked || status.IsTombstoned)
            return false;

        var job = await session.LoadAsync<TokenHistoryBackfillJobDocument>(TokenHistoryBackfillJobDocument.GetId(tokenId), cancellationToken);
        if (job is null)
        {
            job = new TokenHistoryBackfillJobDocument
            {
                Id = TokenHistoryBackfillJobDocument.GetId(tokenId),
                EntityType = TrackedEntityType.Token,
                EntityId = tokenId,
                TokenId = tokenId,
                HistoryMode = TrackedEntityHistoryMode.FullHistory,
                SourceCapability = Dxs.Infrastructure.Common.ExternalChainCapability.HistoricalTokenScan,
                Payload = new TokenHistoryBackfillPayload
                {
                    AnchorBlockHeight = status.HistoryAnchorBlockHeight,
                    OldestCoveredBlockHeight = status.HistoryCoverage?.AuthoritativeFromBlockHeight,
                }
            };
            await session.StoreAsync(job, job.Id, cancellationToken);
        }
        else if (!string.Equals(job.Status, HistoryBackfillExecutionStatus.Completed, StringComparison.Ordinal)
                 && !string.Equals(job.Status, HistoryBackfillExecutionStatus.Running, StringComparison.Ordinal)
                 && !string.Equals(job.Status, HistoryBackfillExecutionStatus.Queued, StringComparison.Ordinal))
        {
            job.Status = HistoryBackfillExecutionStatus.Queued;
            job.ErrorCode = null;
            job.SetUpdate();
        }

        await session.SaveChangesAsync(cancellationToken);
        await lifecycleOrchestrator.MarkTokenHistoryBackfillQueuedAsync(tokenId, cancellationToken);
        return true;
    }
}
