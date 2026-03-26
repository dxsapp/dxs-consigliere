using System.Globalization;
using System.Text;

using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Benchmarks.FullSystem;

public class VNextFullSystemBenchmarkEvidenceTests
{
    private static readonly string EvidencePath = RepoPathResolver.ResolveFromRepoRoot(
        "doc",
        "stream-tasks",
        "consigliere-vnext",
        "benchmarks",
        "S29-full-system-benchmarks-evidence.md");

    [Fact]
    public async Task WritesBenchmarkEvidenceSnapshot()
    {
        var harness = new VNextFullSystemBenchmarkHarness();
        var scenario = new VNextFullSystemBenchmarkScenario("vnext-full-system-baseline", TransferCount: 4, QueryCount: 4, SoakCycles: 2);

        var replay = await harness.MeasureReplayAsync(scenario);
        var query = await harness.MeasureQueryBundleAsync(scenario);
        var soak = await harness.MeasureSoakAsync(scenario);

        var markdown = new StringBuilder()
            .AppendLine("# S29 Full-System Benchmark Evidence")
            .AppendLine()
            .AppendLine($"- measured_at_utc: {DateTimeOffset.UtcNow:O}")
            .AppendLine($"- scenario: {scenario.Name}")
            .AppendLine($"- replay_operations: {replay.Operations}")
            .AppendLine($"- replay_elapsed_ms: {replay.ElapsedMilliseconds}")
            .AppendLine($"- replay_throughput_per_sec: {replay.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- query_operations: {query.Operations}")
            .AppendLine($"- query_elapsed_ms: {query.ElapsedMilliseconds}")
            .AppendLine($"- query_throughput_per_sec: {query.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- soak_operations: {soak.Operations}")
            .AppendLine($"- soak_elapsed_ms: {soak.ElapsedMilliseconds}")
            .AppendLine($"- soak_throughput_per_sec: {soak.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine();

        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath)!);
        await File.WriteAllTextAsync(EvidencePath, markdown.ToString());

        Assert.True(replay.ThroughputPerSecond > 0);
        Assert.True(query.ThroughputPerSecond > 0);
        Assert.True(soak.ThroughputPerSecond > 0);
    }
}
