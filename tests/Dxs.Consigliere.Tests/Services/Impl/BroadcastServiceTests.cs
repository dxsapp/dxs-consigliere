using Dxs.Bsv;
using Dxs.Bsv.Factories;
using Dxs.Bsv.Models;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.Bitails.Dto;
using Dxs.Infrastructure.Common;
using Dxs.Infrastructure.WoC;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Raven.TestDriver;
using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class BroadcastServiceTests : RavenTestDriver
{
    private const string SampleTransactionHex = "0100000001c6f4b6176d3f4d6c6d9e198ba89a4eb7a1b08e6a705cc8cf0f8f2f3e3bcedf1f000000006b4830450221009af2d63b8ef3ebf8c7a227327d8e1a89f5929087566bbb6d6f74a09a87e2375d022007f8cefa32f6d829bb3f8792dd11e5d8f1cb4e4f4f84f7a8d431fed0b8ff103a4121022b698a0f0a1f1fb43fb8f33c2d72cbe7f3f8d98ef1a304681140f64e5681970fffffffff02e8030000000000001976a91489abcdefabbaabbaabbaabbaabbaabbaabbaabba88ac0000000000000000066a040102030400000000";

    [Fact]
    public async Task Broadcast_Succeeds_WhenAnyConfiguredProviderAccepts()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var bitcoind = new Mock<IBitcoindService>(MockBehavior.Strict);
        bitcoind.Setup(x => x.Broadcast(It.IsAny<string>()))
            .ReturnsAsync((false, "missing-inputs", "rpc-25"));

        var bitails = new Mock<IBitailsRestApiClient>(MockBehavior.Strict);
        bitails.Setup(x => x.Broadcast(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BroadcastResponseDto { TxId = "accepted" });

        var whatsOnChain = new Mock<IWhatsOnChainRestApiClient>(MockBehavior.Strict);
        var utxoCache = new Mock<IUtxoCache>(MockBehavior.Strict);
        utxoCache.Setup(x => x.MarkUsed(It.IsAny<OutPoint>(), false));

        var service = CreateService(
            store,
            CreateBroadcastConfig([SourceCapabilityRouting.NodeProvider, ExternalChainProviderName.Bitails]),
            bitcoind.Object,
            bitails.Object,
            whatsOnChain.Object,
            utxoCache.Object);

        var result = await service.Broadcast(SampleTransactionHex);

        Assert.True(result.Success);
        Assert.Equal(ExternalChainProviderName.Bitails, result.Code);
        Assert.Contains("Bitails", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, result.Attempts.Length);
        Assert.Contains(result.Attempts, x => x.Provider == SourceCapabilityRouting.NodeProvider && !x.Success);
        Assert.Contains(result.Attempts, x => x.Provider == ExternalChainProviderName.Bitails && x.Success);
        utxoCache.Verify(x => x.MarkUsed(It.IsAny<OutPoint>(), false), Times.Once);
    }

    [Fact]
    public async Task Broadcast_Fails_WhenAllConfiguredProvidersReject()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var bitcoind = new Mock<IBitcoindService>(MockBehavior.Strict);
        bitcoind.Setup(x => x.Broadcast(It.IsAny<string>()))
            .ReturnsAsync((false, "missing-inputs", "rpc-25"));

        var bitails = new Mock<IBitailsRestApiClient>(MockBehavior.Strict);
        bitails.Setup(x => x.Broadcast(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BroadcastResponseDto
            {
                Error = new BroadcastResponseDto.BroadcastErrorDto
                {
                    Code = 409,
                    Message = "already known"
                }
            });

        var whatsOnChain = new Mock<IWhatsOnChainRestApiClient>(MockBehavior.Strict);
        var utxoCache = new Mock<IUtxoCache>(MockBehavior.Strict);

        var service = CreateService(
            store,
            CreateBroadcastConfig([SourceCapabilityRouting.NodeProvider, ExternalChainProviderName.Bitails]),
            bitcoind.Object,
            bitails.Object,
            whatsOnChain.Object,
            utxoCache.Object);

        var result = await service.Broadcast(SampleTransactionHex);

        Assert.False(result.Success);
        Assert.Equal(2, result.Attempts.Length);
        Assert.Contains("missing-inputs", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("already known", result.Message, StringComparison.OrdinalIgnoreCase);
        utxoCache.Verify(x => x.MarkUsed(It.IsAny<OutPoint>(), false), Times.Never);
    }

    private static BroadcastService CreateService(
        Raven.Client.Documents.IDocumentStore store,
        ConsigliereSourcesConfig config,
        IBitcoindService bitcoindService,
        IBitailsRestApiClient bitailsRestApiClient,
        IWhatsOnChainRestApiClient whatsOnChainRestApiClient,
        IUtxoCache utxoCache)
    {
        var providerConfigService = new Mock<IAdminProviderConfigService>(MockBehavior.Strict);
        providerConfigService.Setup(x => x.GetEffectiveSourcesConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        return new BroadcastService(
            bitcoindService,
            bitailsRestApiClient,
            whatsOnChainRestApiClient,
            providerConfigService.Object,
            CreateCatalog(),
            store,
            utxoCache,
            new TestNetworkProvider(),
            Options.Create(new AppConfig()),
            NullLogger<BroadcastService>.Instance);
    }

    private static ConsigliereSourcesConfig CreateBroadcastConfig(string[] sources)
        => new()
        {
            Providers =
            {
                Node =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.Broadcast]
                },
                Bitails =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.Broadcast]
                },
                Whatsonchain =
                {
                    Enabled = true,
                    EnabledCapabilities = [ExternalChainCapability.Broadcast]
                }
            },
            Capabilities =
            {
                Broadcast = new BroadcastCapabilityOverrideConfig
                {
                    Mode = "multi",
                    Sources = sources
                }
            }
        };

    private static IExternalChainProviderCatalog CreateCatalog()
        => new FakeProviderCatalog(
            [
                new ExternalChainProviderDescriptor(ExternalChainProviderName.Bitails, [ExternalChainCapability.Broadcast]),
                new ExternalChainProviderDescriptor(ExternalChainProviderName.WhatsOnChain, [ExternalChainCapability.Broadcast])
            ]);

    private sealed class FakeProviderCatalog(IReadOnlyCollection<ExternalChainProviderDescriptor> descriptors)
        : IExternalChainProviderCatalog
    {
        public IReadOnlyCollection<ExternalChainProviderDescriptor> GetDescriptors() => descriptors;

        public Task<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>> GetHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>>([]);
    }

    private sealed class TestNetworkProvider : INetworkProvider
    {
        public Dxs.Bsv.Network Network => Dxs.Bsv.Network.Mainnet;
    }
}
