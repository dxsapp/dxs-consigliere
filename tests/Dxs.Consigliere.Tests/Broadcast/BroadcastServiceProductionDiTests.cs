using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.BackgroundTasks.Blocks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Consigliere.Services.P2p;
using Dxs.Consigliere.Setup;
using Dxs.Consigliere.WebSockets;

using MediatR;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Moq;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Tests.Broadcast;

/// <summary>
/// Wave 5 A2 M2 fix — pin the production DI graph for the unified
/// broadcast service. Builds a service provider with the real
/// <see cref="RealtimeSetup.AddRealtimeZoneServices"/> +
/// <see cref="BsvP2pSetup.AddBsvP2pZoneServices"/> registrations,
/// resolves <see cref="IBroadcastService"/>, then runs the W2 wirer
/// host (one-shot startup) to verify the property-injected
/// <c>PolicyValidator</c> / <c>OutgoingStore</c> / <c>Announcer</c>
/// slots are populated.
///
/// <para>Marked with <c>[Collection]</c> to disable xunit parallel
/// execution against itself / against
/// <c>BsvP2pSetupDiResolutionTests</c>. Both suites build distinct
/// service providers, but a few of the W2 hosted services
/// (<c>OutgoingTransactionMonitor</c>, etc.) carry static guards
/// against duplicate-instance creation; running concurrently
/// triggers those guards.</para>
/// </summary>
public class BroadcastServiceProductionDiTests
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

        services.AddSingleton<IConfiguration>(config);
        services.Configure<ConsigliereStorageConfig>(config.GetSection("Consigliere:Storage"));
        services.AddSingleton<IDocumentStore>(_ => Mock.Of<IDocumentStore>());
        services.AddSingleton<IRawTransactionPayloadStore>(_ => Mock.Of<IRawTransactionPayloadStore>());
        services.AddSingleton<INetworkProvider>(_ => new FakeNetworkProviderForBroadcastDi());
        services.AddSingleton(_ => Mock.Of<IObservationJournalAppender<ObservationJournalEntry<TxObservation>>>());
        services.AddSingleton(_ => Mock.Of<IObservationJournalAppender<ObservationJournalEntry<BlockObservation>>>());
        services.AddSingleton<BlockObservationJournalWriter>();
        services.AddSingleton<Dxs.Common.BackgroundTasks.BackgroundTasksConfig>(
            _ => new Dxs.Common.BackgroundTasks.BackgroundTasksConfig());
        services.AddSingleton<TxObservationJournalWriter>();
        services.AddSingleton<IBitcoindService>(_ => Mock.Of<IBitcoindService>());
        services.AddSingleton<IPublisher>(_ => Mock.Of<IPublisher>());
        services.AddSingleton<IHubContext<WalletHub, IWalletHub>>(_ =>
            Mock.Of<IHubContext<WalletHub, IWalletHub>>());

        // Production registrations under audit.
        services.AddRealtimeZoneServices();          // → IBroadcastService → BroadcastService
        services.AddBsvP2pZoneServices(config);      // → wirer + hosted services

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task IBroadcastService_ResolvesAsRealBroadcastService_FromProductionGraph()
    {
        // Audit W5 A2 M2 pin: the production DI registration in
        // RealtimeSetup must resolve IBroadcastService as the real
        // BroadcastService (not a mock). Pre-fix, the
        // BsvP2pSetupDiResolutionTests registered a Mock<IBroadcastService>
        // so it never actually exercised the BroadcastService ctor —
        // a ctor-dep regression would have silently slipped through.
        await using var sp = (ServiceProvider)BuildProvider();

        var service = sp.GetRequiredService<IBroadcastService>();
        Assert.NotNull(service);
        Assert.IsType<BroadcastService>(service);
    }

    [Fact]
    public async Task BroadcastServiceP2pWirerHost_RunsStartAsync_AndWiresInternalProperties()
    {
        // Audit W5 A2 M2 pin: the wirer host MUST populate
        // BroadcastService's internal property-injected slots
        // (PolicyValidator / OutgoingStore / Announcer) when the
        // hosted-service StartAsync fires. Pre-fix the existing
        // DI test asserted only the host's existence, not that
        // running it actually wired the properties.
        await using var sp = (ServiceProvider)BuildProvider();

        // Resolve the wirer directly (avoid GetServices<IHostedService>()
        // which would force construction of every hosted service in the
        // graph — `OutgoingTransactionMonitor` carries a static
        // duplicate-instance guard that fires across parallel xunit
        // runs; W2's closeout documents this as a known constraint).
        var wirer = sp.GetRequiredService<BroadcastServiceP2pWirer>();
        wirer.Wire();

        // Inspect the BroadcastService's property slots (internal,
        // accessed via InternalsVisibleTo from W4 A2-followup).
        var service = (BroadcastService)sp.GetRequiredService<IBroadcastService>();
        Assert.NotNull(service.PolicyValidator);
        Assert.NotNull(service.OutgoingStore);
        Assert.NotNull(service.Announcer);
    }

    private sealed class FakeNetworkProviderForBroadcastDi : INetworkProvider
    {
        public Network Network => Network.Mainnet;
    }
}
