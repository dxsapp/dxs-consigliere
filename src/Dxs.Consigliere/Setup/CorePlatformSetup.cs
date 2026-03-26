using Dxs.Bsv;
using Dxs.Common.Cache;
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
            .AddOptions<ConsigliereCacheConfig>()
            .Bind(configuration.GetSection("Consigliere:Cache"))
            .ValidateOnStart();

        services
            .AddSingleton<IValidateOptions<ConsigliereSourcesConfig>, ConsigliereSourcesConfigValidation>()
            .AddSingleton<IValidateOptions<ConsigliereStorageConfig>, ConsigliereStorageConfigValidation>()
            .AddSingleton<IValidateOptions<ConsigliereCacheConfig>, ConsigliereCacheConfigValidation>()
            .AddHostedService<VNextStartupDiagnosticsHostedService>()
            .AddSingleton<INetworkProvider, NetworkProvider>()
            .AddTransient<IBitcoindService, BitcoindService>()
            .AddTransient<IBroadcastProvider>(sp => sp.GetRequiredService<IBitcoindService>())
            .AddMediatR(cfg => { cfg.RegisterServicesFromAssemblyContaining<IMediator>(); });

        var cacheConfig = configuration.GetSection("Consigliere:Cache").Get<ConsigliereCacheConfig>() ?? new ConsigliereCacheConfig();
        services.AddProjectionReadCache(
            options =>
            {
                options.MaxEntries = cacheConfig.MaxEntries;
                options.DefaultSafetyTtl = cacheConfig.SafetyTtlSeconds is > 0
                    ? TimeSpan.FromSeconds(cacheConfig.SafetyTtlSeconds.Value)
                    : null;
            },
            cacheConfig.Enabled && string.Equals(cacheConfig.Backend, "memory", StringComparison.OrdinalIgnoreCase));

        return services;
    }
}
