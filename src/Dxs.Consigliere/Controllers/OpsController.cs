using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Services.Impl;
using Dxs.Consigliere.Setup;
using Dxs.Common.Cache;
using Dxs.Infrastructure.Common;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Controllers;

[Route("api/ops")]
[Authorize(Policy = AdminAuthDefaults.Policy)]
public class OpsController(
    IAdminProviderConfigService providerConfigService,
    IJungleBusBlockSyncHealthReader jungleBusBlockSyncHealthReader,
    IJungleBusChainTipAssuranceReader jungleBusChainTipAssuranceReader,
    IValidationRepairStatusReader validationRepairStatusReader,
    IOptions<ConsigliereCacheConfig> cacheConfig,
    IOptions<ConsigliereStorageConfig> storageConfig,
    IOptions<AppConfig> appConfig,
    IExternalChainProviderCatalog providerCatalog,
    IProjectionReadCacheTelemetry projectionReadCacheTelemetry,
    IProjectionCacheRuntimeStatusReader runtimeStatusReader
) : BaseController
{
    private static readonly string[] RoutedCapabilities =
    [
        ExternalChainCapability.Broadcast,
        ExternalChainCapability.RealtimeIngest,
        ExternalChainCapability.BlockBackfill,
        ExternalChainCapability.RawTxFetch,
        ExternalChainCapability.ValidationFetch
    ];

    [HttpGet("providers")]
    [Produces(typeof(IReadOnlyCollection<ProviderStatusResponse>))]
    public async Task<IActionResult> GetProviders(CancellationToken cancellationToken)
    {
        var effectiveSources = await providerConfigService.GetEffectiveSourcesConfigAsync(cancellationToken);
        var descriptors = providerCatalog.GetDescriptors()
            .ToDictionary(x => x.Provider, StringComparer.OrdinalIgnoreCase);
        var healthByProvider = (await providerCatalog.GetHealthAsync(cancellationToken))
            .ToDictionary(x => x.Provider, StringComparer.OrdinalIgnoreCase);

        var providers = new[]
        {
            SourceCapabilityRouting.NodeProvider,
            ExternalChainProviderName.JungleBus,
            ExternalChainProviderName.Bitails,
            ExternalChainProviderName.WhatsOnChain
        };

        var result = providers
            .Select(provider => BuildProviderStatus(provider, effectiveSources, descriptors, healthByProvider))
            .ToArray();

        return Ok(result);
    }

    [HttpGet("cache")]
    [Produces(typeof(ProjectionCacheStatusResponse))]
    public async Task<IActionResult> GetProjectionCache(CancellationToken cancellationToken = default)
    {
        var snapshot = projectionReadCacheTelemetry.GetSnapshot();
        var runtime = await runtimeStatusReader.GetSnapshotAsync(cancellationToken);
        return Ok(ProjectionCacheStatusResponseFactory.Build(snapshot, runtime, snapshot.Enabled && cacheConfig.Value.Enabled));
    }

    [HttpGet("storage")]
    [Produces(typeof(StorageStatusResponse))]
    public IActionResult GetStorage()
        => Ok(StorageStatusResponseFactory.Build(storageConfig.Value));

    [HttpGet("junglebus/block-sync")]
    [Produces(typeof(JungleBusBlockSyncStatusResponse))]
    public async Task<IActionResult> GetJungleBusBlockSync(CancellationToken cancellationToken = default)
        => Ok(await jungleBusBlockSyncHealthReader.GetSnapshotAsync(cancellationToken));

    [HttpGet("junglebus/chain-tip-assurance")]
    [Produces(typeof(JungleBusChainTipAssuranceResponse))]
    public async Task<IActionResult> GetJungleBusChainTipAssurance(CancellationToken cancellationToken = default)
        => Ok(await jungleBusChainTipAssuranceReader.GetSnapshotAsync(cancellationToken));

    [HttpGet("validation/repairs")]
    [Produces(typeof(ValidationRepairStatusResponse))]
    public async Task<IActionResult> GetValidationRepairs(CancellationToken cancellationToken = default)
        => Ok(await validationRepairStatusReader.GetSnapshotAsync(cancellationToken));

    private ProviderStatusResponse BuildProviderStatus(
        string provider,
        ConsigliereSourcesConfig effectiveSources,
        IReadOnlyDictionary<string, ExternalChainProviderDescriptor> descriptors,
        IReadOnlyDictionary<string, ExternalChainProviderHealthSnapshot> healthByProvider
    )
    {
        var config = GetProviderConfig(provider, effectiveSources);
        var health = healthByProvider.TryGetValue(provider, out var snapshot)
            ? snapshot
            : new ExternalChainProviderHealthSnapshot(provider, ExternalChainHealthState.Unknown);

        var capabilities = RoutedCapabilities.ToDictionary(
            capability => capability,
            capability => BuildCapabilityStatus(provider, capability, config, health, effectiveSources),
            StringComparer.OrdinalIgnoreCase
        );

        return new ProviderStatusResponse
        {
            Provider = provider,
            Enabled = config.Enabled,
            Configured = IsConfigured(provider, config),
            Roles = BuildRoles(provider, effectiveSources),
            Healthy = health.State == ExternalChainHealthState.Healthy,
            Degraded = health.State == ExternalChainHealthState.Degraded,
            LastErrorCode = health.Detail,
            RateLimitState = BuildRateLimitState(provider, descriptors),
            Capabilities = capabilities
        };
    }

    private ProviderCapabilityStatusResponse BuildCapabilityStatus(
        string provider,
        string capability,
        SourceProviderConfig config,
        ExternalChainProviderHealthSnapshot health,
        ConsigliereSourcesConfig effectiveSources
    )
    {
        var route = SourceCapabilityRouting.Resolve(
            capability,
            effectiveSources,
            appConfig.Value,
            providerCatalog
        );

        return new ProviderCapabilityStatusResponse
        {
            Enabled = config.EnabledCapabilities.Contains(capability, StringComparer.OrdinalIgnoreCase),
            Healthy = health.State == ExternalChainHealthState.Healthy,
            Degraded = health.State == ExternalChainHealthState.Degraded,
            LastErrorCode = health.Detail,
            RateLimitState = BuildRateLimitState(provider, capability, effectiveSources),
            Active = string.Equals(route.PrimarySource, provider, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string[] BuildRoles(string provider, ConsigliereSourcesConfig effectiveSources)
    {
        var roles = new List<string>();
        var routing = effectiveSources.Routing;

        if (string.Equals(routing.PrimarySource, provider, StringComparison.OrdinalIgnoreCase))
            roles.Add("primary");

        if (routing.FallbackSources.Contains(provider, StringComparer.OrdinalIgnoreCase))
            roles.Add("fallback");

        if (string.Equals(routing.VerificationSource, provider, StringComparison.OrdinalIgnoreCase))
            roles.Add("verification");

        return roles.ToArray();
    }

    private RateLimitStateResponse BuildRateLimitState(
        string provider,
        IReadOnlyDictionary<string, ExternalChainProviderDescriptor> descriptors
    )
    {
        if (!descriptors.TryGetValue(provider, out var descriptor) || descriptor.RateLimitHint is null)
            return null;

        return new RateLimitStateResponse
        {
            Limited = false,
            Remaining = descriptor.RateLimitHint.RequestsPerMinute,
            Scope = "provider",
            SourceHint = descriptor.RateLimitHint.SourceHint
        };
    }

    private RateLimitStateResponse BuildRateLimitState(string provider, string capability, ConsigliereSourcesConfig effectiveSources)
    {
        var config = GetProviderConfig(provider, effectiveSources);
        if (config.RateLimits is null)
            return null;

        var perCapability = config.RateLimits.PerCapability.TryGetValue(capability, out var capabilityLimit)
            ? capabilityLimit
            : null;
        var limit = perCapability?.RequestsPerMinute ?? config.RateLimits.RequestsPerMinute;

        if (limit is null)
            return null;

        return new RateLimitStateResponse
        {
            Limited = false,
            Remaining = limit,
            Scope = perCapability is null ? "provider" : "capability",
            SourceHint = "config_budget"
        };
    }

    private SourceProviderConfig GetProviderConfig(string provider, ConsigliereSourcesConfig effectiveSources)
        => provider.ToLowerInvariant() switch
        {
            SourceCapabilityRouting.NodeProvider => effectiveSources.Providers.Node,
            ExternalChainProviderName.JungleBus => effectiveSources.Providers.JungleBus,
            ExternalChainProviderName.Bitails => effectiveSources.Providers.Bitails,
            ExternalChainProviderName.WhatsOnChain => effectiveSources.Providers.Whatsonchain,
            _ => new SourceProviderConfig()
        };

    private static bool IsConfigured(string provider, SourceProviderConfig config)
        => provider.ToLowerInvariant() switch
        {
            SourceCapabilityRouting.NodeProvider => config is NodeSourceConfig node &&
                !string.IsNullOrWhiteSpace(node.Connection.RpcUrl),
            ExternalChainProviderName.JungleBus => config is JungleBusSourceConfig jungleBus &&
                (!string.IsNullOrWhiteSpace(jungleBus.Connection.BaseUrl) || !string.IsNullOrWhiteSpace(jungleBus.Connection.ApiKey)),
            ExternalChainProviderName.Bitails => config is BitailsSourceConfig bitails &&
                (!string.IsNullOrWhiteSpace(bitails.Connection.BaseUrl) || !string.IsNullOrWhiteSpace(bitails.Connection.ApiKey)),
            ExternalChainProviderName.WhatsOnChain => config is WhatsOnChainSourceConfig whatsonchain &&
                (!string.IsNullOrWhiteSpace(whatsonchain.Connection.BaseUrl) || !string.IsNullOrWhiteSpace(whatsonchain.Connection.ApiKey)),
            _ => false
        };

}
