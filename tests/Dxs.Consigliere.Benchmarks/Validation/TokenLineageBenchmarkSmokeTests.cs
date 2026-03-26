namespace Dxs.Consigliere.Benchmarks.Validation;

public class TokenLineageBenchmarkSmokeTests
{
    [Fact]
    public async Task MeasuresEvaluationAndBurstPaths()
    {
        var harness = new TokenLineageBenchmarkHarness();
        var scenario = new TokenLineageBenchmarkScenario("token-lineage-baseline", EvaluationCount: 256, DependentCount: 64);

        var evaluation = await harness.MeasureEvaluationAsync(scenario);
        var burst = await harness.MeasureRevalidationBurstAsync(scenario);

        Assert.Equal(256, evaluation.Operations);
        Assert.Equal(64, burst.Operations);
        Assert.True(evaluation.ThroughputPerSecond > 0);
        Assert.True(burst.ThroughputPerSecond > 0);
    }
}
