using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Observer;
using Dxs.Bsv.P2p.Pool;
using Dxs.Consigliere.BackgroundTasks.P2p;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.Impl;
using Dxs.Consigliere.Services.P2p;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Setup;

public static class BsvP2pSetup
{
    public static IServiceCollection AddBsvP2pZoneServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
        => services
            .Configure<BsvP2pConfig>(configuration.GetSection("Consigliere:Broadcast:P2p"))
            // Gate 2 — peer pool
            .AddSingleton<BsvP2pHealth>()
            .AddHostedService<BsvP2pHostedService>()
            // thin-node-primary-source S1 — expose the thin node as a
            // routable provider (`p2p`) in the external-chain catalog.
            .AddSingleton<Dxs.Infrastructure.Common.IExternalChainProviderDiagnostics, P2pProviderDiagnostics>()
            // thin-node-primary-source S2 — on-demand getdata(MSG_TX)
            // rawTx fetch consumed by RawTransactionFetchService when
            // `p2p` is the resolved rawTx primary. Singleton: stateless,
            // shares the singleton health + dispatcher registry.
            .AddSingleton<IP2pRawTransactionClient, P2pRawTransactionClient>()
            // Gate 3 — tx lifecycle
            .AddSingleton<Dxs.Consigliere.Data.P2p.IBroadcastStateNotifier,
                Dxs.Consigliere.WebSockets.HubBroadcastStateNotifier>()
            .AddSingleton<OutgoingTransactionStore>()
            .AddSingleton<TxPolicyValidator>()
            .AddSingleton<TxRelayCoordinator>()
            .AddSingleton<OutgoingTransactionMonitor>()
            .AddHostedService(sp => sp.GetRequiredService<OutgoingTransactionMonitor>())
            // Wave 1 — headers chain
            .Configure<HeadersChainOptions>(configuration.GetSection("Consigliere:Broadcast:P2p:Headers"))
            .AddSingleton<HeadersChain>(sp =>
                new HeadersChain(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HeadersChainOptions>>().Value))
            .AddSingleton<BlockHeaderStore>()
            .AddSingleton<IBlockHeaderStore>(sp => sp.GetRequiredService<BlockHeaderStore>())
            .AddSingleton<INewBlockNotifier, HubNewBlockNotifier>()
            .AddSingleton<IHeadersBootstrapSource, WhatsOnChainHeadersBootstrapSource>()
            .AddSingleton<HeadersChainBootstrapper>()
            .AddHostedService<HeadersChainService>()
            // Wave 2 — mempool observer (S2-S5)
            .Configure<MempoolWatcherOptions>(configuration.GetSection("Consigliere:Broadcast:P2p:Mempool"))
            .AddSingleton<PerSessionDispatcherRegistry>()
            .AddSingleton<WatchlistMatcher>()
            .AddSingleton<RavenWatchlistLoader>()
            .AddSingleton<SourceObservationRecorder>()
            .AddSingleton<MempoolWatcher>(sp =>
                new MempoolWatcher(sp.GetRequiredService<IOptions<MempoolWatcherOptions>>().Value))
            .AddHostedService<P2pMempoolIngestRunner>()
            // Wire P2P properties into BroadcastService after construction.
            .AddSingleton<BroadcastServiceP2pWirer>()
            // Audit W2 A2 C1: a registered wirer is inert without a
            // host-side invoker. Schedule it via a one-shot hosted
            // service that runs Wire() right after DI is built.
            .AddHostedService<BroadcastServiceP2pWirerHost>()
            // Wave 3 — reorg handling.
            // Audit W3 A2 C1 fix: production uses the bits-based work
            // comparer so a malicious peer minting low-difficulty
            // headers can't win a reorg by height alone. The
            // height-only comparer is kept registered for tests that
            // pin synthetic chains at uniform regtest difficulty.
            .AddSingleton<HeightCumulativeWorkComparer>()
            .AddSingleton<WorkBitsCumulativeWorkComparer>()
            .AddSingleton<ICumulativeWorkComparer>(sp =>
                sp.GetRequiredService<WorkBitsCumulativeWorkComparer>())
            .AddSingleton<ReorgDetector>(sp =>
                new ReorgDetector(sp.GetRequiredService<ICumulativeWorkComparer>()))
            .AddSingleton<IOrphanedTxIdReader, RavenOrphanedTxIdReader>()
            .AddSingleton<OrphanedTxRebroadcastRecorder>()
            .AddSingleton<IOutgoingRawLookup, OutgoingTransactionStoreRawLookup>()
            .AddSingleton<ITxAnnouncer>(sp => sp.GetRequiredService<TxRelayCoordinator>())
            // W3 A2 H2 fix: explicit coinbase skip in the rebroadcaster.
            .AddSingleton<ICoinbaseProbe, MetaTransactionCoinbaseProbe>()
            .AddSingleton<IOrphanedTxRebroadcaster, OrphanedTxRebroadcaster>()
            // IProjectionRebuilder is registered in IndexerStateSetup
            // next to the TxLifecycleProjectionRebuilder itself (W3 A2
            // H1). ReorgPipeline takes it as an optional dependency so
            // narrow DI tests without the full state-setup graph still
            // resolve.
            .AddSingleton<IReorgPipeline, ReorgPipeline>()
            // Wave 6 S7 — scoring policy + rotation are pure-logic
            // singletons consumed by the PeerManager wirer in
            // BsvP2pHostedService (not by direct DI resolution; the
            // manager is constructed manually with the policy).
            .AddSingleton<IPeerScoringPolicy, DefaultPeerScoringPolicy>()
            // Wave 6 S2 — alert pipeline. Evaluator carries per-tick
            // delta state so it MUST be singleton.
            .AddSingleton<P2pAlertEvaluator>()
            .AddSingleton<IAlertEventRepository, RavenAlertEventRepository>()
            .AddSingleton<P2pAlertPoller>()
            .AddHostedService(sp => sp.GetRequiredService<P2pAlertPoller>());

