using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Data.Runtime;

public class AdminRuntimeSourcePolicyServiceTests
{
    [Fact]
    public async Task GetEffectiveSourcesConfigAsync_AppliesRealtimeAndRestOverrides()
    {
        var service = CreateService(
            CreateSourcesConfig(),
            new RealtimeSourcePolicyOverrideDocument
            {
                PrimaryRealtimeSource = ExternalChainProviderName.Bitails,
                RawTxPrimaryProvider = ExternalChainProviderName.JungleBus,
                RestPrimaryProvider = ExternalChainProviderName.WhatsOnChain,
                BitailsTransport = BitailsRealtimeTransportMode.Zmq,
                WhatsonchainBaseUrl = "https://api.whatsonchain.com/v1/bsv/main"
            });

        var effective = await service.GetEffectiveSourcesConfigAsync();

        var realtime = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.RealtimeIngest,
            effective,
            CreateAppConfig(),
            CreateCatalog());
        var rawTx = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.RawTxFetch,
            effective,
            CreateAppConfig(),
            CreateCatalog());

        Assert.Equal(ExternalChainProviderName.Bitails, realtime.PrimarySource);
        Assert.Equal(BitailsRealtimeTransportMode.Zmq, effective.Providers.Bitails.Connection.Transport);
        Assert.Equal(ExternalChainProviderName.JungleBus, rawTx.PrimarySource);
        Assert.Equal([ExternalChainProviderName.WhatsOnChain], rawTx.FallbackSources);
    }

    [Fact]
    public async Task ApplyProviderConfigAsync_RejectsUnconfiguredRealtimeAndRestSelections()
    {
        var service = CreateService(CreateSourcesConfig(), null);
        var restConfig = CreateSourcesConfig();
        restConfig.Providers.Whatsonchain.Connection.BaseUrl = string.Empty;
        var serviceWithMissingRestBase = CreateService(restConfig, null);

        var invalidRealtime = await service.ApplyProviderConfigAsync(
            new AdminProviderConfigUpdateRequest
            {
                RealtimePrimaryProvider = ExternalChainProviderName.WhatsOnChain,
                RawTxPrimaryProvider = ExternalChainProviderName.JungleBus,
                RestPrimaryProvider = ExternalChainProviderName.WhatsOnChain,
                BitailsTransport = BitailsRealtimeTransportMode.Websocket,
                Whatsonchain = new AdminRestProviderConfigUpdateRequest
                {
                    BaseUrl = "https://api.whatsonchain.com/v1/bsv/main"
                }
            },
            "admin");

        var invalidRest = await serviceWithMissingRestBase.ApplyProviderConfigAsync(
            new AdminProviderConfigUpdateRequest
            {
                RealtimePrimaryProvider = ExternalChainProviderName.Bitails,
                RawTxPrimaryProvider = ExternalChainProviderName.JungleBus,
                RestPrimaryProvider = ExternalChainProviderName.WhatsOnChain,
                BitailsTransport = BitailsRealtimeTransportMode.Websocket,
                Whatsonchain = new AdminRestProviderConfigUpdateRequest()
            },
            "admin");

        Assert.False(invalidRealtime.Success);
        Assert.Equal("invalid_realtime_primary_provider", invalidRealtime.ErrorCode);
        Assert.False(invalidRest.Success);
        Assert.Equal("whatsonchain_base_url_required", invalidRest.ErrorCode);
    }

    [Fact]
    public async Task ApplyProviderConfigAsync_ResetsOverrideWhenRequestedValuesMatchStaticConfiguration()
    {
        var store = new FakeRealtimeSourcePolicyOverrideStore();
        var service = CreateService(CreateSourcesConfig(), null, store);

        var result = await service.ApplyProviderConfigAsync(
            new AdminProviderConfigUpdateRequest
            {
                RealtimePrimaryProvider = ExternalChainProviderName.JungleBus,
                RawTxPrimaryProvider = ExternalChainProviderName.JungleBus,
                RestPrimaryProvider = ExternalChainProviderName.Bitails,
                BitailsTransport = BitailsRealtimeTransportMode.Websocket,
                Bitails = new AdminBitailsProviderConfigUpdateRequest
                {
                    BaseUrl = "https://api.bitails.io",
                    WebsocketBaseUrl = "https://socket.bitails.test/global",
                    ZmqTxUrl = "tcp://127.0.0.1:28332",
                    ZmqBlockUrl = "tcp://127.0.0.1:28333"
                },
                Junglebus = new AdminJungleBusProviderConfigUpdateRequest
                {
                    BaseUrl = "https://junglebus.gorillapool.io",
                    MempoolSubscriptionId = "mempool-sub",
                    BlockSubscriptionId = "block-sub"
                },
                Whatsonchain = new AdminRestProviderConfigUpdateRequest
                {
                    BaseUrl = "https://api.whatsonchain.com/v1/bsv/main"
                }
            },
            "admin");

        Assert.True(result.Success);
        Assert.Equal(0, store.ResetCalls);
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public async Task GetProvidersAsync_ReturnsRecommendedDefaultsAndCatalog()
    {
        var service = CreateService(CreateSourcesConfig(), null);

        var result = await service.GetProvidersAsync();

        Assert.Equal(ExternalChainProviderName.Bitails, result.Recommendations.RealtimePrimaryProvider);
        Assert.Equal(ExternalChainProviderName.WhatsOnChain, result.Recommendations.RestPrimaryProvider);
        Assert.Equal(ExternalChainProviderName.JungleBus, result.Recommendations.RawTxFetchProvider);
        Assert.Contains(result.Providers, x => x.ProviderId == ExternalChainProviderName.Bitails);
        Assert.Contains(result.Providers, x => x.ProviderId == ExternalChainProviderName.WhatsOnChain);
        Assert.Contains(result.Providers, x => x.ProviderId == ExternalChainProviderName.JungleBus);
        Assert.Contains(result.Providers, x => x.ProviderId == SourceCapabilityRouting.NodeProvider);
        Assert.Contains(result.Providers, x => x.ProviderId == ExternalChainProviderName.JungleBus && x.RecommendedFor.Contains("raw_tx_fetch"));
        Assert.Contains(result.Providers, x => x.ProviderId == ExternalChainProviderName.Bitails && !x.MissingRequirements.Contains("bitails_api_key_required"));
        Assert.False(result.Config.RestartRequired);
    }

    [Fact]
    public async Task GetProvidersAsync_RequiresRestart_ForAdvancedNodeOrZmqRuntimeSelections()
    {
        var serviceWithNode = CreateService(
            CreateSourcesConfig(),
            new RealtimeSourcePolicyOverrideDocument
            {
                PrimaryRealtimeSource = SourceCapabilityRouting.NodeProvider,
                RawTxPrimaryProvider = ExternalChainProviderName.JungleBus,
                RestPrimaryProvider = ExternalChainProviderName.WhatsOnChain,
                BitailsTransport = BitailsRealtimeTransportMode.Websocket,
                WhatsonchainBaseUrl = "https://api.whatsonchain.com/v1/bsv/main"
            });

        var serviceWithBitailsZmq = CreateService(
            CreateSourcesConfig(),
            new RealtimeSourcePolicyOverrideDocument
            {
                PrimaryRealtimeSource = ExternalChainProviderName.Bitails,
                RawTxPrimaryProvider = ExternalChainProviderName.JungleBus,
                RestPrimaryProvider = ExternalChainProviderName.WhatsOnChain,
                BitailsTransport = BitailsRealtimeTransportMode.Zmq,
                BitailsBaseUrl = "https://api.bitails.io",
                BitailsZmqTxUrl = "tcp://127.0.0.1:28332",
                BitailsZmqBlockUrl = "tcp://127.0.0.1:28333",
                WhatsonchainBaseUrl = "https://api.whatsonchain.com/v1/bsv/main"
            });

        var nodeResult = await serviceWithNode.GetProvidersAsync();
        var bitailsZmqResult = await serviceWithBitailsZmq.GetProvidersAsync();

        Assert.True(nodeResult.Config.RestartRequired);
        Assert.True(bitailsZmqResult.Config.RestartRequired);
    }

    private static AdminProviderConfigService CreateService(
        ConsigliereSourcesConfig config,
        RealtimeSourcePolicyOverrideDocument currentOverride,
        FakeRealtimeSourcePolicyOverrideStore store = null)
    {
        store ??= new FakeRealtimeSourcePolicyOverrideStore(currentOverride);
        return new AdminProviderConfigService(
            store,
            Options.Create(config),
            Options.Create(CreateAppConfig()),
            CreateCatalog());
    }

    private static AppConfig CreateAppConfig()
        => new()
        {
            JungleBus = new JungleBusConfig
            {
                Enabled = true,
                MempoolSubscriptionId = "mempool-sub",
                BlockSubscriptionId = "block-sub"
            }
        };

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
                    EnabledCapabilities = [ExternalChainCapability.RealtimeIngest, ExternalChainCapability.BlockBackfill, ExternalChainCapability.ValidationFetch],
                    Connection =
                    {
                        ZmqTxUrl = "tcp://127.0.0.1:28332"
                    }
                },
                JungleBus =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.RawTxFetch, ExternalChainCapability.RealtimeIngest, ExternalChainCapability.BlockBackfill],
                    Connection =
                    {
                        BaseUrl = "https://junglebus.gorillapool.io"
                    }
                },
                Bitails =
                {
                    Enabled = true,
                    EnabledCapabilities =
                    [
                        ExternalChainCapability.RealtimeIngest,
                        ExternalChainCapability.Broadcast,
                        ExternalChainCapability.RawTxFetch,
                        ExternalChainCapability.ValidationFetch,
                        ExternalChainCapability.HistoricalAddressScan,
                        ExternalChainCapability.HistoricalTokenScan
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
                            TxUrl = "tcp://127.0.0.1:28332",
                            BlockUrl = "tcp://127.0.0.1:28333"
                        }
                    }
                },
                Whatsonchain =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.RawTxFetch, ExternalChainCapability.ValidationFetch],
                    Connection =
                    {
                        BaseUrl = "https://api.whatsonchain.com/v1/bsv/main"
                    }
                }
            },
            Capabilities =
            {
                RealtimeIngest = new RoutedCapabilityOverrideConfig
                {
                    Source = ExternalChainProviderName.JungleBus
                },
                RawTxFetch = new RoutedCapabilityOverrideConfig
                {
                    Source = ExternalChainProviderName.Bitails
                },
                ValidationFetch = new RoutedCapabilityOverrideConfig
                {
                    Source = ExternalChainProviderName.Bitails
                },
                BlockBackfill = new RoutedCapabilityOverrideConfig
                {
                    Source = SourceCapabilityRouting.NodeProvider
                },
                HistoricalAddressScan = new RoutedCapabilityOverrideConfig
                {
                    Source = ExternalChainProviderName.Bitails
                },
                HistoricalTokenScan = new RoutedCapabilityOverrideConfig
                {
                    Source = ExternalChainProviderName.Bitails
                }
            }
        };

    private static IExternalChainProviderCatalog CreateCatalog()
        => new FakeProviderCatalog(
            [
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.JungleBus,
                    [ExternalChainCapability.RawTxFetch, ExternalChainCapability.RealtimeIngest, ExternalChainCapability.BlockBackfill]),
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.Bitails,
                    [
                        ExternalChainCapability.RealtimeIngest,
                        ExternalChainCapability.Broadcast,
                        ExternalChainCapability.RawTxFetch,
                        ExternalChainCapability.ValidationFetch,
                        ExternalChainCapability.HistoricalAddressScan,
                        ExternalChainCapability.HistoricalTokenScan
                    ]),
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.WhatsOnChain,
                    [ExternalChainCapability.RawTxFetch, ExternalChainCapability.ValidationFetch])
            ]);

    private sealed class FakeRealtimeSourcePolicyOverrideStore(RealtimeSourcePolicyOverrideDocument current = null)
        : IRealtimeSourcePolicyOverrideStore
    {
        private RealtimeSourcePolicyOverrideDocument _current = current;

        public int SaveCalls { get; private set; }
        public int ResetCalls { get; private set; }

        public Task<RealtimeSourcePolicyOverrideDocument> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_current);

        public Task<RealtimeSourcePolicyOverrideDocument> SaveAsync(
            RealtimeSourcePolicyOverrideDocument document,
            CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            document.Id = RealtimeSourcePolicyOverrideDocument.DocumentId;
            _current = document;
            return Task.FromResult(_current);
        }

        public Task<RealtimeSourcePolicyOverrideDocument> UpsertAsync(
            string primaryRealtimeSource,
            string bitailsTransport,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
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
