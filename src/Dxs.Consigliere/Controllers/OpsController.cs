using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Services.Impl;
using Dxs.Common.Cache;
using Dxs.Infrastructure.Common;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Controllers;

[Route("api/ops")]
public class OpsController(
    IOptions<ConsigliereSourcesConfig> sourcesConfig,
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
            .Select(provider => BuildProviderStatus(provider, descriptors, healthByProvider))
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

    private ProviderStatusResponse BuildProviderStatus(
        string provider,
        IReadOnlyDictionary<string, ExternalChainProviderDescriptor> descriptors,
        IReadOnlyDictionary<string, ExternalChainProviderHealthSnapshot> healthByProvider
    )
    {
        var config = GetProviderConfig(provider);
        var health = healthByProvider.TryGetValue(provider, out var snapshot)
            ? snapshot
            : new ExternalChainProviderHealthSnapshot(provider, ExternalChainHealthState.Unknown);

        var capabilities = RoutedCapabilities.ToDictionary(
            capability => capability,
            capability => BuildCapabilityStatus(provider, capability, config, health),
            StringComparer.OrdinalIgnoreCase
        );

        return new ProviderStatusResponse
        {
            Provider = provider,
            Enabled = config.Enabled,
            Configured = IsConfigured(provider, config),
            Roles = BuildRoles(provider),
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
        ExternalChainProviderHealthSnapshot health
    )
    {
        var route = SourceCapabilityRouting.Resolve(
            capability,
            sourcesConfig.Value,
            appConfig.Value,
            providerCatalog
        );

        return new ProviderCapabilityStatusResponse
        {
            Enabled = config.EnabledCapabilities.Contains(capability, StringComparer.OrdinalIgnoreCase),
            Healthy = health.State == ExternalChainHealthState.Healthy,
            Degraded = health.State == ExternalChainHealthState.Degraded,
            LastErrorCode = health.Detail,
            RateLimitState = BuildRateLimitState(provider, capability),
            Active = string.Equals(route.PrimarySource, provider, StringComparison.OrdinalIgnoreCase)
        };
    }

    private string[] BuildRoles(string provider)
    {
        var roles = new List<string>();
        var routing = sourcesConfig.Value.Routing;

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

    private RateLimitStateResponse BuildRateLimitState(string provider, string capability)
    {
        var config = GetProviderConfig(provider);
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

    private SourceProviderConfig GetProviderConfig(string provider)
        => provider.ToLowerInvariant() switch
        {
            SourceCapabilityRouting.NodeProvider => sourcesConfig.Value.Providers.Node,
            ExternalChainProviderName.JungleBus => sourcesConfig.Value.Providers.JungleBus,
            ExternalChainProviderName.Bitails => sourcesConfig.Value.Providers.Bitails,
            ExternalChainProviderName.WhatsOnChain => sourcesConfig.Value.Providers.Whatsonchain,
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
