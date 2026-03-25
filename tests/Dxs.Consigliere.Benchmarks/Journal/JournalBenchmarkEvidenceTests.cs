using System.Text;

namespace Dxs.Consigliere.Benchmarks.Journal;

public class JournalBenchmarkEvidenceTests
{
    private const string EvidencePath = "/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/benchmarks/S08-journal-benchmarks-evidence.md";

    [Fact]
    public async Task WritesBenchmarkEvidenceSnapshot()
    {
        var harness = new JournalBenchmarkHarness();
        var scenario = new JournalBenchmarkScenario(
            "journal-baseline",
            ObservationCount: 128,
            DuplicateAttempts: 64,
            IncludePayloadReferences: true
        );

        var append = await harness.MeasureAppendAsync(scenario);
        var replay = await harness.MeasureReplayAsync(scenario);
        var duplicate = await harness.MeasureDuplicateAsync(scenario);

        var markdown = new StringBuilder()
            .AppendLine("# S08 Journal Benchmark Evidence")
            .AppendLine()
            .AppendLine($"- measured_at_utc: {DateTimeOffset.UtcNow:O}")
            .AppendLine($"- scenario: {scenario.Name}")
            .AppendLine($"- append_observations: {append.Observations}")
            .AppendLine($"- append_elapsed_ms: {append.ElapsedMilliseconds}")
            .AppendLine($"- append_throughput_per_sec: {append.ThroughputPerSecond:F2}")
            .AppendLine($"- replay_observations: {replay.Observations}")
            .AppendLine($"- replay_elapsed_ms: {replay.ElapsedMilliseconds}")
            .AppendLine($"- replay_throughput_per_sec: {replay.ThroughputPerSecond:F2}")
            .AppendLine($"- duplicate_attempts: {duplicate.DuplicateAttempts}")
            .AppendLine($"- duplicates_detected: {duplicate.DuplicatesDetected}")
            .AppendLine($"- duplicate_elapsed_ms: {duplicate.ElapsedMilliseconds}")
            .AppendLine($"- duplicate_throughput_per_sec: {duplicate.ThroughputPerSecond:F2}")
            .AppendLine($"- last_sequence: {Math.Max(append.LastSequence, replay.LastSequence)}")
            .AppendLine();

        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath)!);
        await File.WriteAllTextAsync(EvidencePath, markdown.ToString());

        Assert.True(append.ThroughputPerSecond > 0);
        Assert.True(replay.ThroughputPerSecond > 0);
        Assert.True(duplicate.ThroughputPerSecond > 0);
    }
}
