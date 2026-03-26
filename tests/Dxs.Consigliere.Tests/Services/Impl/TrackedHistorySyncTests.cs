using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;
using Newtonsoft.Json;
using Raven.TestDriver;
using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class TrackedHistorySyncTests : RavenTestDriver
{
    private const string Address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    private const string TokenId = "1111111111111111111111111111111111111111";
    private const string TrustedRoot = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task ForwardOnlyAddress_BecomesForwardLiveAfterBoundaryInitialization()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var registration = new Dxs.Consigliere.Data.Tracking.TrackedEntityRegistrationStore(store);
        var orchestrator = new TrackedEntityLifecycleOrchestrator(store);
        var readiness = new TrackedEntityReadinessService(store);

        await registration.RegisterAddressAsync(Address, "Genesis", TrackedEntityHistoryMode.ForwardOnly);
        await orchestrator.BeginTrackingAddressAsync(Address);

        var snapshot = await readiness.GetAddressReadinessAsync(Address);
        Assert.True(snapshot.Readable);
        Assert.Equal(TrackedEntityHistoryReadiness.ForwardLive, snapshot.History.HistoryReadiness);
        Assert.Equal(TrackedEntityHistoryMode.ForwardOnly, snapshot.History.Coverage.Mode);
        Assert.False(snapshot.History.Coverage.FullCoverage);
    }

    [Fact]
    public async Task FullHistoryUpgrade_QueuesBackfillWhileStateStaysLive()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var registration = new Dxs.Consigliere.Data.Tracking.TrackedEntityRegistrationStore(store);
        var orchestrator = new TrackedEntityLifecycleOrchestrator(store);
        var scheduler = new TrackedHistoryBackfillScheduler(store, orchestrator);
        var readiness = new TrackedEntityReadinessService(store);

        await registration.RegisterAddressAsync(Address, "Genesis", TrackedEntityHistoryMode.ForwardOnly);
        await orchestrator.BeginTrackingAddressAsync(Address);
        await registration.RequestAddressFullHistoryAsync(Address);
        await orchestrator.BeginTrackingAddressAsync(Address);
        await scheduler.QueueAddressFullHistoryAsync(Address);

        var snapshot = await readiness.GetAddressReadinessAsync(Address);
        Assert.True(snapshot.Readable);
        Assert.Equal(TrackedEntityHistoryReadiness.BackfillingFullHistory, snapshot.History.HistoryReadiness);
        Assert.Equal(HistoryBackfillExecutionStatus.Queued, snapshot.History.BackfillStatus?.Status);
    }

    [Fact]
    public async Task HistoryGate_DeniesForwardOnlyWithoutPartialOptIn_AndAllowsWithOptIn()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var registration = new Dxs.Consigliere.Data.Tracking.TrackedEntityRegistrationStore(store);
        var orchestrator = new TrackedEntityLifecycleOrchestrator(store);
        var readiness = new TrackedEntityReadinessService(store);

        await registration.RegisterAddressAsync(Address, "Genesis", TrackedEntityHistoryMode.ForwardOnly);
        await orchestrator.BeginTrackingAddressAsync(Address);

        var denied = await readiness.GetBlockingHistoryReadinessAsync([Address], [], false);
        Assert.NotNull(denied);
        Assert.Equal("history_not_ready", denied.Code);

        var allowed = await readiness.GetBlockingHistoryReadinessAsync([Address], [], true);
        Assert.Null(allowed);
    }

    [Fact]
    public async Task HistoricalAddressBackfillRunner_CompletesEmptyPageAndPromotesFullHistoryLive()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var registration = new Dxs.Consigliere.Data.Tracking.TrackedEntityRegistrationStore(store);
        var orchestrator = new TrackedEntityLifecycleOrchestrator(store);
        var scheduler = new TrackedHistoryBackfillScheduler(store, orchestrator);
        var readiness = new TrackedEntityReadinessService(store);

        await registration.RegisterAddressAsync(Address, "Genesis", TrackedEntityHistoryMode.FullHistory);
        await orchestrator.BeginTrackingAddressAsync(Address);
        await scheduler.QueueAddressFullHistoryAsync(Address);

        var bitails = new Mock<IBitailsRestApiClient>(MockBehavior.Strict);
        bitails.Setup(x => x.GetHistoryPageAsync(Address, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeserializeHistoryPage("{\"pgkey\":\"\",\"history\":[]}"));

        var txBus = new Mock<ITxMessageBus>(MockBehavior.Strict);
        var catalog = new Mock<IExternalChainProviderCatalog>(MockBehavior.Strict);
        catalog.Setup(x => x.GetDescriptors())
            .Returns([
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.Bitails,
                    [ExternalChainCapability.HistoricalAddressScan])
            ]);

        var runner = new HistoricalAddressBackfillRunner(
            store,
            bitails.Object,
            txBus.Object,
            new TestNetworkProvider(),
            Options.Create(new ConsigliereSourcesConfig
            {
                Providers = new SourceProvidersConfig
                {
                    Bitails = new BitailsSourceConfig
                    {
                        Enabled = true,
                        EnabledCapabilities = [ExternalChainCapability.HistoricalAddressScan]
                    }
                }
            }),
            Options.Create(new AppConfig()),
            catalog.Object,
            orchestrator,
            Mock.Of<ILogger<HistoricalAddressBackfillRunner>>());

        var processed = await runner.RunNextAsync();

        Assert.True(processed);
        var snapshot = await readiness.GetAddressReadinessAsync(Address);
        Assert.Equal(TrackedEntityHistoryReadiness.FullHistoryLive, snapshot.History.HistoryReadiness);
        Assert.True(snapshot.History.Coverage.FullCoverage);
        Assert.Equal(HistoryBackfillExecutionStatus.Completed, snapshot.History.BackfillStatus?.Status);
    }

    [Fact]
    public async Task HistoricalAddressBackfillRunner_TransitionsToWaitingRetryOnProviderFailure()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var registration = new Dxs.Consigliere.Data.Tracking.TrackedEntityRegistrationStore(store);
        var orchestrator = new TrackedEntityLifecycleOrchestrator(store);
        var scheduler = new TrackedHistoryBackfillScheduler(store, orchestrator);
        var readiness = new TrackedEntityReadinessService(store);

        await registration.RegisterAddressAsync(Address, "Genesis", TrackedEntityHistoryMode.FullHistory);
        await orchestrator.BeginTrackingAddressAsync(Address);
        await scheduler.QueueAddressFullHistoryAsync(Address);

        var bitails = new Mock<IBitailsRestApiClient>(MockBehavior.Strict);
        bitails.Setup(x => x.GetHistoryPageAsync(Address, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var catalog = new Mock<IExternalChainProviderCatalog>(MockBehavior.Strict);
        catalog.Setup(x => x.GetDescriptors())
            .Returns([
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.Bitails,
                    [ExternalChainCapability.HistoricalAddressScan])
            ]);

        var runner = new HistoricalAddressBackfillRunner(
            store,
            bitails.Object,
            Mock.Of<ITxMessageBus>(MockBehavior.Strict),
            new TestNetworkProvider(),
            Options.Create(new ConsigliereSourcesConfig
            {
                Providers = new SourceProvidersConfig
                {
                    Bitails = new BitailsSourceConfig
                    {
                        Enabled = true,
                        EnabledCapabilities = [ExternalChainCapability.HistoricalAddressScan]
                    }
                }
            }),
            Options.Create(new AppConfig()),
            catalog.Object,
            orchestrator,
            Mock.Of<ILogger<HistoricalAddressBackfillRunner>>());

        var processed = await runner.RunNextAsync();

        Assert.True(processed);
        var snapshot = await readiness.GetAddressReadinessAsync(Address);
        Assert.Equal(TrackedEntityHistoryReadiness.BackfillingFullHistory, snapshot.History.HistoryReadiness);
        Assert.Equal(HistoryBackfillExecutionStatus.WaitingRetry, snapshot.History.BackfillStatus?.Status);
        Assert.Equal("historical_address_scan_error", snapshot.History.BackfillStatus?.ErrorCode);
    }

    [Fact]
    public async Task HistoricalTokenBackfillRunner_TransitionsToWaitingRetryOnProviderFailure()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var registration = new Dxs.Consigliere.Data.Tracking.TrackedEntityRegistrationStore(store);
        var orchestrator = new TrackedEntityLifecycleOrchestrator(store);
        var scheduler = new TrackedHistoryBackfillScheduler(store, orchestrator);
        var readiness = new TrackedEntityReadinessService(store);

        await registration.RegisterTokenAsync(TokenId, "TEST", TrackedEntityHistoryMode.FullHistory, [TrustedRoot]);
        await orchestrator.BeginTrackingTokenAsync(TokenId);
        await scheduler.QueueTokenFullHistoryAsync(TokenId);

        var bitails = new Mock<IBitailsRestApiClient>(MockBehavior.Strict);
        bitails.Setup(x => x.GetTransactionDetails(TrustedRoot, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var catalog = new Mock<IExternalChainProviderCatalog>(MockBehavior.Strict);
        catalog.Setup(x => x.GetDescriptors())
            .Returns([
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.Bitails,
                    [ExternalChainCapability.HistoricalTokenScan])
            ]);

        var runner = new HistoricalTokenBackfillRunner(
            store,
            bitails.Object,
            Mock.Of<ITxMessageBus>(MockBehavior.Strict),
            new TestNetworkProvider(),
            Options.Create(new ConsigliereSourcesConfig
            {
                Providers = new SourceProvidersConfig
                {
                    Bitails = new BitailsSourceConfig
                    {
                        Enabled = true,
                        EnabledCapabilities = [ExternalChainCapability.HistoricalTokenScan]
                    }
                },
                Capabilities = new SourceCapabilitiesConfig
                {
                    HistoricalTokenScan = new RoutedCapabilityOverrideConfig
                    {
                        Source = ExternalChainProviderName.Bitails,
                        FallbackSources = [ExternalChainProviderName.Bitails]
                    }
                }
            }),
            Options.Create(new AppConfig()),
            catalog.Object,
            orchestrator,
            Mock.Of<ILogger<HistoricalTokenBackfillRunner>>());

        var processed = await runner.RunNextAsync();

        Assert.True(processed);
        var snapshot = await readiness.GetTokenReadinessAsync(TokenId);
        Assert.True(snapshot.Readable);
        Assert.Equal(TrackedEntityHistoryReadiness.BackfillingFullHistory, snapshot.History.HistoryReadiness);
        Assert.Equal(HistoryBackfillExecutionStatus.WaitingRetry, snapshot.History.BackfillStatus?.Status);
        Assert.Equal("historical_token_scan_error", snapshot.History.BackfillStatus?.ErrorCode);
    }

    [Fact]
    public async Task HistoricalTokenBackfillRunner_CompletesWhenTrustedRootAlreadyKnown()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var registration = new Dxs.Consigliere.Data.Tracking.TrackedEntityRegistrationStore(store);
        var orchestrator = new TrackedEntityLifecycleOrchestrator(store);
        var scheduler = new TrackedHistoryBackfillScheduler(store, orchestrator);
        var readiness = new TrackedEntityReadinessService(store);

        await registration.RegisterTokenAsync(TokenId, "TEST", TrackedEntityHistoryMode.FullHistory, [TrustedRoot]);
        await orchestrator.BeginTrackingTokenAsync(TokenId);
        await SeedTrustedRootTransactionAsync(store);
        await scheduler.QueueTokenFullHistoryAsync(TokenId);

        var bitails = new Mock<IBitailsRestApiClient>(MockBehavior.Strict);
        bitails.Setup(x => x.GetHistoryPageAsync("root-holder", null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeserializeHistoryPage("{\"pgkey\":\"\",\"history\":[]}"));

        var catalog = new Mock<IExternalChainProviderCatalog>(MockBehavior.Strict);
        catalog.Setup(x => x.GetDescriptors())
            .Returns([
                new ExternalChainProviderDescriptor(
                    ExternalChainProviderName.Bitails,
                    [ExternalChainCapability.HistoricalTokenScan])
            ]);

        var runner = new HistoricalTokenBackfillRunner(
            store,
            bitails.Object,
            Mock.Of<ITxMessageBus>(MockBehavior.Strict),
            new TestNetworkProvider(),
            Options.Create(new ConsigliereSourcesConfig
            {
                Providers = new SourceProvidersConfig
                {
                    Bitails = new BitailsSourceConfig
                    {
                        Enabled = true,
                        EnabledCapabilities = [ExternalChainCapability.HistoricalTokenScan]
                    }
                },
                Capabilities = new SourceCapabilitiesConfig
                {
                    HistoricalTokenScan = new RoutedCapabilityOverrideConfig
                    {
                        Source = ExternalChainProviderName.Bitails,
                        FallbackSources = [ExternalChainProviderName.Bitails]
                    }
                }
            }),
            Options.Create(new AppConfig()),
            catalog.Object,
            orchestrator,
            Mock.Of<ILogger<HistoricalTokenBackfillRunner>>());

        Assert.True(await runner.RunNextAsync());
        Assert.True(await runner.RunNextAsync());

        var snapshot = await readiness.GetTokenReadinessAsync(TokenId);
        Assert.Contains(
            snapshot.History.HistoryReadiness,
            (IEnumerable<string>)new[] { TrackedEntityHistoryReadiness.BackfillingFullHistory, TrackedEntityHistoryReadiness.FullHistoryLive });
        Assert.Contains(
            snapshot.History.BackfillStatus?.Status,
            (IEnumerable<string>)new[] { HistoryBackfillExecutionStatus.Running, HistoryBackfillExecutionStatus.Completed });
        Assert.NotNull(snapshot.History.RootedToken);
        Assert.True(snapshot.History.RootedToken.RootedHistorySecure);
        Assert.Equal(1, snapshot.History.RootedToken.CompletedTrustedRootCount);
        Assert.Empty(snapshot.History.RootedToken.UnknownRootFindings);
    }

    private static Dxs.Infrastructure.Bitails.Dto.HistoryPage DeserializeHistoryPage(string json)
        => JsonConvert.DeserializeObject<Dxs.Infrastructure.Bitails.Dto.HistoryPage>(json)!;

    private static async Task SeedTrustedRootTransactionAsync(Raven.Client.Documents.IDocumentStore store)
    {
        using var session = store.OpenAsyncSession();
        var root = new Dxs.Consigliere.Data.Models.Transactions.MetaTransaction
        {
            Id = TrustedRoot,
            Height = 777,
            Index = 0,
            Timestamp = DateTimeOffset.Parse("2026-03-26T18:10:00Z").ToUnixTimeMilliseconds(),
            IsStas = true,
            IsIssue = true,
            IsValidIssue = true,
            RedeemAddress = "issuer-rooted",
            Inputs = [],
            Outputs =
            [
                new Dxs.Consigliere.Data.Models.Transactions.MetaTransaction.Output
                {
                    Id = Dxs.Consigliere.Data.Models.Transactions.MetaOutput.GetId(TrustedRoot, 0),
                    Address = "root-holder",
                    TokenId = TokenId,
                    Satoshis = 1,
                    Type = Dxs.Bsv.Script.ScriptType.P2STAS
                }
            ],
            Addresses = ["root-holder", "issuer-rooted"],
            TokenIds = [TokenId],
            IllegalRoots = [],
            MissingTransactions = [],
            AllStasInputsKnown = true
        };

        await session.StoreAsync(root, root.Id);
        await session.SaveChangesAsync();
    }

    private sealed class TestNetworkProvider : INetworkProvider
    {
        public Dxs.Bsv.Network Network => Dxs.Bsv.Network.Mainnet;
    }

}
