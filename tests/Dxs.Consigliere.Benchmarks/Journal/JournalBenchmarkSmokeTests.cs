using Dxs.Consigliere.Benchmarks.Journal;

namespace Dxs.Consigliere.Benchmarks.Journal;

public class JournalBenchmarkSmokeTests
{
    [Fact]
    public async Task MeasuresAppendReplayAndDuplicatePaths()
    {
        var harness = new JournalBenchmarkHarness();
        var scenario = new JournalBenchmarkScenario(
            "journal-baseline",
            ObservationCount: 64,
            DuplicateAttempts: 32,
            IncludePayloadReferences: true
        );

        var append = await harness.MeasureAppendAsync(scenario);
        var replay = await harness.MeasureReplayAsync(scenario);
        var duplicate = await harness.MeasureDuplicateAsync(scenario);

        Assert.Equal(64, append.Observations);
        Assert.Equal(64, replay.Observations);
        Assert.Equal(1, duplicate.Observations);
        Assert.Equal(32, duplicate.DuplicateAttempts);
        Assert.True(append.ThroughputPerSecond > 0);
        Assert.True(replay.ThroughputPerSecond > 0);
        Assert.True(duplicate.ThroughputPerSecond > 0);
    }
}
