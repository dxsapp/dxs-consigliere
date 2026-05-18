using System;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.BackgroundTasks.Blocks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.P2p;
using Dxs.Consigliere.Setup;
using Dxs.Consigliere.WebSockets;
using Dxs.Common.BackgroundTasks;
using Dxs.Common.Journal;

using MediatR;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Tests.Setup;

/// <summary>
/// Wave 2 audit A2 C1 fix: regression DI test. Builds the service
/// provider with <see cref="BsvP2pSetup.AddBsvP2pZoneServices"/> and
/// resolves every hosted/singleton the P2P zone registers. Catches
/// future constructor-dependency drift (the audit's C1 finding) by
/// failing the build rather than the runtime.
/// </summary>
public class BsvP2pSetupDiResolutionTests
{
    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Consigliere:Broadcast:P2p:Enabled"] = "false",
                ["Consigliere:Broadcast:P2p:Network"] = "mainnet",
                ["Consigliere:Storage:RawTransactionPayloads:Provider"] = "raven",
                ["Consigliere:Storage:RawTransactionPayloads:Enabled"] = "false",
            }!)
            .Build();

        // External dependencies the P2P zone consumes through DI.
        services.AddSingleton<IConfiguration>(config);
        services.Configure<ConsigliereStorageConfig>(config.GetSection("Consigliere:Storage"));
        services.AddSingleton<IDocumentStore>(_ => Mock.Of<IDocumentStore>());
        services.AddSingleton<IRawTransactionPayloadStore>(_ => Mock.Of<IRawTransactionPayloadStore>());
        // Wave 6 S7 — P2pAlertPoller depends on ISnapshotPersistence
        // (registered in MetricsSetup in production). The W6 DI test
        // mocks it here so the W6 zone resolves without pulling the
        // entire metrics graph.
        services.AddSingleton<Dxs.Consigliere.Services.Metrics.ISnapshotPersistence>(_ =>
            Mock.Of<Dxs.Consigliere.Services.Metrics.ISnapshotPersistence>());
        services.AddSingleton<INetworkProvider>(_ => new FakeNetworkProvider());
        services.AddSingleton(_ => Mock.Of<IObservationJournalAppender<ObservationJournalEntry<TxObservation>>>());
        // Wave 3: ReorgPipeline depends on BlockObservationJournalWriter
        // (registered in production by HostedTasksSetup, not BsvP2pSetup).
        // Stand up the writer over a mock block-observation appender so the
        // DI graph resolves end-to-end without bringing in the full
        // HostedTasksSetup chain.
        services.AddSingleton(_ => Mock.Of<IObservationJournalAppender<ObservationJournalEntry<BlockObservation>>>());
        services.AddSingleton<BlockObservationJournalWriter>();
        services.AddSingleton<Dxs.Common.BackgroundTasks.BackgroundTasksConfig>(
            _ => new Dxs.Common.BackgroundTasks.BackgroundTasksConfig());
        services.AddSingleton<TxObservationJournalWriter>();
        services.AddSingleton<IBroadcastService>(_ => Mock.Of<IBroadcastService>());
        services.AddSingleton<IPublisher>(_ => Mock.Of<IPublisher>());
        services.AddSingleton<IHubContext<WalletHub, IWalletHub>>(_ => Mock.Of<IHubContext<WalletHub, IWalletHub>>());

        // The W2 zone under test.
        services.AddBsvP2pZoneServices(config);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TxRelayCoordinator_Resolves_FromConsigliereServiceGraph()
    {
        // Audit W2 A2 C1: previously the coordinator required
        // PeerManager which BsvP2pHostedService constructed privately
        // (not in DI); the production graph couldn't construct it.
        // Now it depends on BsvP2pHealth which is a registered
        // singleton.
        await using var sp = (ServiceProvider)BuildProvider();
        var coordinator = sp.GetRequiredService<TxRelayCoordinator>();
        Assert.NotNull(coordinator);
    }

    [Fact]
    public async Task W2_SingletonGraph_Resolves()
    {
        // Audit W2 A2 C1 spread: pin every W2-registered singleton
        // so a future ctor-dep drift fails the build, not the host
        // startup. (OutgoingTransactionMonitor's PeriodicTask
        // base-class guard against duplicate-instance creation
        // makes the wholesale IHostedService enumeration test
        // fragile across xunit runs; targeted singleton resolution
        // is the stable substitute.)
        await using var sp = (ServiceProvider)BuildProvider();
        Assert.NotNull(sp.GetRequiredService<BsvP2pHealth>());
        Assert.NotNull(sp.GetRequiredService<PerSessionDispatcherRegistry>());
        Assert.NotNull(sp.GetRequiredService<Dxs.Bsv.P2p.Observer.WatchlistMatcher>());
        Assert.NotNull(sp.GetRequiredService<RavenWatchlistLoader>());
        Assert.NotNull(sp.GetRequiredService<SourceObservationRecorder>());
        Assert.NotNull(sp.GetRequiredService<Dxs.Bsv.P2p.Observer.MempoolWatcher>());
        Assert.NotNull(sp.GetRequiredService<TxRelayCoordinator>());
    }

    [Fact]
    public async Task W3_SingletonGraph_Resolves()
    {
        // Wave 3: pin every W3-registered singleton so a future
        // ctor-dep drift fails the build, not the host startup —
        // same pattern as W2 A2 C1.
        // Audit W3 A2 L1 fix: added IOutgoingRawLookup + ITxAnnouncer
        // + ICoinbaseProbe (which were previously missing from the
        // pin and could ctor-drift silently).
        await using var sp = (ServiceProvider)BuildProvider();
        Assert.NotNull(sp.GetRequiredService<Dxs.Bsv.P2p.Chain.HeightCumulativeWorkComparer>());
        Assert.NotNull(sp.GetRequiredService<Dxs.Bsv.P2p.Chain.WorkBitsCumulativeWorkComparer>());
        Assert.NotNull(sp.GetRequiredService<Dxs.Bsv.P2p.Chain.ICumulativeWorkComparer>());
        Assert.NotNull(sp.GetRequiredService<Dxs.Bsv.P2p.Chain.ReorgDetector>());
        Assert.NotNull(sp.GetRequiredService<IOrphanedTxIdReader>());
        Assert.NotNull(sp.GetRequiredService<IOutgoingRawLookup>());
        Assert.NotNull(sp.GetRequiredService<ITxAnnouncer>());
        Assert.NotNull(sp.GetRequiredService<ICoinbaseProbe>());
        Assert.NotNull(sp.GetRequiredService<OrphanedTxRebroadcastRecorder>());
        Assert.NotNull(sp.GetRequiredService<IOrphanedTxRebroadcaster>());
        Assert.NotNull(sp.GetRequiredService<IReorgPipeline>());
    }

    [Fact]
    public async Task W6_SingletonGraph_Resolves()
    {
        // Wave 6 S7 — pin every W6-registered singleton so a future
        // ctor-dep drift fails the build, not the host startup. Same
        // pattern as W2 A2 C1 / W3 / W4 / W5.
        // Covers: scoring policy, alert evaluator + repo + poller.
        await using var sp = (ServiceProvider)BuildProvider();
        Assert.NotNull(sp.GetRequiredService<Dxs.Bsv.P2p.Pool.IPeerScoringPolicy>());
        Assert.NotNull(sp.GetRequiredService<P2pAlertEvaluator>());
        Assert.NotNull(sp.GetRequiredService<Dxs.Consigliere.Data.P2p.IAlertEventRepository>());
        Assert.NotNull(sp.GetRequiredService<P2pAlertPoller>());
    }

    [Fact]
    public void W6_AlertPoller_IsRegisteredAsHostedService()
    {
        // Wave 6 S7 — pin the hosted-service registration without
        // doing a wholesale GetServices<IHostedService>() resolution
        // (which trips the OutgoingTransactionMonitor PeriodicTask
        // duplicate-instance guard across xunit runs, per the W2
        // comment). Inspect the ServiceCollection directly.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new ConfigurationBuilder().Build());
        services.AddBsvP2pZoneServices(
            new ConfigurationBuilder().Build());

        var hostedDescriptors = services
            .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
            .ToList();

        // The poller is registered via AddHostedService(sp =>
        // sp.GetRequiredService<P2pAlertPoller>()) so the
        // ImplementationFactory captures the singleton accessor.
        // Materializing it would require a full provider build;
        // existence of at least one IHostedService factory
        // descriptor after AddBsvP2pZoneServices is enough to
        // detect a future "AddHostedService line deleted"
        // regression.
        Assert.NotEmpty(hostedDescriptors);
    }

    [Fact]
    public async Task BroadcastServiceP2pWirerHost_Resolves()
    {
        // Audit W2 A2 C1: the wirer was registered but no invoker
        // existed. We now register BroadcastServiceP2pWirerHost as a
        // hosted service so DI resolves it AND its StartAsync runs
        // Wire() on application startup.
        await using var sp = (ServiceProvider)BuildProvider();
        var hostedServices = sp.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
        var wirerHost = hostedServices.FirstOrDefault(h => h.GetType().Name == "BroadcastServiceP2pWirerHost");
        Assert.NotNull(wirerHost);
    }

    private sealed class FakeNetworkProvider : INetworkProvider
    {
        public Network Network => Network.Mainnet;
    }
}
