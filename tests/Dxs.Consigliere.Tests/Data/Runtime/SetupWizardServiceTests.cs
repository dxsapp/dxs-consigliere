using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses.Admin;
using Dxs.Consigliere.Dto.Responses.Setup;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Options;

using Moq;

namespace Dxs.Consigliere.Tests.Data.Runtime;

public class SetupWizardServiceTests
{
    [Fact]
    public async Task GetOptionsAsync_ReturnsJungleBusBlockSyncDefaults()
    {
        var provider = CreateProviderConfigService();
        var service = CreateService(provider.Object);

        var options = await service.GetOptionsAsync();

        Assert.Equal("https://junglebus.gorillapool.io", options.BlockSync.BaseUrl);
        Assert.Equal("block-sub", options.BlockSync.BlockSubscriptionId);
    }

    [Fact]
    public async Task CompleteAsync_RejectsMissingJungleBusBlockSyncFields()
    {
        var provider = new Mock<IAdminProviderConfigService>(MockBehavior.Strict);
        var service = CreateService(provider.Object);

        var exception = await Assert.ThrowsAsync<SetupWizardException>(() =>
            service.CompleteAsync(
                new SetupCompleteRequest
                {
                    Admin = new SetupAdminAccessRequest { Enabled = false },
                    Providers = new SetupProviderSelectionRequest
                    {
                        RawTxPrimaryProvider = ExternalChainProviderName.JungleBus,
                        RestFallbackProvider = ExternalChainProviderName.WhatsOnChain,
                        RealtimePrimaryProvider = ExternalChainProviderName.Bitails,
                        BitailsTransport = BitailsRealtimeTransportMode.Websocket,
                        Whatsonchain = new AdminRestProviderConfigUpdateRequest
                        {
                            BaseUrl = "https://api.whatsonchain.com/v1/bsv/main"
                        }
                    }
                }));

        Assert.Equal("junglebus_block_sync_base_url_required", exception.Code);
    }

    [Fact]
    public async Task CompleteAsync_RejectsMissingJungleBusBlockSubscriptionId()
    {
        var provider = new Mock<IAdminProviderConfigService>(MockBehavior.Strict);
        var service = CreateService(provider.Object);

        var exception = await Assert.ThrowsAsync<SetupWizardException>(() =>
            service.CompleteAsync(
                new SetupCompleteRequest
                {
                    Admin = new SetupAdminAccessRequest { Enabled = false },
                    BlockSync = new SetupJungleBusBlockSyncRequest
                    {
                        BaseUrl = "https://junglebus.gorillapool.io"
                    },
                    Providers = new SetupProviderSelectionRequest
                    {
                        RawTxPrimaryProvider = ExternalChainProviderName.JungleBus,
                        RestFallbackProvider = ExternalChainProviderName.WhatsOnChain,
                        RealtimePrimaryProvider = ExternalChainProviderName.Bitails,
                        BitailsTransport = BitailsRealtimeTransportMode.Websocket,
                        Whatsonchain = new AdminRestProviderConfigUpdateRequest
                        {
                            BaseUrl = "https://api.whatsonchain.com/v1/bsv/main"
                        }
                    }
                }));

        Assert.Equal("junglebus_block_subscription_id_required", exception.Code);
    }

