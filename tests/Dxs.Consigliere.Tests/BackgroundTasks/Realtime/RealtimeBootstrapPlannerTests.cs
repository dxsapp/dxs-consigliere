using Dxs.Bsv.Zmq;
using Dxs.Consigliere.BackgroundTasks.Realtime;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

namespace Dxs.Consigliere.Tests.BackgroundTasks.Realtime;

public class RealtimeBootstrapPlannerTests
{
    [Fact]
    public void Build_StartsNoNodeTopics_WhenBitailsIsRealtimePrimary_AndJungleBusOwnsBlockBackfill()
    {
        var plan = RealtimeBootstrapPlanner.Build(
            new SourceCapabilityRoute(ExternalChainCapability.RealtimeIngest, "hybrid", ExternalChainProviderName.Bitails, [ExternalChainProviderName.JungleBus], SourceCapabilityRouting.NodeProvider),
            new SourceCapabilityRoute(ExternalChainCapability.BlockBackfill, "hybrid", ExternalChainProviderName.JungleBus, [SourceCapabilityRouting.NodeProvider], SourceCapabilityRouting.NodeProvider));

        Assert.Equal(ZmqSubscriptionTopics.None, plan.NodeZmqTopics);
        Assert.False(plan.ScanMempoolOnStart);
        Assert.False(plan.ReplayRecentBlocksOnStart);
    }

    [Fact]
    public void Build_StartsNodeTopics_WhenNodeIsPrimaryForRealtimeAndBlocks()
    {
        var plan = RealtimeBootstrapPlanner.Build(
            new SourceCapabilityRoute(ExternalChainCapability.RealtimeIngest, "node", SourceCapabilityRouting.NodeProvider, [], SourceCapabilityRouting.NodeProvider),
            new SourceCapabilityRoute(ExternalChainCapability.BlockBackfill, "node", SourceCapabilityRouting.NodeProvider, [], SourceCapabilityRouting.NodeProvider));

        Assert.Equal(ZmqSubscriptionTopics.All, plan.NodeZmqTopics);
        Assert.True(plan.ScanMempoolOnStart);
        Assert.True(plan.ReplayRecentBlocksOnStart);
    }
}
