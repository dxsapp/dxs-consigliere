using Dxs.Bsv.Rpc.Services;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Common.Journal;
using Dxs.Consigliere.BackgroundTasks.Blocks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Notifications;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;
using Dxs.Tests.Shared;

using MediatR;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using Raven.Client.Documents;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.BackgroundTasks.Blocks;

public class JungleBusBlockSyncOrchestrationTests : RavenTestDriver
{
    [Fact]
    public async Task BlockProcessExecutor_SkipsLegacyNodeContext_WhenBlockBackfillPrimaryIsJungleBus()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        const string blockHash = "0000000000000000000000000000000000000000000000000000000000000001";

        using (var seed = store.OpenAsyncSession())
        {
            await seed.StoreAsync(new BlockProcessContext
            {
                Id = blockHash,
                Scheduled = true,
                Start = 1,
                Finish = 1
            });
            await seed.SaveChangesAsync();
        }

        var rpcClient = new Mock<IRpcClient>(MockBehavior.Strict);
        var publisher = new Mock<IPublisher>(MockBehavior.Strict);
        publisher.Setup(x => x.Publish(It.IsAny<BlockProcessed>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var journal = new Mock<IObservationJournalAppender<ObservationJournalEntry<Dxs.Bsv.BitcoinMonitor.Models.BlockObservation>>>(MockBehavior.Strict);
        var providerConfigService = new Mock<IAdminProviderConfigService>(MockBehavior.Strict);
        providerConfigService.Setup(x => x.GetEffectiveSourcesConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateJungleBusFirstSources());

        var executor = new BlockProcessExecutor(
            rpcClient.Object,
            new ServiceCollection().BuildServiceProvider(),
            publisher.Object,
            store,
            journal.Object,
            providerConfigService.Object,
            Options.Create(new AppConfig()),
            CreateCatalog(),
            NullLogger<BlockProcessExecutor>.Instance
        );

        await executor.ExecuteAsync(blockHash, CancellationToken.None);

        using var verify = store.OpenAsyncSession();
        var context = await verify.LoadAsync<BlockProcessContext>(blockHash);
        Assert.NotNull(context);
        Assert.Contains(context.Messages, x => x.Contains("Skipped legacy node-sourced block context", StringComparison.Ordinal));
        Assert.Equal(0, context.ErrorsCount);
        Assert.Null(context.NextProcessAt);
        Assert.False(context.Scheduled);

        rpcClient.VerifyNoOtherCalls();
        journal.VerifyNoOtherCalls();
        providerConfigService.VerifyAll();
        publisher.VerifyAll();
    }

    [Fact]
    public async Task ActualChainTipVerify_SkipsNodeRpc_WhenBlockBackfillPrimaryIsJungleBus()
    {
        var rpcClient = new Mock<IRpcClient>(MockBehavior.Strict);
        var blockMessageBus = new Mock<IBlockMessageBus>(MockBehavior.Strict);
        var runtimeSourcePolicyService = new Mock<IAdminRuntimeSourcePolicyService>(MockBehavior.Strict);
        runtimeSourcePolicyService.Setup(x => x.GetEffectiveSourcesConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateJungleBusFirstSources());

        var task = new TestActualChainTipVerifyBackgroundTask(
            new Mock<IDocumentStore>(MockBehavior.Strict).Object,
            rpcClient.Object,
            blockMessageBus.Object,
            runtimeSourcePolicyService.Object,
            Options.Create(new AppConfig()),
            CreateCatalog(),
            NullLogger<ActualChainTipVerifyBackgroundTask>.Instance
        );

        await task.InvokeAsync(CancellationToken.None);

        rpcClient.VerifyNoOtherCalls();
        blockMessageBus.VerifyNoOtherCalls();
        runtimeSourcePolicyService.VerifyAll();
    }

    [Fact]
    public async Task JungleBusBlockSyncScheduler_SeedsFromObservedHeight_WhenDatabaseHasNoKnownBlocks()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var scheduler = new JungleBusBlockSyncScheduler(store, NullLogger<JungleBusBlockSyncScheduler>.Instance);

        await scheduler.ScheduleUpToHeightAsync(150, "block-sub", CancellationToken.None);

        using var session = store.OpenAsyncSession();
        var requests = await session.Query<SyncRequest>().ToListAsync();

        var request = Assert.Single(requests);
        Assert.Equal(150, request.FromHeight);
        Assert.Equal(150, request.ToHeight);
        Assert.Equal("block-sub", request.SubscriptionId);
    }

