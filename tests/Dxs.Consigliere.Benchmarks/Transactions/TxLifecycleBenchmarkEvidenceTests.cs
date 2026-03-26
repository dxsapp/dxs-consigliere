using System.Globalization;
using System.Text;

using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Benchmarks.Transactions;

public class TxLifecycleBenchmarkEvidenceTests
{
    private static readonly string EvidencePath = RepoPathResolver.ResolveFromRepoRoot(
        "doc",
        "stream-tasks",
        "consigliere-vnext",
        "benchmarks",
        "S16-tx-lifecycle-benchmarks-evidence.md");

    [Fact]
    public async Task WritesBenchmarkEvidenceSnapshot()
    {
        var harness = new TxLifecycleBenchmarkHarness();
        var scenario = new TxLifecycleBenchmarkScenario("tx-lifecycle-baseline", TransactionCount: 128);

        var rebuild = await harness.MeasureRebuildAsync(scenario);
        var query = await harness.MeasureQueryAsync(scenario);

        var markdown = new StringBuilder()
            .AppendLine("# S16 Tx Lifecycle Benchmark Evidence")
            .AppendLine()
            .AppendLine($"- measured_at_utc: {DateTimeOffset.UtcNow:O}")
            .AppendLine($"- scenario: {scenario.Name}")
            .AppendLine($"- rebuild_observations: {rebuild.Observations}")
            .AppendLine($"- rebuild_elapsed_ms: {rebuild.ElapsedMilliseconds}")
            .AppendLine($"- rebuild_throughput_per_sec: {rebuild.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- query_count: {query.Queries}")
            .AppendLine($"- query_elapsed_ms: {query.ElapsedMilliseconds}")
            .AppendLine($"- query_throughput_per_sec: {query.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine();

        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath)!);
        await File.WriteAllTextAsync(EvidencePath, markdown.ToString());

        Assert.True(rebuild.ThroughputPerSecond > 0);
        Assert.True(query.ThroughputPerSecond > 0);
    }
}
