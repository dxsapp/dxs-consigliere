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
            .Configure<NetworkConfig>(configuration)
            // wave-A3 S5: SecretsFileStore reads its on-disk
            // root from Consigliere:Secrets:Dir.
            .Configure<ConsigliereSecretsConfig>(configuration.GetSection("Consigliere:Secrets"));

        // `Dxs.Common.BackgroundTasks.PeriodicTask` (and every
        // hosted-service descendant — OutgoingTransactionMonitor,
        // UnconfirmedTransactionsMonitor, the JungleBus monitors,
        // etc.) takes the base `BackgroundTasksConfig` directly in
        // its constructor (NOT wrapped in IOptions). Expose it as a
        // singleton derived from the already-bound AppConfig so the
        // DI graph resolves under DockerComposeE2E + production.
        services.AddSingleton<Common.BackgroundTasks.BackgroundTasksConfig>(sp =>
            sp.GetRequiredService<IOptions<AppConfig>>().Value.BackgroundTasks
        );

        services.AddConsigliereAdminAuth(configuration);

        // wave-A3 S3: fail-stop audit logger. The retention
        // configurator is split out so its rethrow-on-failure
        // contract is unit-testable without standing up Raven's
        // sealed MaintenanceOperationExecutor (S3-audit M1 fix).
        services.AddSingleton<Services.Audit.IAuditRetentionConfigurator, Services.Audit.RavenAuditRetentionConfigurator>();
        services.AddSingleton<Services.Audit.IAuditLogger, Services.Audit.AuditLogger>();

        // wave-A3 S4: in-process log ring + MEL provider that
        // pushes every framework log emission through the
        // sanitizer + into the buffer. The hub registered in
        // SignalRSetup pulls from the same singleton.
        services.AddSingleton<Logging.LogStreamBuffer>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(sp =>
            new Logging.LogStreamProvider(sp.GetRequiredService<Logging.LogStreamBuffer>()));

        services
            .AddOptions<ConsigliereSourcesConfig>()
            .Configure(options =>
            {
                configuration.GetSection("Consigliere:Sources:Routing").Bind(options.Routing);
                configuration.GetSection("Consigliere:Sources:Capabilities").Bind(options.Capabilities);
            })
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
            // W5 S3: legacy IBroadcastProvider renamed to IFeeRateProvider
            // (broadcast moved to the unified P2P path in
            // IBroadcastService.BroadcastAsync). The DI forwarder still
            // points the fee-rate slot at BitcoindService (the only
            // current IFeeRateProvider) — STAS tx factories consume it.
            .AddTransient<IFeeRateProvider>(sp => sp.GetRequiredService<IBitcoindService>())
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
