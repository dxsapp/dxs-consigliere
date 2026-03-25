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

    private static ConsigliereSourcesConfig CreateSourcesConfig()
    {
        return new ConsigliereSourcesConfig
        {
            Routing =
            {
                PreferredMode = "hybrid"
            },
            Providers =
            {
                Node =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.BlockBackfill, ExternalChainCapability.ValidationFetch]
                },
                JungleBus =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.BlockBackfill]
                },
                Bitails =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.ValidationFetch]
                }
            }
        };
    }

    private static IExternalChainProviderCatalog CreateCatalog()
        => new FakeProviderCatalog(
            [
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.JungleBus,
                    [ExternalChainCapability.RealtimeIngest, ExternalChainCapability.BlockBackfill]),
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.Bitails,
                    [ExternalChainCapability.Broadcast, ExternalChainCapability.RawTxFetch, ExternalChainCapability.ValidationFetch])
            ]);

    private sealed class FakeProviderCatalog(IReadOnlyCollection<ExternalChainProviderDescriptor> descriptors)
        : IExternalChainProviderCatalog
    {
        public IReadOnlyCollection<ExternalChainProviderDescriptor> GetDescriptors() => descriptors;

        public Task<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>> GetHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>>([]);
    }
}
