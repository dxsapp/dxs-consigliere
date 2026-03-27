using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Data.Runtime;

public class AdminRuntimeSourcePolicyServiceTests
{
    [Fact]
    public async Task GetEffectiveSourcesConfigAsync_AppliesOverrideOnlyToRealtimePolicy()
    {
        var service = CreateService(
            CreateSourcesConfig(),
            new RealtimeSourcePolicyOverrideDocument
            {
                PrimaryRealtimeSource = ExternalChainProviderName.Bitails,
                BitailsTransport = BitailsRealtimeTransportMode.Zmq
            });

        var effective = await service.GetEffectiveSourcesConfigAsync();

        var realtime = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.RealtimeIngest,
            effective,
            new AppConfig { JungleBus = new JungleBusConfig { Enabled = true } },
            CreateCatalog());
        var blockBackfill = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.BlockBackfill,
            effective,
            new AppConfig { JungleBus = new JungleBusConfig { Enabled = true } },
            CreateCatalog());

        Assert.Equal(ExternalChainProviderName.Bitails, realtime.PrimarySource);
        Assert.Equal(BitailsRealtimeTransportMode.Zmq, effective.Providers.Bitails.Connection.Transport);
        Assert.Equal(SourceCapabilityRouting.NodeProvider, blockBackfill.PrimarySource);
    }

    [Fact]
    public async Task ApplyRealtimePolicyAsync_RejectsUnknownOrUnconfiguredValues()
    {
        var service = CreateService(CreateSourcesConfig(), null);

        var invalidPrimary = await service.ApplyRealtimePolicyAsync("whatsonchain", BitailsRealtimeTransportMode.Websocket, "admin");
        var invalidTransport = await service.ApplyRealtimePolicyAsync("bitails", "pipes", "admin");

        Assert.False(invalidPrimary.Success);
        Assert.Equal("invalid_primary_realtime_source", invalidPrimary.ErrorCode);
        Assert.False(invalidTransport.Success);
        Assert.Equal("invalid_bitails_transport", invalidTransport.ErrorCode);
    }

    [Fact]
    public async Task ApplyRealtimePolicyAsync_ResetsOverrideWhenValuesMatchStaticPolicy()
    {
        var store = new FakeRealtimeSourcePolicyOverrideStore();
        var service = CreateService(CreateSourcesConfig(), null, store);

        var result = await service.ApplyRealtimePolicyAsync(
            ExternalChainProviderName.JungleBus,
            BitailsRealtimeTransportMode.Websocket,
            "admin");

        Assert.True(result.Success);
        Assert.Equal(1, store.ResetCalls);
        Assert.Equal(0, store.UpsertCalls);
    }

    private static AdminRuntimeSourcePolicyService CreateService(
        ConsigliereSourcesConfig config,
        RealtimeSourcePolicyOverrideDocument currentOverride,
        FakeRealtimeSourcePolicyOverrideStore store = null)
    {
        store ??= new FakeRealtimeSourcePolicyOverrideStore(currentOverride);
        return new AdminRuntimeSourcePolicyService(
            store,
            Options.Create(config),
            Options.Create(new AppConfig { JungleBus = new JungleBusConfig { Enabled = true } }),
            CreateCatalog());
    }

    private static ConsigliereSourcesConfig CreateSourcesConfig()
        => new()
        {
            Routing =
            {
                PreferredMode = "hybrid",
                PrimarySource = ExternalChainProviderName.JungleBus,
                FallbackSources = [ExternalChainProviderName.Bitails],
                VerificationSource = SourceCapabilityRouting.NodeProvider
            },
            Providers =
            {
                Node =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.RealtimeIngest, ExternalChainCapability.BlockBackfill, ExternalChainCapability.ValidationFetch]
                },
                JungleBus =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.RealtimeIngest, ExternalChainCapability.BlockBackfill]
                },
                Bitails =
                {
                    Enabled = true,
                    EnabledCapabilities =
                    [
                        ExternalChainCapability.RealtimeIngest,
                        ExternalChainCapability.Broadcast,
                        ExternalChainCapability.RawTxFetch,
                        ExternalChainCapability.ValidationFetch
                    ],
                    Connection =
                    {
                        Transport = BitailsRealtimeTransportMode.Websocket,
                        BaseUrl = "https://api.bitails.io",
                        Websocket = new BitailsWebsocketConnectionConfig
                        {
                            BaseUrl = "https://socket.bitails.test/global"
                        },
                        Zmq = new BitailsZmqConnectionConfig
                        {
                            TxUrl = "tcp://127.0.0.1:28332"
                        }
                    }
                }
            },
            Capabilities =
            {
                BlockBackfill = new RoutedCapabilityOverrideConfig
                {
                    Source = SourceCapabilityRouting.NodeProvider
                }
            }
        };

    private static IExternalChainProviderCatalog CreateCatalog()
        => new FakeProviderCatalog(
            [
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.JungleBus,
                    [ExternalChainCapability.RealtimeIngest, ExternalChainCapability.BlockBackfill]),
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.Bitails,
                    [ExternalChainCapability.RealtimeIngest, ExternalChainCapability.Broadcast, ExternalChainCapability.RawTxFetch, ExternalChainCapability.ValidationFetch])
            ]);

    private sealed class FakeRealtimeSourcePolicyOverrideStore(RealtimeSourcePolicyOverrideDocument current = null)
        : IRealtimeSourcePolicyOverrideStore
    {
        private RealtimeSourcePolicyOverrideDocument _current = current;

        public int UpsertCalls { get; private set; }
        public int ResetCalls { get; private set; }

        public Task<RealtimeSourcePolicyOverrideDocument> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_current);

        public Task<RealtimeSourcePolicyOverrideDocument> UpsertAsync(
            string primaryRealtimeSource,
            string bitailsTransport,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            UpsertCalls++;
            _current = new RealtimeSourcePolicyOverrideDocument
            {
                Id = RealtimeSourcePolicyOverrideDocument.DocumentId,
                PrimaryRealtimeSource = primaryRealtimeSource,
                BitailsTransport = bitailsTransport,
                UpdatedBy = updatedBy
            };
            return Task.FromResult(_current);
        }

        public Task ResetAsync(CancellationToken cancellationToken = default)
        {
            ResetCalls++;
            _current = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProviderCatalog(IReadOnlyCollection<ExternalChainProviderDescriptor> descriptors)
        : IExternalChainProviderCatalog
    {
        public IReadOnlyCollection<ExternalChainProviderDescriptor> GetDescriptors() => descriptors;

        public Task<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>> GetHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>>([]);
    }
}
