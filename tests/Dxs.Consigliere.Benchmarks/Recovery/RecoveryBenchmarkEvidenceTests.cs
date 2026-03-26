using System.Globalization;
using System.Text;

using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Benchmarks.Recovery;

public class RecoveryBenchmarkEvidenceTests
{
    private static readonly string EvidencePath = RepoPathResolver.ResolveFromRepoRoot(
        "doc",
        "stream-tasks",
        "consigliere-vnext",
        "benchmarks",
        "B3-recovery-reorg-benchmarks-evidence.md");

    [Fact]
    public async Task WritesBenchmarkEvidenceSnapshot()
    {
        var harness = new RecoveryBenchmarkHarness();
        var scenario = new RecoveryBenchmarkScenario("recovery-remediated", TransferCount: 12, PendingCount: 12);

        var reorg = await harness.MeasureReorgRecoveryAsync(scenario);
        var drop = await harness.MeasureDropRecoveryAsync(scenario);

        var markdown = new StringBuilder()
            .AppendLine("# B3 Recovery And Reorg Benchmark Evidence")
            .AppendLine()
            .AppendLine($"- measured_at_utc: {DateTimeOffset.UtcNow:O}")
            .AppendLine($"- scenario: {scenario.Name}")
            .AppendLine($"- reorg_operations: {reorg.Operations}")
            .AppendLine($"- reorg_elapsed_ms: {reorg.ElapsedMilliseconds}")
            .AppendLine($"- reorg_throughput_per_sec: {reorg.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- drop_operations: {drop.Operations}")
            .AppendLine($"- drop_elapsed_ms: {drop.ElapsedMilliseconds}")
            .AppendLine($"- drop_throughput_per_sec: {drop.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine();

        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath)!);
        await File.WriteAllTextAsync(EvidencePath, markdown.ToString());

        Assert.True(reorg.ThroughputPerSecond > 0);
        Assert.True(drop.ThroughputPerSecond > 0);
    }
}
