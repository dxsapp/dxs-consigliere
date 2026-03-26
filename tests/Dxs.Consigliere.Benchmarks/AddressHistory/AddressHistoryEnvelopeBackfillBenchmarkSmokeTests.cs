namespace Dxs.Consigliere.Benchmarks.AddressHistory;

public class AddressHistoryEnvelopeBackfillBenchmarkSmokeTests
{
    [Fact]
    public async Task MeasuresLegacyQueryRecoveryAfterBackfill()
    {
        var harness = new AddressHistoryBenchmarkHarness();
        var scenario = new AddressHistoryBenchmarkScenario(
            "address-history-envelope-backfill",
            TransferCount: 24,
            QueryCount: 8,
            Take: 16,
            Skip: 12);

        var legacy = await harness.MeasureLegacyQueryFallbackAsync(scenario);
        var afterBackfill = await harness.MeasureQueryAfterBackfillAsync(scenario);

        Assert.True(legacy.ThroughputPerSecond > 0);
        Assert.True(afterBackfill.ThroughputPerSecond > 0);
        Assert.True(afterBackfill.RowsReturned > 0);
    }
}
