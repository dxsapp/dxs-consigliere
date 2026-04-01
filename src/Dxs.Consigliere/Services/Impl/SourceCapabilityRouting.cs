using Dxs.Consigliere.Configs;
using Dxs.Infrastructure.Common;

namespace Dxs.Consigliere.Services.Impl;

public sealed record SourceCapabilityRoute(
    string Capability,
    string PreferredMode,
    string PrimarySource,
    IReadOnlyList<string> FallbackSources,
    string VerificationSource
);

public static class SourceCapabilityRouting
{
    public const string NodeProvider = "node";

    public static SourceCapabilityRoute Resolve(
        string capability,
        ConsigliereSourcesConfig sourcesConfig,
        AppConfig legacyConfig,
        IExternalChainProviderCatalog providerCatalog
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);
        ArgumentNullException.ThrowIfNull(sourcesConfig);
        ArgumentNullException.ThrowIfNull(legacyConfig);
        ArgumentNullException.ThrowIfNull(providerCatalog);

        var descriptorByProvider = providerCatalog.GetDescriptors()
            .ToDictionary(x => x.Provider, StringComparer.OrdinalIgnoreCase);

        var overrideConfig = GetOverride(sourcesConfig.Capabilities, capability);
        var primarySource = FirstAllowed(
            GetOverrideSources(overrideConfig),
            capability,
            sourcesConfig,
            descriptorByProvider
        );

        if (string.IsNullOrWhiteSpace(primarySource))
        {
            primarySource = FirstAllowed(
                [sourcesConfig.Routing.PrimarySource],
                capability,
                sourcesConfig,
                descriptorByProvider
            );
        }

        if (string.IsNullOrWhiteSpace(primarySource))
        {
            primarySource = GetLegacyPrimary(capability, legacyConfig);
        }

        var fallbacks = AllowedProviders(
            overrideConfig?.FallbackSources?.Any() == true
                ? overrideConfig.FallbackSources
                : sourcesConfig.Routing.FallbackSources,
            capability,
            sourcesConfig,
            descriptorByProvider
        );

