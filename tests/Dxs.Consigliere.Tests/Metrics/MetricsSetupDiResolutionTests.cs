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
    /// <summary>A2 L1 fix: a distinct, non-default eviction window so
    /// the config flow-through assertion is meaningful (not a tautology
    /// against the production default).</summary>
    private const long TestEvictionWindowMs = 1_000L;

    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Consigliere:Metrics:Sources:SnapshotIntervalMs"] = "30000",
                ["Consigliere:Metrics:Sources:SnapshotRetentionCount"] = "720",
                // L1 fix: 1 s window (vs production default 5 min). The
                // flow-through test below sleeps 1.5 s and asserts the
                // entry is evicted — only true if the config-driven
                // window is the small one. Production default would
                // leave the entry intact.
                ["Consigliere:Metrics:Sources:EvictionWindowMs"] = TestEvictionWindowMs.ToString(),
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
    public async Task SourceVisibilityTracker_HonoursConfiguredEvictionWindow_DistinctFromDefault()
    {
        // A2 L1 fix: the previous version of this test used a 5-min
        // eviction window (production default) and asserted survival
        // 10 s in — passes regardless of whether config flows through.
        // The corrected test uses a 1 s configured window and:
        //   - asserts SURVIVAL at 500 ms (well below the window);
        //   - asserts EVICTION at 1500 ms (past the window).
        // Both assertions fail if the production default leaked into
        // the tracker.
        await using var sp = (ServiceProvider)BuildProvider();
        var tracker = sp.GetRequiredService<SourceVisibilityTracker>();
        var basis = DateTimeOffset.FromUnixTimeMilliseconds(1_000);
        tracker.RecordObservation("tx1", "p2p", basis);

        tracker.EvictStaleEntries(basis.AddMilliseconds(500));
        Assert.Equal(1, tracker.InflightCount);

        tracker.EvictStaleEntries(basis.AddMilliseconds(TestEvictionWindowMs + 500));
        Assert.Equal(0, tracker.InflightCount);
    }
}
