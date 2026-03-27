using Dxs.Bsv.Zmq;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

namespace Dxs.Consigliere.BackgroundTasks.Realtime;

public sealed record RealtimeBootstrapPlan(
    string RealtimePrimarySource,
    string BlockBackfillPrimarySource,
    ZmqSubscriptionTopics NodeZmqTopics,
    bool ScanMempoolOnStart,
    bool ReplayRecentBlocksOnStart
)
{
    public bool StartsNodeRealtimeFeed => NodeZmqTopics.HasFlag(ZmqSubscriptionTopics.Mempool);
    public bool StartsNodeBlockFeed => NodeZmqTopics.HasFlag(ZmqSubscriptionTopics.Blocks);
}

public static class RealtimeBootstrapPlanner
{
    public static RealtimeBootstrapPlan Build(SourceCapabilityRoute realtimeRoute, SourceCapabilityRoute blockBackfillRoute)
    {
        ArgumentNullException.ThrowIfNull(realtimeRoute);
        ArgumentNullException.ThrowIfNull(blockBackfillRoute);

        var topics = ZmqSubscriptionTopics.None;
        if (string.Equals(realtimeRoute.PrimarySource, SourceCapabilityRouting.NodeProvider, StringComparison.OrdinalIgnoreCase))
            topics |= ZmqSubscriptionTopics.Mempool;

        if (string.Equals(blockBackfillRoute.PrimarySource, SourceCapabilityRouting.NodeProvider, StringComparison.OrdinalIgnoreCase))
            topics |= ZmqSubscriptionTopics.Blocks;

        return new RealtimeBootstrapPlan(
            realtimeRoute.PrimarySource,
            blockBackfillRoute.PrimarySource,
            topics,
            topics.HasFlag(ZmqSubscriptionTopics.Mempool),
            topics.HasFlag(ZmqSubscriptionTopics.Blocks)
        );
    }
}
