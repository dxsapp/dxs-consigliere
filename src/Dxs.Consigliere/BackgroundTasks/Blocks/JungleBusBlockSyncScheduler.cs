using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.BackgroundTasks.Blocks;

public sealed class JungleBusBlockSyncScheduler(
    IDocumentStore documentStore,
    ILogger<JungleBusBlockSyncScheduler> logger
) : IJungleBusBlockSyncScheduler
{
    public async Task<JungleBusBlockSyncScheduleResult> ScheduleUpToHeightAsync(int observedHeight, string subscriptionId, CancellationToken cancellationToken)
    {
        if (observedHeight <= 0 || string.IsNullOrWhiteSpace(subscriptionId))
            return new JungleBusBlockSyncScheduleResult(false, observedHeight, null, null);

        using var session = documentStore.GetSession();

        var unfinishedRequests = await session.Query<SyncRequest>()
            .Where(x => !x.Finished)
            .Where(x => x.SubscriptionId == subscriptionId)
            .ToListAsync(cancellationToken);

        if (unfinishedRequests.Any(x => x.FromHeight <= observedHeight && x.ToHeight >= observedHeight))
            return new JungleBusBlockSyncScheduleResult(false, observedHeight, null, null);

        var highestKnownBlock = await session.Query<BlockProcessContext>()
            .Where(x => x.Height != 0)
            .OrderByDescending(x => x.Height)
            .Select(x => (int?)x.Height)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        var highestRequestedBlock = unfinishedRequests.Count == 0
            ? 0
            : unfinishedRequests.Max(x => x.ToHeight);

        var fromHeight = highestKnownBlock == 0 && highestRequestedBlock == 0
            ? observedHeight
            : Math.Max(highestKnownBlock, highestRequestedBlock) + 1;
        if (fromHeight > observedHeight)
            return new JungleBusBlockSyncScheduleResult(false, observedHeight, null, null);

        var request = new SyncRequest
        {
            Id = $"SyncRequests/JungleBus/{subscriptionId}/{fromHeight}-{observedHeight}",
            FromHeight = fromHeight,
            ToHeight = observedHeight,
            SubscriptionId = subscriptionId
        };

        logger.LogInformation(
            "Scheduling JungleBus block sync request from {FromHeight} to {ToHeight} using subscription `{SubscriptionId}`",
            fromHeight,
            observedHeight,
            subscriptionId
        );

        await session.StoreAsync(request, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        return new JungleBusBlockSyncScheduleResult(true, observedHeight, fromHeight, observedHeight);
    }
}
