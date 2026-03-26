using System.Globalization;
using System.Text;

using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Benchmarks.Realtime;

public class RealtimeFanoutBenchmarkEvidenceTests
{
    private static readonly string EvidencePath = RepoPathResolver.ResolveFromRepoRoot(
        "doc",
        "stream-tasks",
        "consigliere-vnext",
        "benchmarks",
        "B4-realtime-fanout-benchmarks-evidence.md");

    [Fact]
    public async Task WritesBenchmarkEvidenceSnapshot()
    {
        var harness = new RealtimeFanoutBenchmarkHarness();
        var scenario = new RealtimeFanoutBenchmarkScenario("realtime-fanout-baseline", AddressFanout: 4, TokenFanout: 3, TransactionCount: 16);

        var seen = await harness.MeasureSeenFanoutAsync(scenario);
        var confirmed = await harness.MeasureConfirmedFanoutAsync(scenario);

        var markdown = new StringBuilder()
            .AppendLine("# B4 Realtime Fanout Benchmark Evidence")
            .AppendLine()
            .AppendLine($"- measured_at_utc: {DateTimeOffset.UtcNow:O}")
            .AppendLine($"- scenario: {scenario.Name}")
            .AppendLine($"- seen_operations: {seen.Operations}")
            .AppendLine($"- seen_published_events: {seen.PublishedEvents}")
            .AppendLine($"- seen_elapsed_ms: {seen.ElapsedMilliseconds}")
            .AppendLine($"- seen_throughput_per_sec: {seen.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- confirmed_operations: {confirmed.Operations}")
            .AppendLine($"- confirmed_published_events: {confirmed.PublishedEvents}")
            .AppendLine($"- confirmed_elapsed_ms: {confirmed.ElapsedMilliseconds}")
            .AppendLine($"- confirmed_throughput_per_sec: {confirmed.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine();

        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath)!);
        await File.WriteAllTextAsync(EvidencePath, markdown.ToString());

        Assert.True(seen.ThroughputPerSecond > 0);
        Assert.True(confirmed.ThroughputPerSecond > 0);
    }
}
