namespace Dxs.Consigliere.Benchmarks.Replay;

public class ReplayHarnessSmokeTests
{
    [Fact]
    public void LoadsScenarioAndProducesMetrics()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "replay-sample.json");
        var scenario = ReplayScenarioLoader.Load(fixturePath);
        var harness = new ReplayHarness();

        var metrics = harness.Execute(scenario);

        Assert.Equal("baseline-observation-stream", scenario.Name);
        Assert.Equal(6, scenario.Observations.Count);
        Assert.Equal(6, metrics.ObservationCount);
        Assert.Equal(4, metrics.TxObservationCount);
        Assert.Equal(2, metrics.BlockObservationCount);
        Assert.Equal(6, metrics.LastSequence);
    }
}
