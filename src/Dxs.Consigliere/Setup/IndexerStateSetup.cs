using Dxs.Bsv.Factories;
using Dxs.Common.Cache;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Tracking;
using Dxs.Consigliere.Data.Tokens;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;

using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Consigliere.Setup;

public static class IndexerStateSetup
{
    public static IServiceCollection AddIndexerStateZoneServices(this IServiceCollection services)
        => services
            .AddTransient<IMetaTransactionStore, TransactionStore>()
            .AddSingleton<UtxoSetManager>()
            .AddSingleton<IUtxoSetProvider>(sp => sp.GetRequiredService<UtxoSetManager>())
            .AddSingleton<IUtxoManager>(sp => sp.GetRequiredService<UtxoSetManager>())
            .AddSingleton<RavenObservationJournalReader>()
            .AddSingleton<IProjectionReadCacheKeyFactory, ProjectionReadCacheKeyFactory>()
            .AddSingleton<IProjectionCacheInvalidationTelemetry, ProjectionCacheInvalidationTelemetry>()
            .AddSingleton<IAddressHistoryEnvelopeBackfillService, AddressHistoryEnvelopeBackfillService>()
            .AddSingleton<IAddressHistoryEnvelopeBackfillTelemetry>(sp => (IAddressHistoryEnvelopeBackfillTelemetry)sp.GetRequiredService<IAddressHistoryEnvelopeBackfillService>())
            .AddSingleton<IProjectionCacheRuntimeStatusReader, ProjectionCacheRuntimeStatusReader>()
            .AddSingleton<AddressProjectionReader>()
            .AddSingleton<AddressHistoryProjectionReader>()
            .AddSingleton<AddressProjectionRebuilder>()
            .AddSingleton<TokenProjectionReader>()
            .AddSingleton<TokenProjectionRebuilder>()
            .AddSingleton<TxLifecycleProjectionReader>()
            .AddSingleton<TxLifecycleProjectionRebuilder>()
            .AddSingleton<ITrackedEntityRegistrationStore, TrackedEntityRegistrationStore>()
            .AddSingleton<ITrackedEntityLifecycleOrchestrator, TrackedEntityLifecycleOrchestrator>()
            .AddSingleton<ITrackedEntityReadinessService, TrackedEntityReadinessService>()
            .AddCache()
            .AddTransactionFactories()
            .AddSingleton<IAddressHistoryService, AddressHistoryService>();
}
