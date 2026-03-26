namespace Dxs.Consigliere.Benchmarks.Realtime;

public class RealtimeFanoutBenchmarkSmokeTests
{
    [Fact]
    public async Task MeasuresSeenAndConfirmedFanoutPaths()
    {
        var harness = new RealtimeFanoutBenchmarkHarness();
        var scenario = new RealtimeFanoutBenchmarkScenario(
            "realtime-fanout-baseline",
            AddressFanout: 3,
            TokenFanout: 2,
            TransactionCount: 12);

        var seen = await harness.MeasureSeenFanoutAsync(scenario);
        var confirmed = await harness.MeasureConfirmedFanoutAsync(scenario);

        Assert.Equal(12, seen.Operations);
        Assert.Equal(12, confirmed.Operations);
        Assert.True(seen.PublishedEvents > 0);
        Assert.True(confirmed.PublishedEvents > 0);
        Assert.True(seen.ThroughputPerSecond > 0);
        Assert.True(confirmed.ThroughputPerSecond > 0);
    }
}
