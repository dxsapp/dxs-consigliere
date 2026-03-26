namespace Dxs.Consigliere.Benchmarks.Ingest;

public class IngestBenchmarkSmokeTests
{
    [Fact]
    public async Task MeasuresTxBlockAndMixedIngestPaths()
    {
        var harness = new IngestBenchmarkHarness();
        var scenario = new IngestBenchmarkScenario(
            "ingest-baseline",
            TxObservationCount: 48,
            BlockObservationCount: 16,
            PersistPayloads: true);

        var tx = await harness.MeasureTxIngestAsync(scenario);
        var block = await harness.MeasureBlockIngestAsync(scenario);
        var mixed = await harness.MeasureMixedBurstAsync(scenario);

        Assert.Equal(48, tx.TxObservations);
        Assert.Equal(16, block.BlockObservations);
        Assert.Equal(48, mixed.TxObservations);
        Assert.Equal(16, mixed.BlockObservations);
        Assert.True(tx.PayloadWrites > 0);
        Assert.True(tx.ThroughputPerSecond > 0);
        Assert.True(block.ThroughputPerSecond > 0);
        Assert.True(mixed.ThroughputPerSecond > 0);
    }
}
