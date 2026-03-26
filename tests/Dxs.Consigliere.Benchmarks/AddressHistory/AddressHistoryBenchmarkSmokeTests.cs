namespace Dxs.Consigliere.Benchmarks.AddressHistory;

public class AddressHistoryBenchmarkSmokeTests
{
    [Fact]
    public async Task MeasuresAddressHistoryRebuildAndQueryPaths()
    {
        var harness = new AddressHistoryBenchmarkHarness();
        var scenario = new AddressHistoryBenchmarkScenario(
            "address-history-baseline",
            TransferCount: 24,
            QueryCount: 8,
            Take: 32);

        var rebuild = await harness.MeasureRebuildAsync(scenario);
        var query = await harness.MeasureQueryAsync(scenario);

        Assert.Equal(48, rebuild.ProjectedTransactions);
        Assert.Equal(8, query.Queries);
        Assert.True(query.RowsReturned > 0);
        Assert.True(rebuild.ThroughputPerSecond > 0);
        Assert.True(query.ThroughputPerSecond > 0);
    }
}
