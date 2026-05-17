using System.Threading.Tasks;

using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Services.Metrics;
using Dxs.Consigliere.Services.P2p;
using Dxs.Consigliere.Setup;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Tests.Metrics;

/// <summary>
/// Wave 4 S7 — pins every W4 singleton against a mocked DI graph.
/// Ctor-dep drift fails the build, not the host startup (W2 A2 C1
/// + W3 W3_SingletonGraph_Resolves pattern).
/// </summary>
public class MetricsSetupDiResolutionTests
{
    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Consigliere:Metrics:Sources:SnapshotIntervalMs"] = "30000",
                ["Consigliere:Metrics:Sources:SnapshotRetentionCount"] = "720",
                ["Consigliere:Metrics:Sources:EvictionWindowMs"] = "300000",
                ["Consigliere:Metrics:Sources:Enabled"] = "false",
            }!)
            .Build();
        services.AddSingleton<IConfiguration>(config);

        // External dependencies the metrics zone consumes.
        services.AddSingleton<SourceObservationRecorder>();
        services.AddSingleton<OrphanedTxRebroadcastRecorder>();
        services.AddSingleton<BsvP2pHealth>();
        services.AddSingleton<IDocumentStore>(_ => Mock.Of<IDocumentStore>());

        services.AddMetricsZoneServices(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task W4_SingletonGraph_Resolves()
    {
        // Audit W4 S7 pin: every W4 singleton must be resolvable
        // from the production DI graph. Failure here is a build-time
        // signal of ctor-dep drift before host startup.
        await using var sp = (ServiceProvider)BuildProvider();
        Assert.NotNull(sp.GetRequiredService<SourceVisibilityTracker>());
        Assert.NotNull(sp.GetRequiredService<ISourceMetricsCollector>());
        Assert.NotNull(sp.GetRequiredService<SourceMetricsAggregator>());
        Assert.NotNull(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SourceMetricsConfig>>());
    }

    [Fact]
    public async Task SourceMetricsAggregator_IsRegisteredAsHostedService()
    {
        await using var sp = (ServiceProvider)BuildProvider();
        var hostedServices = sp.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
        Assert.Contains(hostedServices,
            h => h is SourceMetricsAggregator);
    }

    [Fact]
    public async Task SourceVisibilityTracker_HonoursConfiguredEvictionWindow()
    {
        // Wire the tracker via DI and verify the EvictionWindowMs
        // option flows through SourceMetricsConfig → tracker options.
        // We don't have a public getter for the inner option, so
        // this test asserts via behaviour: an entry younger than the
        // configured window survives eviction.
        await using var sp = (ServiceProvider)BuildProvider();
        var tracker = sp.GetRequiredService<SourceVisibilityTracker>();
        tracker.RecordObservation("tx1", "p2p",
            DateTimeOffset.UtcNow);
        tracker.EvictStaleEntries(DateTimeOffset.UtcNow.AddSeconds(10));
        Assert.Equal(1, tracker.InflightCount);
    }
}
