using Dxs.Consigliere.BackgroundTasks.P2p;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.Impl;
using Dxs.Consigliere.Services.P2p;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            // Gate 3 — tx lifecycle
            .AddSingleton<OutgoingTransactionStore>()
            .AddSingleton<TxPolicyValidator>()
            .AddSingleton<TxRelayCoordinator>()
            .AddSingleton<OutgoingTransactionMonitor>()
            .AddHostedService(sp => sp.GetRequiredService<OutgoingTransactionMonitor>())
            // Wire P2P properties into BroadcastService after construction.
            .AddSingleton<BroadcastServiceP2pWirer>();

    // Called from BsvP2pHostedService after PeerManager starts, so
    // BroadcastService can find the relay coordinator.
    internal static void ConfigureBroadcastServiceP2p(
        BroadcastService broadcastService,
        TxPolicyValidator validator,
        OutgoingTransactionStore store,
        TxRelayCoordinator relay)
    {
        broadcastService.PolicyValidator = validator;
        broadcastService.OutgoingStore = store;
        broadcastService.RelayCoordinator = relay;
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
