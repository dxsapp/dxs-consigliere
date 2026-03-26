using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Common.Cache;
using Dxs.Infrastructure.Common;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Controllers;

public class OpsControllerTests
{
    [Fact]
    public async Task GetProviders_ReturnsProviderFirstStatusWithNestedCapabilities()
    {
        var controller = new OpsController(
            Options.Create(new ConsigliereSourcesConfig
            {
                Routing =
                {
                    PrimarySource = "junglebus",
                    FallbackSources = ["bitails"],
                    VerificationSource = "node"
                },
                Providers =
                {
                    Node =
                    {
                        Enabled = true,
                        EnabledCapabilities =
                        [
                            ExternalChainCapability.Broadcast,
                            ExternalChainCapability.BlockBackfill,
                            ExternalChainCapability.ValidationFetch
                        ]
                    },
                    JungleBus =
                    {
                        Enabled = true,
                        EnabledCapabilities =
                        [
                            ExternalChainCapability.RealtimeIngest,
                            ExternalChainCapability.BlockBackfill
                        ]
                    },
                    Bitails =
                    {
                        Enabled = true,
                        EnabledCapabilities =
                        [
                            ExternalChainCapability.Broadcast,
                            ExternalChainCapability.RawTxFetch,
                            ExternalChainCapability.ValidationFetch
                        ]
                    },
                    Whatsonchain =
                    {
                        Enabled = true,
                        EnabledCapabilities =
                        [
                            ExternalChainCapability.Broadcast,
                            ExternalChainCapability.BlockBackfill,
                            ExternalChainCapability.RawTxFetch,
                            ExternalChainCapability.ValidationFetch
                        ]
                    }
                }
            }),
            Options.Create(new ConsigliereCacheConfig
            {
                Enabled = true,
                Backend = "memory",
                MaxEntries = 256
            }),
            Options.Create(new AppConfig
            {
                JungleBus = new JungleBusConfig { Enabled = true }
            }),
            new FakeProviderCatalog(
                [
                    new ExternalChainProviderDescriptor(
                        ExternalChainProviderName.JungleBus,
                        [ExternalChainCapability.RealtimeIngest, ExternalChainCapability.BlockBackfill]
                    ),
                    new ExternalChainProviderDescriptor(
                        ExternalChainProviderName.Bitails,
                        [ExternalChainCapability.Broadcast, ExternalChainCapability.RawTxFetch, ExternalChainCapability.ValidationFetch],
                        new ExternalChainRateLimitHint(600, "bitails-limit")
                    ),
                    new ExternalChainProviderDescriptor(
                        ExternalChainProviderName.WhatsOnChain,
                        [ExternalChainCapability.Broadcast, ExternalChainCapability.BlockBackfill, ExternalChainCapability.RawTxFetch, ExternalChainCapability.ValidationFetch],
                        new ExternalChainRateLimitHint(180, "woc-limit")
                    )
                ])
            ,
            new FakeProjectionReadCacheTelemetry()
        );

        var action = await controller.GetProviders(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(action);
        var payload = Assert.IsAssignableFrom<IReadOnlyCollection<ProviderStatusResponse>>(ok.Value);

        var providers = payload.ToDictionary(x => x.Provider, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(["primary"], providers["junglebus"].Roles);
        Assert.Equal(["fallback"], providers["bitails"].Roles);
        Assert.Equal(["verification"], providers["node"].Roles);

        Assert.True(providers["junglebus"].Capabilities[ExternalChainCapability.BlockBackfill].Active);
        Assert.True(providers["bitails"].Capabilities[ExternalChainCapability.Broadcast].Enabled);
        Assert.False(providers["bitails"].Capabilities[ExternalChainCapability.Broadcast].Active);
        Assert.Equal("provider", providers["bitails"].RateLimitState.Scope);
        Assert.Equal("bitails-limit", providers["bitails"].RateLimitState.SourceHint);
    }

    [Fact]
    public void GetProjectionCache_ReturnsProjectionCacheMetrics()
    {
        var controller = new OpsController(
            Options.Create(new ConsigliereSourcesConfig()),
            Options.Create(new ConsigliereCacheConfig
            {
                Enabled = true,
                Backend = "memory",
                MaxEntries = 512
            }),
            Options.Create(new AppConfig()),
            new FakeProviderCatalog([]),
            new FakeProjectionReadCacheTelemetry());

        var result = controller.GetProjectionCache();

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ProjectionCacheStatusResponse>(ok.Value);
        Assert.True(payload.Enabled);
        Assert.Equal("memory", payload.Backend);
        Assert.Equal(7, payload.Count);
        Assert.Equal(10, payload.Hits);
        Assert.Equal(5, payload.Misses);
        Assert.Equal(2, payload.InvalidatedTags);
        Assert.Equal(512, payload.MaxEntries);
    }

    private sealed class FakeProviderCatalog(IReadOnlyCollection<ExternalChainProviderDescriptor> descriptors)
        : IExternalChainProviderCatalog
    {
        public IReadOnlyCollection<ExternalChainProviderDescriptor> GetDescriptors() => descriptors;

        public Task<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>> GetHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>>(
                [
                    new ExternalChainProviderHealthSnapshot("junglebus", ExternalChainHealthState.Healthy),
                    new ExternalChainProviderHealthSnapshot("bitails", ExternalChainHealthState.Unknown),
                    new ExternalChainProviderHealthSnapshot("whatsonchain", ExternalChainHealthState.Unknown)
                ]
            );
    }

    private sealed class FakeProjectionReadCacheTelemetry : IProjectionReadCacheTelemetry
    {
        public ProjectionCacheStatsSnapshot GetSnapshot()
            => new(
                "memory",
                true,
                7,
                512,
                10,
                5,
                6,
                4,
                2,
                1);
    }
}