        if (!fallbacks.Any())
        {
            fallbacks = GetLegacyFallbacks(capability, legacyConfig)
                .Where(x => !string.Equals(x, primarySource, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var verificationSource = FirstAllowed(
            [sourcesConfig.Routing.VerificationSource],
            ExternalChainCapability.ValidationFetch,
            sourcesConfig,
            descriptorByProvider
        );

        if (string.IsNullOrWhiteSpace(verificationSource))
        {
            verificationSource = GetLegacyVerificationSource(legacyConfig);
        }

        return new SourceCapabilityRoute(
            capability,
            sourcesConfig.Routing.PreferredMode,
            primarySource,
            fallbacks,
            verificationSource
        );
    }

    public static string SelectForAttempt(SourceCapabilityRoute route, int errorsCount)
    {
        ArgumentNullException.ThrowIfNull(route);

        if (errorsCount <= 0 || route.FallbackSources.Count == 0)
            return route.PrimarySource;

        var fallbackIndex = (errorsCount - 1) % route.FallbackSources.Count;
        return route.FallbackSources[fallbackIndex];
    }

    private static RoutedCapabilityOverrideConfig GetOverride(SourceCapabilitiesConfig capabilities, string capability)
        => capability switch
        {
            ExternalChainCapability.Broadcast => capabilities.Broadcast,
            ExternalChainCapability.RealtimeIngest => capabilities.RealtimeIngest,
            ExternalChainCapability.BlockBackfill => capabilities.BlockBackfill,
            ExternalChainCapability.RawTxFetch => capabilities.RawTxFetch,
            ExternalChainCapability.ValidationFetch => capabilities.ValidationFetch,
            ExternalChainCapability.HistoricalAddressScan => capabilities.HistoricalAddressScan,
            ExternalChainCapability.HistoricalTokenScan => capabilities.HistoricalTokenScan,
            _ => null
        };

    private static IEnumerable<string> GetOverrideSources(RoutedCapabilityOverrideConfig overrideConfig)
    {
        if (overrideConfig is null)
            return [];

        if (overrideConfig is BroadcastCapabilityOverrideConfig broadcastOverride && broadcastOverride.Sources.Any())
            return broadcastOverride.Sources;

        return string.IsNullOrWhiteSpace(overrideConfig.Source)
            ? []
            : [overrideConfig.Source];
    }

    private static string FirstAllowed(
        IEnumerable<string> providers,
        string capability,
        ConsigliereSourcesConfig sourcesConfig,
        IReadOnlyDictionary<string, ExternalChainProviderDescriptor> descriptorByProvider
    )
        => AllowedProviders(providers, capability, sourcesConfig, descriptorByProvider).FirstOrDefault();

    private static IReadOnlyList<string> AllowedProviders(
        IEnumerable<string> providers,
        string capability,
        ConsigliereSourcesConfig sourcesConfig,
        IReadOnlyDictionary<string, ExternalChainProviderDescriptor> descriptorByProvider
    )
    {
        if (providers is null)
            return [];

        return providers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => CanServe(x, capability, sourcesConfig, descriptorByProvider))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool CanServe(
        string provider,
        string capability,
        ConsigliereSourcesConfig sourcesConfig,
        IReadOnlyDictionary<string, ExternalChainProviderDescriptor> descriptorByProvider
    )
    {
        if (string.Equals(provider, NodeProvider, StringComparison.OrdinalIgnoreCase))
        {
            return sourcesConfig.Providers.Node.Enabled &&
                sourcesConfig.Providers.Node.EnabledCapabilities.Contains(capability, StringComparer.OrdinalIgnoreCase);
        }

        if (!descriptorByProvider.TryGetValue(provider, out var descriptor))
            return false;

        if (!descriptor.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
            return false;

        SourceProviderConfig config = provider.ToLowerInvariant() switch
        {
            ExternalChainProviderName.Bitails => sourcesConfig.Providers.Bitails,
            ExternalChainProviderName.JungleBus => sourcesConfig.Providers.JungleBus,
            ExternalChainProviderName.WhatsOnChain => sourcesConfig.Providers.Whatsonchain,
            _ => null
        };

        return config is { Enabled: true } &&
            config.EnabledCapabilities.Contains(capability, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetLegacyPrimary(string capability, AppConfig legacyConfig)
        => capability switch
        {
            ExternalChainCapability.BlockBackfill => NodeProvider,
            ExternalChainCapability.RealtimeIngest => legacyConfig.JungleBus.Enabled
                ? ExternalChainProviderName.JungleBus
                : NodeProvider,
            ExternalChainCapability.Broadcast => NodeProvider,
            ExternalChainCapability.ValidationFetch => NodeProvider,
            ExternalChainCapability.RawTxFetch => legacyConfig.JungleBus.Enabled
                ? ExternalChainProviderName.JungleBus
                : ExternalChainProviderName.WhatsOnChain,
            ExternalChainCapability.HistoricalAddressScan => ExternalChainProviderName.Bitails,
            ExternalChainCapability.HistoricalTokenScan => ExternalChainProviderName.Bitails,
            _ => NodeProvider
        };

    private static IReadOnlyList<string> GetLegacyFallbacks(string capability, AppConfig legacyConfig)
        => capability switch
        {
            ExternalChainCapability.BlockBackfill when legacyConfig.JungleBus.Enabled => [ExternalChainProviderName.JungleBus],
            ExternalChainCapability.Broadcast => [ExternalChainProviderName.Bitails, ExternalChainProviderName.WhatsOnChain],
            ExternalChainCapability.RawTxFetch when legacyConfig.JungleBus.Enabled
                => [ExternalChainProviderName.WhatsOnChain, ExternalChainProviderName.Bitails],
            ExternalChainCapability.RawTxFetch => [ExternalChainProviderName.WhatsOnChain, ExternalChainProviderName.Bitails],
            ExternalChainCapability.ValidationFetch => [ExternalChainProviderName.Bitails, ExternalChainProviderName.WhatsOnChain],
            ExternalChainCapability.HistoricalAddressScan => [],
            ExternalChainCapability.HistoricalTokenScan => [],
            _ => []
        };

    private static string GetLegacyVerificationSource(AppConfig legacyConfig)
        => NodeProvider;
}
