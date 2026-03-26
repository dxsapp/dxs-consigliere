using System.Globalization;
using System.Text;

using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Benchmarks.AddressHistory;

public class AddressHistoryBenchmarkEvidenceTests
{
    private static readonly string EvidencePath = RepoPathResolver.ResolveFromRepoRoot(
        "doc",
        "stream-tasks",
        "cache-followup-wave",
        "benchmarks",
        "F09-address-history-optimization-benchmarks.md");

    [Fact]
    public async Task WritesBenchmarkEvidenceSnapshot()
    {
        var harness = new AddressHistoryBenchmarkHarness();
        var scenario = new AddressHistoryBenchmarkScenario("address-history-baseline", TransferCount: 32, QueryCount: 10, Take: 48);

        var rebuild = await harness.MeasureRebuildAsync(scenario);
        var query = await harness.MeasureQueryAsync(scenario);
        var legacyQuery = await harness.MeasureLegacyQueryFallbackAsync(scenario);

        var markdown = new StringBuilder()
            .AppendLine("# F09 Address History Optimization Benchmark Evidence")
            .AppendLine()
            .AppendLine($"- measured_at_utc: {DateTimeOffset.UtcNow:O}")
            .AppendLine($"- scenario: {scenario.Name}")
            .AppendLine($"- rebuild_projected_transactions: {rebuild.ProjectedTransactions}")
            .AppendLine($"- rebuild_elapsed_ms: {rebuild.ElapsedMilliseconds}")
            .AppendLine($"- rebuild_throughput_per_sec: {rebuild.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- query_count: {query.Queries}")
            .AppendLine($"- query_rows_returned: {query.RowsReturned}")
            .AppendLine($"- optimized_query_elapsed_ms: {query.ElapsedMilliseconds}")
            .AppendLine($"- optimized_query_throughput_per_sec: {query.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- legacy_query_elapsed_ms: {legacyQuery.ElapsedMilliseconds}")
            .AppendLine($"- legacy_query_throughput_per_sec: {legacyQuery.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- optimized_vs_legacy_ratio: {(legacyQuery.ElapsedMilliseconds <= 0 ? 0 : (double)legacyQuery.ElapsedMilliseconds / Math.Max(1, query.ElapsedMilliseconds)).ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine();

        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath)!);
        await File.WriteAllTextAsync(EvidencePath, markdown.ToString());

        Assert.True(rebuild.ThroughputPerSecond > 0);
        Assert.True(query.ThroughputPerSecond > 0);
        Assert.True(legacyQuery.ThroughputPerSecond > 0);
    }
}