    [Fact]
    public async Task CompleteAsync_MergesBlockSyncIntoJungleBusProviderPayload()
    {
        AdminProviderConfigUpdateRequest? captured = null;
        var provider = new Mock<IAdminProviderConfigService>(MockBehavior.Strict);
        provider.Setup(x => x.ApplyProviderConfigAsync(
                It.IsAny<AdminProviderConfigUpdateRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminProviderConfigMutationResult(true))
            .Callback<AdminProviderConfigUpdateRequest, string, CancellationToken>((request, _, _) => captured = request);

        var service = CreateService(provider.Object);

        var status = await service.CompleteAsync(
            new SetupCompleteRequest
            {
                Admin = new SetupAdminAccessRequest { Enabled = false },
                BlockSync = new SetupJungleBusBlockSyncRequest
                {
                    BaseUrl = "https://junglebus.gorillapool.io",
                    BlockSubscriptionId = "block-sub"
                },
                Providers = new SetupProviderSelectionRequest
                {
                    RawTxPrimaryProvider = ExternalChainProviderName.JungleBus,
                    RestFallbackProvider = ExternalChainProviderName.WhatsOnChain,
                    RealtimePrimaryProvider = ExternalChainProviderName.Bitails,
                    BitailsTransport = BitailsRealtimeTransportMode.Websocket,
                    Junglebus = new AdminJungleBusProviderConfigUpdateRequest
                    {
                        MempoolSubscriptionId = "mempool-sub"
                    },
                    Whatsonchain = new AdminRestProviderConfigUpdateRequest
                    {
                        BaseUrl = "https://api.whatsonchain.com/v1/bsv/main"
                    }
                }
            });

        Assert.True(status.SetupCompleted);
        Assert.NotNull(captured);
        Assert.Equal("https://junglebus.gorillapool.io", captured!.Junglebus.BaseUrl);
        Assert.Equal("block-sub", captured.Junglebus.BlockSubscriptionId);
        Assert.Equal("mempool-sub", captured.Junglebus.MempoolSubscriptionId);
    }

    private static SetupWizardService CreateService(IAdminProviderConfigService providerConfigService)
        => new(
            new InMemorySetupBootstrapStore(),
            providerConfigService);

    private static Mock<IAdminProviderConfigService> CreateProviderConfigService()
    {
        var provider = new Mock<IAdminProviderConfigService>(MockBehavior.Strict);
        provider.Setup(x => x.GetProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateProvidersResponse());
        provider.Setup(x => x.GetEffectiveSourcesConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSourcesConfig());
        return provider;
    }

    private static AdminProvidersResponse CreateProvidersResponse()
        => new()
        {
            Recommendations = new AdminProviderRecommendationsResponse
            {
                RealtimePrimaryProvider = ExternalChainProviderName.Bitails,
                RestPrimaryProvider = ExternalChainProviderName.WhatsOnChain,
                RawTxFetchProvider = ExternalChainProviderName.JungleBus
            },
            Config = new AdminProviderConfigResponse
            {
                Static = CreateConfigValues("bitails", "junglebus", "whatsonchain", "websocket"),
                Effective = CreateConfigValues("bitails", "junglebus", "whatsonchain", "websocket"),
                AllowedRealtimePrimaryProviders = [ExternalChainProviderName.Bitails, ExternalChainProviderName.JungleBus],
                AllowedRawTxPrimaryProviders = [ExternalChainProviderName.JungleBus, ExternalChainProviderName.Bitails, ExternalChainProviderName.WhatsOnChain],
                AllowedRestPrimaryProviders = [ExternalChainProviderName.WhatsOnChain, ExternalChainProviderName.Bitails],
                AllowedBitailsTransports = [BitailsRealtimeTransportMode.Websocket, BitailsRealtimeTransportMode.Zmq]
            }
        };

    private static AdminProviderConfigValuesResponse CreateConfigValues(
        string realtime,
        string rawTx,
        string rest,
        string transport)
        => new()
        {
            RealtimePrimaryProvider = realtime,
            RawTxPrimaryProvider = rawTx,
            RestPrimaryProvider = rest,
            BitailsTransport = transport,
            Bitails = new AdminBitailsProviderConfigResponse
            {
                BaseUrl = "https://api.bitails.io",
                WebsocketBaseUrl = "https://api.bitails.io/global"
            },
            Whatsonchain = new AdminRestProviderConfigResponse
            {
                BaseUrl = "https://api.whatsonchain.com/v1/bsv/main"
            },
            Junglebus = new AdminJungleBusProviderConfigResponse
            {
                BaseUrl = "https://junglebus.gorillapool.io",
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
                PrimarySource = ExternalChainProviderName.Bitails,
                FallbackSources = [ExternalChainProviderName.JungleBus],
                VerificationSource = SourceCapabilityRouting.NodeProvider
            },
            Providers =
            {
                Bitails =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.RealtimeIngest, ExternalChainCapability.RawTxFetch],
                    Connection =
                    {
                        BaseUrl = "https://api.bitails.io",
                        Websocket =
                        {
                            BaseUrl = "https://api.bitails.io/global"
                        }
                    }
                },
                JungleBus =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.RealtimeIngest, ExternalChainCapability.RawTxFetch, ExternalChainCapability.BlockBackfill],
                    Connection =
                    {
                        BaseUrl = "https://junglebus.gorillapool.io"
                    }
                },
                Whatsonchain =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.RawTxFetch],
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
                    Source = ExternalChainProviderName.Bitails
                },
                RawTxFetch = new RoutedCapabilityOverrideConfig
                {
                    Source = ExternalChainProviderName.JungleBus
                },
                BlockBackfill = new RoutedCapabilityOverrideConfig
                {
                    Source = ExternalChainProviderName.JungleBus
                }
            }
        };

    private sealed class InMemorySetupBootstrapStore : ISetupBootstrapStore
    {
        private SetupBootstrapDocument _current = new()
        {
            Id = SetupBootstrapDocument.DocumentId,
            SetupCompleted = false,
            AdminEnabled = false,
            AdminUsername = string.Empty,
            AdminPasswordHash = string.Empty,
            UpdatedBy = "system-defaults"
        };

        public SetupBootstrapDocument Get() => _current;

        public Task<SetupBootstrapDocument> SaveAsync(SetupBootstrapDocument document, CancellationToken cancellationToken = default)
        {
            _current = document;
            return Task.FromResult(_current);
        }
    }
}
