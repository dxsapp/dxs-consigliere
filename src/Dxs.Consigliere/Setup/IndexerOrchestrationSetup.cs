using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;

using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Consigliere.Setup;

public static class IndexerOrchestrationSetup
{
    public static IServiceCollection AddIndexerOrchestrationZoneServices(
        this IServiceCollection services
    )
        => services
            .AddTransient<JungleBusBlockchainDataProvider>()
            .AddTransient<NodeBlockchainDataProvider>()
            .AddSingleton<ITrackedHistoryBackfillScheduler, TrackedHistoryBackfillScheduler>()
            .AddTransient<HistoricalAddressBackfillRunner>()
            .AddTransient<HistoricalTokenBackfillRunner>();
}