    [Fact]
    public async Task JungleBusBlockSyncScheduler_StartsFromNextHeight_WhenKnownBlockExists()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        using (var seed = store.OpenAsyncSession())
        {
            await seed.StoreAsync(new BlockProcessContext
            {
                Id = "known-block",
                Height = 120
            });
            await seed.SaveChangesAsync();
        }

        var scheduler = new JungleBusBlockSyncScheduler(store, NullLogger<JungleBusBlockSyncScheduler>.Instance);

        await scheduler.ScheduleUpToHeightAsync(123, "block-sub", CancellationToken.None);

        using var session = store.OpenAsyncSession();
        var request = Assert.Single(await session.Query<SyncRequest>().ToListAsync());
        Assert.Equal(121, request.FromHeight);
        Assert.Equal(123, request.ToHeight);
    }

    [Fact]
    public async Task JungleBusBlockSyncScheduler_DoesNotDuplicate_WhenPendingRangeAlreadyCoversObservedHeight()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        using (var seed = store.OpenAsyncSession())
        {
            await seed.StoreAsync(new SyncRequest
            {
                Id = "SyncRequests/JungleBus/block-sub/100-140",
                FromHeight = 100,
                ToHeight = 140,
                SubscriptionId = "block-sub",
                Finished = false
            });
            await seed.SaveChangesAsync();
        }

        var scheduler = new JungleBusBlockSyncScheduler(store, NullLogger<JungleBusBlockSyncScheduler>.Instance);

        await scheduler.ScheduleUpToHeightAsync(120, "block-sub", CancellationToken.None);

        using var session = store.OpenAsyncSession();
        var requests = await session.Query<SyncRequest>().ToListAsync();
        Assert.Single(requests);
    }

    private static ConsigliereSourcesConfig CreateJungleBusFirstSources()
    {
        var config = new ConsigliereSourcesConfig
        {
            Providers = SourceProvidersConfig.CreateDefaults(),
            Routing = SourceRoutingConfig.CreateDefaults(),
            Capabilities = SourceCapabilitiesConfig.CreateDefaults()
        };

        config.Capabilities.BlockBackfill.Source = ExternalChainProviderName.JungleBus;
        config.Capabilities.BlockBackfill.FallbackSources = ["node"];
        return config;
    }

    private static IExternalChainProviderCatalog CreateCatalog()
        => new FakeProviderCatalog(
            [
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.JungleBus,
                    [ExternalChainCapability.BlockBackfill, ExternalChainCapability.RawTxFetch, ExternalChainCapability.RealtimeIngest]),
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.Bitails,
                    [ExternalChainCapability.RealtimeIngest, ExternalChainCapability.ValidationFetch])
            ]);

    private sealed class FakeProviderCatalog(IReadOnlyCollection<ExternalChainProviderDescriptor> descriptors)
        : IExternalChainProviderCatalog
    {
        public IReadOnlyCollection<ExternalChainProviderDescriptor> GetDescriptors() => descriptors;

        public Task<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>> GetHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>>([]);
    }

    private sealed class TestActualChainTipVerifyBackgroundTask(
        IDocumentStore store,
        IRpcClient rpcClient,
        IBlockMessageBus blockMessageBus,
        IAdminRuntimeSourcePolicyService runtimeSourcePolicyService,
        IOptions<AppConfig> appConfig,
        IExternalChainProviderCatalog providerCatalog,
        ILogger<ActualChainTipVerifyBackgroundTask> logger
    ) : ActualChainTipVerifyBackgroundTask(
        store,
        rpcClient,
        blockMessageBus,
        runtimeSourcePolicyService,
        appConfig,
        providerCatalog,
        logger
    )
    {
        public override string Name => $"{nameof(ActualChainTipVerifyBackgroundTask)}-{Guid.NewGuid():N}";

        public Task InvokeAsync(CancellationToken cancellationToken) => RunAsync(cancellationToken);
    }
}
