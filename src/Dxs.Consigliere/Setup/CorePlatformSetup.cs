using Dxs.Bsv;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;

using MediatR;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Setup;

public static class CorePlatformSetup
{
    public static IServiceCollection AddCorePlatformZoneServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .Configure<AppConfig>(configuration)
            .Configure<NetworkConfig>(configuration);

        services
            .AddOptions<ConsigliereSourcesConfig>()
            .Bind(configuration.GetSection("Consigliere:Sources"))
            .ValidateOnStart();

        services
            .AddOptions<ConsigliereStorageConfig>()
            .Bind(configuration.GetSection("Consigliere:Storage"))
            .ValidateOnStart();

        services
            .AddSingleton<IValidateOptions<ConsigliereSourcesConfig>, ConsigliereSourcesConfigValidation>()
            .AddSingleton<IValidateOptions<ConsigliereStorageConfig>, ConsigliereStorageConfigValidation>()
            .AddHostedService<VNextStartupDiagnosticsHostedService>()
            .AddSingleton<INetworkProvider, NetworkProvider>()
            .AddTransient<IBitcoindService, BitcoindService>()
            .AddTransient<IBroadcastProvider>(sp => sp.GetRequiredService<IBitcoindService>())
            .AddMediatR(cfg => { cfg.RegisterServicesFromAssemblyContaining<IMediator>(); });

        return services;
    }
}
