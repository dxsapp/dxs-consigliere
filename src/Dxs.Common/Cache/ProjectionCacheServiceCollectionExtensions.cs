using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Common.Cache;

public static class ProjectionCacheServiceCollectionExtensions
{
    public static IServiceCollection AddProjectionReadCache(
        this IServiceCollection services,
        Action<InProcessProjectionReadCacheOptions> configure = null,
        bool enabled = true)
    {
        if (!enabled)
        {
            services
                .AddSingleton<NoopProjectionReadCache>()
                .AddSingleton<IProjectionReadCache>(sp => sp.GetRequiredService<NoopProjectionReadCache>())
                .AddSingleton<IProjectionCacheInvalidationSink>(sp => sp.GetRequiredService<NoopProjectionReadCache>())
                .AddSingleton<IProjectionReadCacheTelemetry>(sp => sp.GetRequiredService<NoopProjectionReadCache>());

            return services;
        }

        var inProcessOptions = new InProcessProjectionReadCacheOptions();
        configure?.Invoke(inProcessOptions);

        services
            .AddSingleton(inProcessOptions)
            .AddSingleton<InProcessProjectionReadCache>()
            .AddSingleton<IProjectionReadCache>(sp => sp.GetRequiredService<InProcessProjectionReadCache>())
            .AddSingleton<IProjectionCacheInvalidationSink>(sp => sp.GetRequiredService<InProcessProjectionReadCache>())
            .AddSingleton<IProjectionReadCacheTelemetry>(sp => sp.GetRequiredService<InProcessProjectionReadCache>());

        return services;
    }
}
