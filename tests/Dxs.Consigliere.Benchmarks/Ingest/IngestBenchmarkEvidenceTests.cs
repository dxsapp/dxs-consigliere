using System.Globalization;
using System.Text;

using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Benchmarks.Ingest;

public class IngestBenchmarkEvidenceTests
{
    private static readonly string EvidencePath = RepoPathResolver.ResolveFromRepoRoot(
        "doc",
        "stream-tasks",
        "consigliere-vnext",
        "benchmarks",
        "B1-ingest-benchmarks-evidence.md");

    [Fact]
    public async Task WritesBenchmarkEvidenceSnapshot()
    {
        var harness = new IngestBenchmarkHarness();
        var scenario = new IngestBenchmarkScenario("ingest-baseline", 64, 24, PersistPayloads: true);

        var tx = await harness.MeasureTxIngestAsync(scenario);
        var block = await harness.MeasureBlockIngestAsync(scenario);
        var mixed = await harness.MeasureMixedBurstAsync(scenario);

        var markdown = new StringBuilder()
            .AppendLine("# B1 Ingest Benchmark Evidence")
            .AppendLine()
            .AppendLine($"- measured_at_utc: {DateTimeOffset.UtcNow:O}")
            .AppendLine($"- scenario: {scenario.Name}")
            .AppendLine($"- tx_observations: {tx.TxObservations}")
            .AppendLine($"- tx_payload_writes: {tx.PayloadWrites}")
            .AppendLine($"- tx_elapsed_ms: {tx.ElapsedMilliseconds}")
            .AppendLine($"- tx_throughput_per_sec: {tx.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- block_observations: {block.BlockObservations}")
            .AppendLine($"- block_elapsed_ms: {block.ElapsedMilliseconds}")
            .AppendLine($"- block_throughput_per_sec: {block.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- mixed_total_observations: {mixed.TxObservations + mixed.BlockObservations}")
            .AppendLine($"- mixed_elapsed_ms: {mixed.ElapsedMilliseconds}")
            .AppendLine($"- mixed_throughput_per_sec: {mixed.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine();

        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath)!);
        await File.WriteAllTextAsync(EvidencePath, markdown.ToString());

        Assert.True(tx.ThroughputPerSecond > 0);
        Assert.True(block.ThroughputPerSecond > 0);
        Assert.True(mixed.ThroughputPerSecond > 0);
    }
}
