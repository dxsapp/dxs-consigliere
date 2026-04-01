using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class SourceCapabilityRoutingTests
{
    [Fact]
    public void Resolve_BlockBackfill_UsesLegacyNodePrimary_AndJungleBusFallback_WhenLegacyJungleBusIsEnabled()
    {
        var sourcesConfig = CreateSourcesConfig();
        var legacyConfig = new AppConfig
        {
            JungleBus = new JungleBusConfig { Enabled = true }
        };

        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.BlockBackfill,
            sourcesConfig,
            legacyConfig,
            CreateCatalog());

        Assert.Equal(ExternalChainCapability.BlockBackfill, route.Capability);
        Assert.Equal("hybrid", route.PreferredMode);
        Assert.Equal(SourceCapabilityRouting.NodeProvider, route.PrimarySource);
        Assert.Equal([ExternalChainProviderName.JungleBus], route.FallbackSources);
        Assert.Equal(SourceCapabilityRouting.NodeProvider, route.VerificationSource);
        Assert.Equal(SourceCapabilityRouting.NodeProvider, SourceCapabilityRouting.SelectForAttempt(route, 0));
        Assert.Equal(ExternalChainProviderName.JungleBus, SourceCapabilityRouting.SelectForAttempt(route, 1));
        Assert.Equal(ExternalChainProviderName.JungleBus, SourceCapabilityRouting.SelectForAttempt(route, 2));
    }

    [Fact]
    public void Resolve_BlockBackfill_UsesCapabilityOverridePrimary_OverLegacyDefaults()
    {
        var sourcesConfig = CreateSourcesConfig();
        sourcesConfig.Capabilities.BlockBackfill.Source = ExternalChainProviderName.JungleBus;

        var legacyConfig = new AppConfig
        {
            JungleBus = new JungleBusConfig { Enabled = false }
        };

        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.BlockBackfill,
            sourcesConfig,
            legacyConfig,
            CreateCatalog());

        Assert.Equal(ExternalChainProviderName.JungleBus, route.PrimarySource);
        Assert.Empty(route.FallbackSources);
        Assert.Equal(SourceCapabilityRouting.NodeProvider, route.VerificationSource);
    }

    [Fact]
    public void Resolve_UsesConfiguredVerificationSource_WhenAllowed()
    {
        var sourcesConfig = CreateSourcesConfig();
        sourcesConfig.Routing.VerificationSource = ExternalChainProviderName.Bitails;

        var legacyConfig = new AppConfig
        {
            JungleBus = new JungleBusConfig { Enabled = true }
        };

        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.BlockBackfill,
            sourcesConfig,
            legacyConfig,
            CreateCatalog());

        Assert.Equal(ExternalChainProviderName.Bitails, route.VerificationSource);
        Assert.Equal(SourceCapabilityRouting.NodeProvider, route.PrimarySource);
    }

    [Fact]
    public void Resolve_FallsBackToLegacyNodeVerificationSource_WhenUnset()
    {
        var sourcesConfig = CreateSourcesConfig();
        sourcesConfig.Routing.VerificationSource = null;

        var legacyConfig = new AppConfig
        {
            JungleBus = new JungleBusConfig { Enabled = true }
        };

        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.BlockBackfill,
            sourcesConfig,
            legacyConfig,
            CreateCatalog());

        Assert.Equal(SourceCapabilityRouting.NodeProvider, route.VerificationSource);
    }

    [Fact]
    public void Resolve_RealtimeIngest_CanSelectBitails_WhenProviderAndCapabilityAreConfigured()
    {
        var sourcesConfig = CreateSourcesConfig();
        sourcesConfig.Routing.PrimarySource = ExternalChainProviderName.Bitails;
        sourcesConfig.Providers.Bitails.Connection.Transport = Dxs.Consigliere.Configs.BitailsRealtimeTransportMode.Websocket;
        sourcesConfig.Providers.Bitails.EnabledCapabilities =
        [
            ExternalChainCapability.RealtimeIngest,
            ExternalChainCapability.ValidationFetch
        ];

        var legacyConfig = new AppConfig
        {
            JungleBus = new JungleBusConfig { Enabled = true }
        };

        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.RealtimeIngest,
            sourcesConfig,
            legacyConfig,
            CreateCatalog());

        Assert.Equal(ExternalChainProviderName.Bitails, route.PrimarySource);
        Assert.Empty(route.FallbackSources);
    }

    [Fact]
    public void Resolve_RawTxFetch_UsesLegacyJungleBusPrimary_WhenLegacyJungleBusIsEnabled()
    {
        var sourcesConfig = CreateSourcesConfig();
        var legacyConfig = new AppConfig
        {
            JungleBus = new JungleBusConfig { Enabled = true }
        };

        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.RawTxFetch,
            sourcesConfig,
            legacyConfig,
            CreateCatalog());

        Assert.Equal(ExternalChainProviderName.JungleBus, route.PrimarySource);
        Assert.Equal([ExternalChainProviderName.WhatsOnChain, ExternalChainProviderName.Bitails], route.FallbackSources);
    }

    [Fact]
    public void Resolve_ValidationFetch_UsesLegacyJungleBusPrimary_WhenLegacyJungleBusIsEnabled()
    {
        var sourcesConfig = CreateSourcesConfig();
        var legacyConfig = new AppConfig
        {
            JungleBus = new JungleBusConfig { Enabled = true }
        };

        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.ValidationFetch,
            sourcesConfig,
            legacyConfig,
            CreateCatalog());

        Assert.Equal(ExternalChainProviderName.JungleBus, route.PrimarySource);
        Assert.Equal([ExternalChainProviderName.Bitails, ExternalChainProviderName.WhatsOnChain], route.FallbackSources);
    }

    [Fact]
    public void Resolve_HistoricalAddressScan_DoesNotAdvertiseLegacyFallbackProviders()
    {
        var sourcesConfig = CreateSourcesConfig();
        var legacyConfig = new AppConfig
        {
            JungleBus = new JungleBusConfig { Enabled = true }
        };

        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.HistoricalAddressScan,
            sourcesConfig,
            legacyConfig,
            CreateCatalog());

        Assert.Equal(ExternalChainProviderName.Bitails, route.PrimarySource);
        Assert.Empty(route.FallbackSources);
    }

    [Fact]
    public void Resolve_HistoricalAddressScan_DoesNotInventLegacyFallbacks()
    {
        var sourcesConfig = CreateSourcesConfig();
        sourcesConfig.Providers.Bitails.EnabledCapabilities =
        [
            ExternalChainCapability.ValidationFetch,
            ExternalChainCapability.RawTxFetch,
            ExternalChainCapability.HistoricalAddressScan
        ];

        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.HistoricalAddressScan,
            sourcesConfig,
            new AppConfig { JungleBus = new JungleBusConfig { Enabled = true } },
            CreateCatalog());

        Assert.Equal(ExternalChainProviderName.Bitails, route.PrimarySource);
        Assert.Empty(route.FallbackSources);
    }

    [Fact]
    public void Resolve_HistoricalTokenScan_DoesNotAdvertiseLegacyFallbackProviders()
    {
        var sourcesConfig = CreateSourcesConfig();
        sourcesConfig.Providers.Bitails.EnabledCapabilities =
        [
            ExternalChainCapability.ValidationFetch,
            ExternalChainCapability.RawTxFetch,
            ExternalChainCapability.HistoricalTokenScan
        ];

        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.HistoricalTokenScan,
            sourcesConfig,
            new AppConfig { JungleBus = new JungleBusConfig { Enabled = true } },
            CreateCatalog());

        Assert.Equal(ExternalChainProviderName.Bitails, route.PrimarySource);
        Assert.Empty(route.FallbackSources);
    }

    private static ConsigliereSourcesConfig CreateSourcesConfig()
    {
        return new ConsigliereSourcesConfig
        {
            Routing = new SourceRoutingConfig
            {
                PreferredMode = "hybrid",
                PrimarySource = null,
                FallbackSources = [],
                VerificationSource = null
            },
            Capabilities = new SourceCapabilitiesConfig
            {
                Broadcast = new BroadcastCapabilityOverrideConfig(),
                RealtimeIngest = new RoutedCapabilityOverrideConfig(),
                BlockBackfill = new RoutedCapabilityOverrideConfig(),
                RawTxFetch = new RoutedCapabilityOverrideConfig(),
                ValidationFetch = new RoutedCapabilityOverrideConfig(),
                HistoricalAddressScan = new RoutedCapabilityOverrideConfig(),
                HistoricalTokenScan = new RoutedCapabilityOverrideConfig()
            },
            Providers = new SourceProvidersConfig
            {
                Node = new NodeSourceConfig
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.BlockBackfill, ExternalChainCapability.ValidationFetch]
                },
                JungleBus = new JungleBusSourceConfig
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.BlockBackfill, ExternalChainCapability.RawTxFetch, ExternalChainCapability.ValidationFetch]
                },
                Bitails = new BitailsSourceConfig
                {
                    Enabled = true,
                    Connection = new BitailsSourceConnectionConfig
                    {
                        BaseUrl = "https://api.bitails.io",
                        Websocket = new BitailsWebsocketConnectionConfig
                        {
                            BaseUrl = "https://api.bitails.io/global"
                        }
                    },
                    EnabledCapabilities = [ExternalChainCapability.ValidationFetch, ExternalChainCapability.RawTxFetch]
                },
                Whatsonchain = new WhatsOnChainSourceConfig
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.RawTxFetch]
                }
            }
        };
    }

    private static IExternalChainProviderCatalog CreateCatalog()
        => new FakeProviderCatalog(
            [
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.JungleBus,
                    [ExternalChainCapability.RealtimeIngest, ExternalChainCapability.BlockBackfill, ExternalChainCapability.RawTxFetch, ExternalChainCapability.ValidationFetch]),
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.Bitails,
                    [
                        ExternalChainCapability.Broadcast,
                        ExternalChainCapability.RealtimeIngest,
                        ExternalChainCapability.RawTxFetch,
                        ExternalChainCapability.ValidationFetch,
                        ExternalChainCapability.HistoricalAddressScan,
                        ExternalChainCapability.HistoricalTokenScan
                    ]),
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.WhatsOnChain,
                    [ExternalChainCapability.RawTxFetch])
            ]);

    private sealed class FakeProviderCatalog(IReadOnlyCollection<ExternalChainProviderDescriptor> descriptors)
        : IExternalChainProviderCatalog
    {
        public IReadOnlyCollection<ExternalChainProviderDescriptor> GetDescriptors() => descriptors;

        public Task<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>> GetHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>>([]);
    }
}
