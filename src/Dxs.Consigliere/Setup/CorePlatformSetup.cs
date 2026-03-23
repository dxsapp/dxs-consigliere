using Dxs.Bsv;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;

using MediatR;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Consigliere.Setup;

public static class CorePlatformSetup
{
    public static IServiceCollection AddCorePlatformZoneServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
        => services
            .Configure<AppConfig>(configuration)
            .Configure<NetworkConfig>(configuration)
            .AddSingleton<INetworkProvider, NetworkProvider>()
            .AddTransient<IBitcoindService, BitcoindService>()
            .AddTransient<IBroadcastProvider>(sp => sp.GetRequiredService<IBitcoindService>())
            .AddMediatR(cfg => { cfg.RegisterServicesFromAssemblyContaining<IMediator>(); });
}
