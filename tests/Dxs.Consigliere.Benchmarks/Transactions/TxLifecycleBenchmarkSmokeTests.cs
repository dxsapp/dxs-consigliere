namespace Dxs.Consigliere.Benchmarks.Transactions;

public class TxLifecycleBenchmarkSmokeTests
{
    [Fact]
    public async Task MeasuresRebuildAndQueryPaths()
    {
        var harness = new TxLifecycleBenchmarkHarness();
        var scenario = new TxLifecycleBenchmarkScenario("tx-lifecycle-baseline", TransactionCount: 64);

        var rebuild = await harness.MeasureRebuildAsync(scenario);
        var query = await harness.MeasureQueryAsync(scenario);

        Assert.Equal(128, rebuild.Observations);
        Assert.Equal(64, query.Queries);
        Assert.True(rebuild.ThroughputPerSecond > 0);
        Assert.True(query.ThroughputPerSecond > 0);
    }
}
