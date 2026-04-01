using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Consigliere.BackgroundTasks.Blocks;
using Dxs.Consigliere.BackgroundTasks.Realtime;

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
            .AddSingleton<IRawTransactionFetchService, RawTransactionFetchService>()
            .AddTransient<BlockProcessExecutor>()
            .AddSingleton<IBitailsRealtimeSubscriptionScopeProvider, BitailsRealtimeSubscriptionScopeProvider>()
            .AddSingleton<BitailsRealtimeIngestRunner>()
            .AddSingleton<JungleBusRealtimeIngestRunner>()
            .AddSingleton<ITrackedHistoryBackfillScheduler, TrackedHistoryBackfillScheduler>()
            .AddTransient<HistoricalAddressBackfillRunner>()
            .AddTransient<HistoricalTokenBackfillRunner>();
}
