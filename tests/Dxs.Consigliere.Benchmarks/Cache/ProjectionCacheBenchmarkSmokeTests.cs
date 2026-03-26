namespace Dxs.Consigliere.Benchmarks.Cache;

public class ProjectionCacheBenchmarkSmokeTests
{
    [Theory]
    [InlineData("memory")]
    [InlineData("azos")]
    public async Task MeasureAsync_ProducesPositiveMetrics(string backend)
    {
        var harness = new ProjectionCacheBenchmarkHarness();
        var scenario = new ProjectionCacheBenchmarkScenario(
            $"cache-{backend}",
            backend,
            AddressCount: 24,
            HistoryTransactionCount: 48,
            UtxoCountPerAddress: 3,
            QueryIterations: 120,
            Take: 32);

        var metrics = await harness.MeasureAsync(scenario);

        Assert.Equal(backend, metrics.Backend);
        Assert.True(metrics.HistoryQueriesPerSecond > 0);
        Assert.True(metrics.BalanceQueriesPerSecond > 0);
        Assert.True(metrics.UtxoQueriesPerSecond > 0);
        Assert.True(metrics.TokenHistoryQueriesPerSecond > 0);
        Assert.True(metrics.InvalidationCyclesPerSecond > 0);
        Assert.True(metrics.CacheEntryCount > 0);
    }
}
