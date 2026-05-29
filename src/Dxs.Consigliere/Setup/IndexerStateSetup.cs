using Dxs.Bsv.Factories;
using Dxs.Common.Cache;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Runtime;
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
            .AddSingleton<IJungleBusBlockSyncHealthStore, JungleBusBlockSyncHealthStore>()
            .AddSingleton<IJungleBusBlockSyncHealthReader, JungleBusBlockSyncHealthReader>()
            .AddSingleton<IJungleBusChainTipAssuranceReader, JungleBusChainTipAssuranceReader>()
            .AddSingleton<IValidationRepairStatusReader, ValidationRepairStatusReader>()
            .AddSingleton<ITokenValidationDependencyStore, TokenValidationDependencyStore>()
            .AddSingleton<IValidationRepairWorkItemStore, ValidationRepairWorkItemStore>()
            .AddSingleton<AddressProjectionReader>()
            .AddSingleton<AddressHistoryProjectionReader>()
            .AddSingleton<AddressProjectionRebuilder>()
            .AddSingleton<TokenProjectionReader>()
            .AddSingleton<TokenProjectionRebuilder>()
            .AddSingleton<TxLifecycleProjectionReader>()
            .AddSingleton<TxLifecycleProjectionRebuilder>()
            // W3 A2 H1 fix: ReorgPipeline drives the projection rebuilder
            // before firing OnReorg so SignalR clients re-querying after
            // the event observe Reorged. The rebuilder is sealed, so we
            // expose it via a thin IProjectionRebuilder adapter.
            .AddSingleton<Dxs.Consigliere.Services.P2p.IProjectionRebuilder,
                          Dxs.Consigliere.Services.P2p.TxLifecycleProjectionRebuilderAdapter>()
            .AddSingleton<SecretsFileStore>()
            .AddSingleton<IRealtimeSourcePolicyOverrideStore>(sp => sp.GetRequiredService<SecretsFileStore>())
            .AddSingleton<ISetupBootstrapStore, SetupBootstrapStore>()
            .AddSingleton<IOperatorRuntimeSettingsStore, OperatorRuntimeSettingsStore>()
            .AddSingleton<IOperatorRuntimeSettingsService, OperatorRuntimeSettingsService>()
            .AddSingleton<IAdminProviderConfigService, AdminProviderConfigService>()
            .AddSingleton<ISetupWizardService, SetupWizardService>()
            .AddSingleton<IAdminRuntimeSourcePolicyService, AdminRuntimeSourcePolicyService>()
            .AddSingleton<Dxs.Infrastructure.Common.IExternalChainProviderSettingsAccessor, ExternalChainProviderSettingsAccessor>()
            .AddSingleton<IAdminTrackingQueryService, AdminTrackingQueryService>()
            .AddSingleton<ITrackedEntityRegistrationStore, TrackedEntityRegistrationStore>()
            .AddSingleton<ITrackedEntityLifecycleOrchestrator, TrackedEntityLifecycleOrchestrator>()
            .AddSingleton<ITrackedEntityReadinessService, TrackedEntityReadinessService>()
            .AddCache()
            .AddTransactionFactories()
            .AddSingleton<IAddressHistoryService, AddressHistoryService>();
}
