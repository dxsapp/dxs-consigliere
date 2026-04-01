using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Options;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Data.Runtime;

public sealed class JungleBusChainTipAssuranceReader(
    IDocumentStore documentStore,
    IAdminProviderConfigService providerConfigService,
    IAdminRuntimeSourcePolicyService runtimeSourcePolicyService,
    IExternalChainProviderCatalog providerCatalog,
    IExternalChainProviderSettingsAccessor providerSettingsAccessor,
    IOptions<AppConfig> appConfig
) : IJungleBusChainTipAssuranceReader
{
    private const int ControlFlowStaleAfterSeconds = 120;
    private const int LocalProgressStaleAfterSeconds = 180;

    public async Task<JungleBusChainTipAssuranceResponse> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var effectiveSources = await runtimeSourcePolicyService.GetEffectiveSourcesConfigAsync(cancellationToken);
        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.BlockBackfill,
            effectiveSources,
            appConfig.Value,
            providerCatalog);
        var jungleBus = await providerConfigService.GetEffectiveJungleBusAsync(cancellationToken);
        var runtimeSettings = await providerSettingsAccessor.GetJungleBusAsync(cancellationToken);

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

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var controlFlowStalled = isPrimary && configured && IsOlderThan(snapshot?.LastControlMessageAt, now, ControlFlowStaleAfterSeconds);
        var localProgressReference = snapshot?.LastLocalProgressAt ?? snapshot?.LastProcessedAt;
        var localProgressStalled = isPrimary
                                   && configured
                                   && !controlFlowStalled
                                   && lagBlocks is > 0
                                   && IsOlderThan(localProgressReference, now, LocalProgressStaleAfterSeconds);

        var secondaryCrossCheckAvailable = false;
        var singleSourceAssurance = isPrimary && configured && !secondaryCrossCheckAvailable;
        var state = !isPrimary || !configured
            ? "unavailable"
            : controlFlowStalled
                ? "stalled_control_flow"
                : localProgressStalled
                    ? "stalled_local_progress"
                    : lagBlocks is > 0
                        ? "catching_up"
                        : "healthy";
        var assuranceMode = !isPrimary || !configured
            ? "unavailable"
            : secondaryCrossCheckAvailable
                ? "cross_checked"
                : "single_source";

        return new JungleBusChainTipAssuranceResponse
        {
            Primary = isPrimary,
            Configured = configured,
            State = state,
            AssuranceMode = assuranceMode,
            SingleSourceAssurance = singleSourceAssurance,
            SecondaryCrossCheckAvailable = secondaryCrossCheckAvailable,
            ControlFlowStalled = controlFlowStalled,
            LocalProgressStalled = localProgressStalled,
            UnavailableReason = unavailableReason,
            Note = singleSourceAssurance
                ? "No secondary chain-tip cross-check is active in JungleBus-first mode."
                : null,
            LastObservedBlockHeight = snapshot?.LastObservedBlockHeight,
            HighestKnownLocalBlockHeight = highestKnownLocalBlockHeight,
            LagBlocks = lagBlocks,
            LastObservedMovementAt = snapshot?.LastObservedMovementAt,
            LastObservedMovementHeight = snapshot?.LastObservedMovementHeight,
            LastLocalProgressAt = snapshot?.LastLocalProgressAt ?? snapshot?.LastProcessedAt,
            LastLocalProgressHeight = snapshot?.LastLocalProgressHeight ?? snapshot?.LastProcessedBlockHeight,
            LastControlMessageAt = snapshot?.LastControlMessageAt,
            LastScheduledAt = snapshot?.LastScheduledAt,
            LastProcessedAt = snapshot?.LastProcessedAt,
            LastError = snapshot?.LastError,
            LastErrorAt = snapshot?.LastErrorAt,
            ControlFlowStaleAfterSeconds = ControlFlowStaleAfterSeconds,
            LocalProgressStaleAfterSeconds = LocalProgressStaleAfterSeconds
        };
    }

    private static bool IsOlderThan(long? timestamp, long nowUnixMs, int thresholdSeconds)
    {
        if (!timestamp.HasValue || timestamp.Value <= 0)
            return true;

        return nowUnixMs - timestamp.Value > thresholdSeconds * 1000L;
    }
}