    // Called from BsvP2pHostedService after PeerManager starts, so
    // BroadcastService can find the relay coordinator.
    internal static void ConfigureBroadcastServiceP2p(
        BroadcastService broadcastService,
        TxPolicyValidator validator,
        OutgoingTransactionStore store,
        TxRelayCoordinator relay)
    {
        // A2 M1 fix: BroadcastService now takes IOutgoingTransactionRepository
        // + ITxAnnouncer (interfaces). OutgoingTransactionStore implements
        // IOutgoingTransactionRepository; TxRelayCoordinator implements
        // ITxAnnouncer (the latter shipped by W3 A2).
        broadcastService.PolicyValidator = validator;
        broadcastService.OutgoingStore = store;
        broadcastService.Announcer = relay;
    }
}

/// <summary>
/// Singleton that wires Gate-3 dependencies into BroadcastService
/// after DI is built (avoids circular dependency).
/// </summary>
public sealed class BroadcastServiceP2pWirer(
    Services.IBroadcastService broadcastService,
    TxPolicyValidator validator,
    OutgoingTransactionStore store,
    TxRelayCoordinator relay)
{
    public void Wire()
    {
        if (broadcastService is BroadcastService bs)
            BsvP2pSetup.ConfigureBroadcastServiceP2p(bs, validator, store, relay);
    }
}

/// <summary>
/// Audit W2 A2 C1 fix: hosts the <see cref="BroadcastServiceP2pWirer"/>
/// as a one-shot startup invoker so the registered wirer actually
/// runs.
/// </summary>
internal sealed class BroadcastServiceP2pWirerHost(BroadcastServiceP2pWirer wirer)
    : Microsoft.Extensions.Hosting.IHostedService
{
    public System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken)
    {
        wirer.Wire();
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken)
        => System.Threading.Tasks.Task.CompletedTask;
}
