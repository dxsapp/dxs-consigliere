namespace Dxs.Consigliere.Benchmarks.FullSystem;

public class VNextFullSystemBenchmarkSmokeTests
{
    [Fact]
    public async Task MeasuresReplayQueryAndSoakPaths()
    {
        var harness = new VNextFullSystemBenchmarkHarness();
        var scenario = new VNextFullSystemBenchmarkScenario("vnext-full-system-baseline", TransferCount: 2, QueryCount: 2, SoakCycles: 2);

        var replay = await harness.MeasureReplayAsync(scenario);
        var query = await harness.MeasureQueryBundleAsync(scenario);
        var soak = await harness.MeasureSoakAsync(scenario);

        Assert.Equal(4, replay.Operations);
        Assert.Equal(14, query.Operations);
        Assert.Equal(6, soak.Operations);
        Assert.True(replay.ThroughputPerSecond > 0);
        Assert.True(query.ThroughputPerSecond > 0);
        Assert.True(soak.ThroughputPerSecond > 0);
    }
}
