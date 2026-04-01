using Dxs.Common.Extensions;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.BackgroundTasks.Blocks;
using Dxs.Consigliere.BackgroundTasks.Realtime;

using Microsoft.Extensions.DependencyInjection;

using TrustMargin.Common.Extensions;

namespace Dxs.Consigliere.Setup;

public static class HostedTasksSetup
{
    public static IServiceCollection AddHostedTaskZoneServices(this IServiceCollection services)
        => services
            .AddSingleton<IJungleBusSyncRequestProcessor, JungleBusSyncRequestProcessor>()
            .AddSingleton<IJungleBusMissingTransactionCollector, JungleBusMissingTransactionCollector>()
            .AddSingleton<IJungleBusMissingTransactionFetcher, JungleBusMissingTransactionFetcher>()
            .AddSingleton<IJungleBusBlockSyncScheduler, JungleBusBlockSyncScheduler>()
            .AddSingleton<TxObservationJournalWriter>()
            .AddSingleton<BlockObservationJournalWriter>()
            .AddSingletonHostedService<AppInitBackgroundTask>()
            .AddSingletonHostedService<BlockProcessBackgroundTask>()
            .AddSingletonHostedService<BlockObservationJournalMirrorBackgroundTask>()
            .AddSingletonHostedService<ActualChainTipVerifyBackgroundTask>()
            .AddSingletonHostedService<JungleBusBlockSyncMonitorBackgroundTask>()
            .AddSingletonHostedService<AddressHistoryEnvelopeBackfillBackgroundTask>()
            .AddSingletonHostedService<TrackedHistoryBackfillBackgroundTask>()
            .AddSingletonHostedService<StasAttributesMissingTransactions>()
            .AddSingletonHostedService<StasAttributesChangeObserverTask>()
            .AddSingletonHostedService<ValidationDependencyRepairBackgroundTask>()
            .AddSingletonHostedService<TxObservationJournalMirrorBackgroundTask>()
            .AddSingletonHostedService<JungleBusSyncMissingDataBackgroundTask>()
            .AddSingletonHostedService<UnconfirmedTransactionsMonitor>()
            .AddSingletonHostedService<RealtimeIngestBackgroundTask>();
}
