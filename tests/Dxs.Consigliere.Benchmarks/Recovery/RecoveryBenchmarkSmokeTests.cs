namespace Dxs.Consigliere.Benchmarks.Recovery;

public class RecoveryBenchmarkSmokeTests
{
    [Fact]
    public async Task MeasuresReorgAndDropRecoveryPaths()
    {
        var harness = new RecoveryBenchmarkHarness();
        var scenario = new RecoveryBenchmarkScenario(
            "recovery-baseline",
            TransferCount: 2,
            PendingCount: 2);

        var reorg = await harness.MeasureReorgRecoveryAsync(scenario);
        var drop = await harness.MeasureDropRecoveryAsync(scenario);

        Assert.Equal(2, reorg.Operations);
        Assert.Equal(2, drop.Operations);
        Assert.True(reorg.ThroughputPerSecond > 0);
        Assert.True(drop.ThroughputPerSecond > 0);
    }
}
