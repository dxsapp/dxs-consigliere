using Dxs.Bsv.Rpc.Models.Responses;
using Dxs.Bsv.Rpc.Services;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.Common;
using Dxs.Infrastructure.JungleBus;
using Dxs.Infrastructure.WoC;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class RawTransactionFetchServiceTests
{
    [Fact]
    public async Task UsesConfiguredPrimaryProviderBeforeFallbacks()
    {
        var jungleBus = new Mock<IJungleBusRawTransactionClient>(MockBehavior.Strict);
        jungleBus.Setup(x => x.GetTransactionRawOrNullAsync("tx-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x01, 0x02]);

        var service = CreateService(
            CreateSourcesConfig(ExternalChainProviderName.JungleBus, [ExternalChainProviderName.WhatsOnChain]),
            jungleBus: jungleBus.Object);

        var result = await service.GetAsync("tx-1");

        Assert.NotNull(result);
        Assert.Equal(ExternalChainProviderName.JungleBus, result.Provider);
        Assert.Equal(new byte[] { 0x01, 0x02 }, result.Raw);
    }

    [Fact]
    public async Task FallsBackToWhatsOnChainWhenPrimaryReturnsNothing()
    {
        var jungleBus = new Mock<IJungleBusRawTransactionClient>(MockBehavior.Strict);
        jungleBus.Setup(x => x.GetTransactionRawOrNullAsync("tx-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null);

        var whatsOnChain = new Mock<IWhatsOnChainRestApiClient>(MockBehavior.Strict);
        whatsOnChain.Setup(x => x.GetTransactionRawOrNullAsync("tx-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync("0102");

        var service = CreateService(
            CreateSourcesConfig(ExternalChainProviderName.JungleBus, [ExternalChainProviderName.WhatsOnChain]),
            jungleBus: jungleBus.Object,
            whatsOnChain: whatsOnChain.Object);

        var result = await service.GetAsync("tx-2");

        Assert.NotNull(result);
        Assert.Equal(ExternalChainProviderName.WhatsOnChain, result.Provider);
        Assert.Equal(new byte[] { 0x01, 0x02 }, result.Raw);
    }

    [Fact]
    public async Task CanUseNodeRawTransactionFallback()
    {
        var rpcClient = new Mock<IRpcClient>(MockBehavior.Strict);
        rpcClient.Setup(x => x.GetRawTransactionAsString("tx-3"))
            .ReturnsAsync(new RpcResponseStringWithErrorDetails
            {
                Result = "0a0b"
            });

        var service = CreateService(
            CreateSourcesConfig(SourceCapabilityRouting.NodeProvider, []),
            rpcClient: rpcClient.Object);

        var result = await service.GetAsync("tx-3");

        Assert.NotNull(result);
        Assert.Equal(SourceCapabilityRouting.NodeProvider, result.Provider);
        Assert.Equal(new byte[] { 0x0a, 0x0b }, result.Raw);
    }

    private static RawTransactionFetchService CreateService(
        ConsigliereSourcesConfig config,
        IJungleBusRawTransactionClient jungleBus = null,
        IBitailsRestApiClient bitails = null,
        IWhatsOnChainRestApiClient whatsOnChain = null,
        IRpcClient rpcClient = null)
    {
        var providerConfigService = new Mock<IAdminProviderConfigService>(MockBehavior.Strict);
        providerConfigService.Setup(x => x.GetEffectiveSourcesConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        return new RawTransactionFetchService(
            providerConfigService.Object,
            Options.Create(new AppConfig()),
            CreateCatalog(),
            jungleBus ?? Mock.Of<IJungleBusRawTransactionClient>(MockBehavior.Strict),
            bitails ?? Mock.Of<IBitailsRestApiClient>(MockBehavior.Strict),
            whatsOnChain ?? Mock.Of<IWhatsOnChainRestApiClient>(MockBehavior.Strict),
            rpcClient ?? Mock.Of<IRpcClient>(MockBehavior.Strict),
            NullLogger<RawTransactionFetchService>.Instance);
    }

    private static ConsigliereSourcesConfig CreateSourcesConfig(string primary, string[] fallbacks)
        => new()
        {
            Routing =
            {
                PreferredMode = "hybrid",
                VerificationSource = SourceCapabilityRouting.NodeProvider
            },
            Providers =
            {
                Node =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.RawTxFetch]
                },
                JungleBus =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.RawTxFetch]
                },
                Bitails =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.RawTxFetch]
                },
                Whatsonchain =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.RawTxFetch]
                }
            },
            Capabilities =
            {
                RawTxFetch = new RoutedCapabilityOverrideConfig
                {
                    Source = primary,
                    FallbackSources = fallbacks
                }
            }
        };

    private static IExternalChainProviderCatalog CreateCatalog()
        => new FakeProviderCatalog(
            [
                new ExternalChainProviderDescriptor(ExternalChainProviderName.JungleBus, [ExternalChainCapability.RawTxFetch]),
                new ExternalChainProviderDescriptor(ExternalChainProviderName.Bitails, [ExternalChainCapability.RawTxFetch]),
                new ExternalChainProviderDescriptor(ExternalChainProviderName.WhatsOnChain, [ExternalChainCapability.RawTxFetch])
            ]);

    private sealed class FakeProviderCatalog(IReadOnlyCollection<ExternalChainProviderDescriptor> descriptors)
        : IExternalChainProviderCatalog
    {
        public IReadOnlyCollection<ExternalChainProviderDescriptor> GetDescriptors() => descriptors;

        public Task<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>> GetHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>>([]);
    }
}
