using System.Globalization;
using System.Text;

using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Benchmarks.AddressHistory;

public class AddressHistoryEnvelopeBackfillBenchmarkEvidenceTests
{
    private static readonly string EvidencePath = RepoPathResolver.ResolveFromRepoRoot(
        "doc",
        "stream-tasks",
        "cache-coupling-wave",
        "benchmarks",
        "J05-address-history-envelope-backfill-benchmarks.md");

    [Fact]
    public async Task WritesEnvelopeBackfillBenchmarkEvidence()
    {
        var harness = new AddressHistoryBenchmarkHarness();
        var scenario = new AddressHistoryBenchmarkScenario(
            "address-history-envelope-backfill",
            TransferCount: 96,
            QueryCount: 12,
            Take: 16,
            Skip: 32);

        var legacy = await harness.MeasureLegacyQueryFallbackAsync(scenario);
        var afterBackfill = await harness.MeasureQueryAfterBackfillAsync(scenario);

        var markdown = new StringBuilder()
            .AppendLine("# J05 Address History Envelope Backfill Benchmark Evidence")
            .AppendLine()
            .AppendLine($"- measured_at_utc: {DateTimeOffset.UtcNow:O}")
            .AppendLine($"- scenario: {scenario.Name}")
            .AppendLine($"- transfer_count: {scenario.TransferCount}")
            .AppendLine($"- query_count: {scenario.QueryCount}")
            .AppendLine($"- query_skip: {scenario.Skip}")
            .AppendLine($"- query_take: {scenario.Take}")
            .AppendLine($"- legacy_query_elapsed_ms: {legacy.ElapsedMilliseconds}")
            .AppendLine($"- legacy_query_throughput_per_sec: {legacy.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- query_after_backfill_elapsed_ms: {afterBackfill.ElapsedMilliseconds}")
            .AppendLine($"- query_after_backfill_throughput_per_sec: {afterBackfill.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- recovered_vs_legacy_ratio: {(legacy.ElapsedMilliseconds <= 0 ? 0 : (double)legacy.ElapsedMilliseconds / Math.Max(1, afterBackfill.ElapsedMilliseconds)).ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine();

        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath)!);
        await File.WriteAllTextAsync(EvidencePath, markdown.ToString());

        Assert.True(legacy.ThroughputPerSecond > 0);
        Assert.True(afterBackfill.ThroughputPerSecond > 0);
    }
}
