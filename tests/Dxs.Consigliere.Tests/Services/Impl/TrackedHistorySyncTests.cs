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
        Assert.Equal("partial_history_opt_in_required", denied.Code);

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
    public async Task HistoricalTokenBackfillRunner_MarksHistoryDegradedWhileStateRemainsLive()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var registration = new Dxs.Consigliere.Data.Tracking.TrackedEntityRegistrationStore(store);
        var orchestrator = new TrackedEntityLifecycleOrchestrator(store);
        var scheduler = new TrackedHistoryBackfillScheduler(store, orchestrator);
        var readiness = new TrackedEntityReadinessService(store);

        await registration.RegisterTokenAsync(TokenId, "TEST", TrackedEntityHistoryMode.FullHistory);
        await orchestrator.BeginTrackingTokenAsync(TokenId);
        await scheduler.QueueTokenFullHistoryAsync(TokenId);

        var runner = new HistoricalTokenBackfillRunner(store, orchestrator);
        var processed = await runner.RunNextAsync();

        Assert.True(processed);
        var snapshot = await readiness.GetTokenReadinessAsync(TokenId);
        Assert.True(snapshot.Readable);
        Assert.Equal(TrackedEntityHistoryReadiness.Degraded, snapshot.History.HistoryReadiness);
        Assert.Equal(HistoryBackfillExecutionStatus.Failed, snapshot.History.BackfillStatus?.Status);
        Assert.Equal("historical_token_scan_not_implemented", snapshot.History.BackfillStatus?.ErrorCode);
    }

    private static Dxs.Infrastructure.Bitails.Dto.HistoryPage DeserializeHistoryPage(string json)
        => JsonConvert.DeserializeObject<Dxs.Infrastructure.Bitails.Dto.HistoryPage>(json)!;

    private sealed class TestNetworkProvider : INetworkProvider
    {
        public Dxs.Bsv.Network Network => Dxs.Bsv.Network.Mainnet;
    }
}
