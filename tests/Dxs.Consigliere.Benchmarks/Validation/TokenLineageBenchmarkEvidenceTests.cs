using System.Globalization;
using System.Text;

using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Benchmarks.Validation;

public class TokenLineageBenchmarkEvidenceTests
{
    private static readonly string EvidencePath = RepoPathResolver.ResolveFromRepoRoot(
        "doc",
        "stream-tasks",
        "consigliere-vnext",
        "benchmarks",
        "S23-token-lineage-benchmarks-evidence.md");

    [Fact]
    public async Task WritesBenchmarkEvidenceSnapshot()
    {
        var harness = new TokenLineageBenchmarkHarness();
        var scenario = new TokenLineageBenchmarkScenario("token-lineage-baseline", EvaluationCount: 1024, DependentCount: 128);

        var evaluation = await harness.MeasureEvaluationAsync(scenario);
        var burst = await harness.MeasureRevalidationBurstAsync(scenario);

        var markdown = new StringBuilder()
            .AppendLine("# S23 Token Lineage Benchmark Evidence")
            .AppendLine()
            .AppendLine($"- measured_at_utc: {DateTimeOffset.UtcNow:O}")
            .AppendLine($"- scenario: {scenario.Name}")
            .AppendLine($"- evaluation_count: {evaluation.Operations}")
            .AppendLine($"- evaluation_elapsed_ms: {evaluation.ElapsedMilliseconds}")
            .AppendLine($"- evaluation_throughput_per_sec: {evaluation.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- burst_dependents: {burst.Operations}")
            .AppendLine($"- burst_elapsed_ms: {burst.ElapsedMilliseconds}")
            .AppendLine($"- burst_throughput_per_sec: {burst.ThroughputPerSecond.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine();

        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath)!);
        await File.WriteAllTextAsync(EvidencePath, markdown.ToString());

        Assert.True(evaluation.ThroughputPerSecond > 0);
        Assert.True(burst.ThroughputPerSecond > 0);
    }
}
