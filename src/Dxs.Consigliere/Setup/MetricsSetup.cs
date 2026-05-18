using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Services.Metrics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Setup;

/// <summary>
/// Wave 4 S7 — DI wiring for the source-metrics aggregator.
/// Registered from <c>Startup.ConfigureServices</c> after the W2 +
/// W3 zones (the collector consumes <c>SourceObservationRecorder</c>,
/// <c>OrphanedTxRebroadcastRecorder</c>, <c>BsvP2pHealth</c>).
/// </summary>
public static class MetricsSetup
{
    public static IServiceCollection AddMetricsZoneServices(
        this IServiceCollection services,
        IConfiguration configuration)
        => services
            .Configure<SourceMetricsConfig>(configuration.GetSection("Consigliere:Metrics:Sources"))
            .AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<SourceMetricsConfig>>().Value;
                return new SourceVisibilityTracker(new SourceVisibilityTrackerOptions
                {
                    EvictionWindowMs = opts.EvictionWindowMs,
                });
            })
            .AddSingleton<ISourceMetricsCollector, SourceMetricsCollector>()
            .AddSingleton<ISnapshotPersistence, RavenSnapshotPersistence>()
            .AddSingleton<SourceMetricsAggregator>()
            .AddHostedService(sp => sp.GetRequiredService<SourceMetricsAggregator>());
}
