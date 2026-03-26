using System.Globalization;
using System.Text;

using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Benchmarks.AddressHistory;

public class AddressHistoryBenchmarkEvidenceTests
{
    private static readonly string EvidencePath = RepoPathResolver.ResolveFromRepoRoot(
        "doc",
        "stream-tasks",
        "consigliere-vnext",
        "benchmarks",
        "B2-address-history-benchmarks-evidence.md");

    [Fact]
    public async Task WritesBenchmarkEvidenceSnapshot()
    {
        var harness = new AddressHistoryBenchmarkHarness();
        var scenario = new AddressHistoryBenchmarkScenario("address-history-baseline", TransferCount: 32, QueryCount: 10, Take: 48);

        var rebuild = await harness.MeasureRebuildAsync(scenario);
        var query = await harness.MeasureQueryAsync(scenario);

        var markdown = new StringBuilder()
            .AppendLine("# B2 Address History Benchmark Evidence")
            .AppendLine()
            .AppendLine($"- measured_at_utc: {DateTimeOffset.UtcNow:O}")
            .AppendLine($"- scenario: {scenario.Name}")
            .AppendLine($"- rebuild_projected_transactions: {rebuild.ProjectedTransactions}")
            .AppendLine($"- rebuild_elapsed_ms: {rebuild.ElapsedMilliseconds}")
            .AppendLine($"- rebuild_throughput_per_sec: {rebuild.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- query_count: {query.Queries}")
            .AppendLine($"- query_rows_returned: {query.RowsReturned}")
            .AppendLine($"- query_elapsed_ms: {query.ElapsedMilliseconds}")
            .AppendLine($"- query_throughput_per_sec: {query.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine();

        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath)!);
        await File.WriteAllTextAsync(EvidencePath, markdown.ToString());

        Assert.True(rebuild.ThroughputPerSecond > 0);
        Assert.True(query.ThroughputPerSecond > 0);
    }
}
