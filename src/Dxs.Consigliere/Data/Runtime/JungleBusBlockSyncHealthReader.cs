using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Options;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Data.Runtime;

public sealed class JungleBusBlockSyncHealthReader(
    IDocumentStore documentStore,
    IAdminProviderConfigService providerConfigService,
    IAdminRuntimeSourcePolicyService runtimeSourcePolicyService,
    IExternalChainProviderCatalog providerCatalog,
    IExternalChainProviderSettingsAccessor providerSettingsAccessor,
    IOptions<AppConfig> appConfig
) : IJungleBusBlockSyncHealthReader
{
    public async Task<JungleBusBlockSyncStatusResponse> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var effectiveSources = await runtimeSourcePolicyService.GetEffectiveSourcesConfigAsync(cancellationToken);
        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.BlockBackfill,
            effectiveSources,
            appConfig.Value,
            providerCatalog);
        var jungleBus = await providerConfigService.GetEffectiveJungleBusAsync(cancellationToken);
        var runtimeSettings = await providerSettingsAccessor.GetJungleBusAsync(cancellationToken);
        var health = await providerCatalog.GetHealthAsync(cancellationToken);
        var providerHealth = health.FirstOrDefault(x => string.Equals(x.Provider, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase));

        using var session = documentStore.GetNoCacheNoTrackingSession();
        var snapshot = await session.LoadAsync<Data.Models.Runtime.JungleBusBlockSyncHealthDocument>(
            Data.Models.Runtime.JungleBusBlockSyncHealthDocument.DocumentId,
            cancellationToken);

        var highestKnownLocalBlockHeight = await session.Query<BlockProcessContext>()
            .Where(x => x.Height > 0)
            .OrderByDescending(x => x.Height)
            .Select(x => (int?)x.Height)
            .FirstOrDefaultAsync(cancellationToken);

        var isPrimary = string.Equals(route.PrimarySource, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase);
        var baseUrl = jungleBus.BaseUrl ?? runtimeSettings.BaseUrl;
        var blockSubscriptionId = jungleBus.BlockSubscriptionId ?? runtimeSettings.BlockSubscriptionId;
        var configured = !string.IsNullOrWhiteSpace(baseUrl) && !string.IsNullOrWhiteSpace(blockSubscriptionId);
        var lagBlocks = snapshot?.LastObservedBlockHeight is int observed && observed > 0 &&
                        highestKnownLocalBlockHeight is int local && local > 0
            ? (int?)Math.Max(0, observed - local)
            : null;
        var unavailableReason = !isPrimary
            ? "block_backfill_primary_not_junglebus"
            : !configured
                ? "junglebus_block_sync_not_configured"
                : null;

        return new JungleBusBlockSyncStatusResponse
        {
            Primary = isPrimary,
            Configured = configured,
            Healthy = providerHealth?.State == ExternalChainHealthState.Healthy,
            Degraded = providerHealth?.State == ExternalChainHealthState.Degraded,
            UnavailableReason = unavailableReason,
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl,
            BlockSubscriptionIdConfigured = !string.IsNullOrWhiteSpace(blockSubscriptionId),
            LastObservedBlockHeight = snapshot?.LastObservedBlockHeight,
            HighestKnownLocalBlockHeight = highestKnownLocalBlockHeight,
            LagBlocks = lagBlocks,
            LastControlMessageAt = snapshot?.LastControlMessageAt,
            LastControlCode = snapshot?.LastControlCode,
            LastControlStatus = snapshot?.LastControlStatus,
            LastControlMessage = snapshot?.LastControlMessage,
            LastScheduledAt = snapshot?.LastScheduledAt,
            LastScheduledFromHeight = snapshot?.LastScheduledFromHeight,
            LastScheduledToHeight = snapshot?.LastScheduledToHeight,
            LastProcessedAt = snapshot?.LastProcessedAt,
            LastProcessedBlockHeight = snapshot?.LastProcessedBlockHeight,
            LastRequestId = snapshot?.LastRequestId,
            LastError = snapshot?.LastError ?? providerHealth?.Detail,
            LastErrorAt = snapshot?.LastErrorAt
        };
    }
}
