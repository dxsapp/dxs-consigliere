using System.Globalization;
using System.Text;

using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Benchmarks.Cache;

public class ProjectionCacheBenchmarkEvidenceTests
{
    private static readonly string EvidencePath = RepoPathResolver.ResolveFromRepoRoot(
        "doc",
        "stream-tasks",
        "cache-wave",
        "benchmarks",
        "C11-C13-projection-cache-benchmarks.md");

    private static readonly string AuditPath = RepoPathResolver.ResolveFromRepoRoot(
        "doc",
        "stream-tasks",
        "cache-wave",
        "audits",
        "A2.md");

    [Fact]
    public async Task WritesComparativeBenchmarkEvidence()
    {
        var harness = new ProjectionCacheBenchmarkHarness();
        var scenarioBase = new ProjectionCacheBenchmarkScenario(
            "projection-cache-comparison",
            "memory",
            AddressCount: 32,
            HistoryTransactionCount: 64,
            UtxoCountPerAddress: 4,
            QueryIterations: 400,
            Take: 48);

        var memory = await harness.MeasureAsync(scenarioBase);
        var azos = await harness.MeasureAsync(scenarioBase with { Backend = "azos", Name = "projection-cache-comparison-azos" });
        var decision = BuildDecision(memory, azos);

        Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath)!);
        await File.WriteAllTextAsync(EvidencePath, BuildEvidence(memory, azos, scenarioBase));

        Directory.CreateDirectory(Path.GetDirectoryName(AuditPath)!);
        await File.WriteAllTextAsync(AuditPath, decision);

        Assert.True(memory.HistoryQueriesPerSecond > 0);
        Assert.True(azos.HistoryQueriesPerSecond > 0);
    }

    private static string BuildEvidence(
        ProjectionCacheBenchmarkMetrics memory,
        ProjectionCacheBenchmarkMetrics azos,
        ProjectionCacheBenchmarkScenario scenario)
    {
        var markdown = new StringBuilder()
            .AppendLine("# C11-C13 Projection Cache Benchmark Evidence")
            .AppendLine()
            .AppendLine($"- measured_at_utc: {DateTimeOffset.UtcNow:O}")
            .AppendLine($"- scenario: {scenario.Name}")
            .AppendLine($"- address_count: {scenario.AddressCount}")
            .AppendLine($"- history_transaction_count: {scenario.HistoryTransactionCount}")
            .AppendLine($"- utxo_count_per_address: {scenario.UtxoCountPerAddress}")
            .AppendLine($"- query_iterations: {scenario.QueryIterations}")
            .AppendLine()
            .AppendLine("| backend | history_qps | balance_qps | utxo_qps | token_history_qps | invalidation_cycles_qps | cache_entries |")
            .AppendLine("|---|---:|---:|---:|---:|---:|---:|")
            .AppendLine(Row(memory))
            .AppendLine(Row(azos))
            .AppendLine()
            .AppendLine("- note: `Azos` benchmark uses the same `IProjectionReadCache` abstraction and compares only the backend implementation.");

        return markdown.ToString();
    }

    private static string BuildDecision(
        ProjectionCacheBenchmarkMetrics memory,
        ProjectionCacheBenchmarkMetrics azos)
    {
        var avgRatio =
            SafeRatio(azos.HistoryQueriesPerSecond, memory.HistoryQueriesPerSecond) +
            SafeRatio(azos.BalanceQueriesPerSecond, memory.BalanceQueriesPerSecond) +
            SafeRatio(azos.UtxoQueriesPerSecond, memory.UtxoQueriesPerSecond) +
            SafeRatio(azos.TokenHistoryQueriesPerSecond, memory.TokenHistoryQueriesPerSecond) +
            SafeRatio(azos.InvalidationCyclesPerSecond, memory.InvalidationCyclesPerSecond);
        avgRatio /= 5d;

        var recommendation = avgRatio >= 0.80d
            ? "keep optional"
            : "reject";

        return new StringBuilder()
            .AppendLine("# A2 Azos Decision Gate")
            .AppendLine()
            .AppendLine($"- measured_at_utc: {DateTimeOffset.UtcNow:O}")
            .AppendLine($"- average_relative_throughput: {avgRatio.ToString("F2", CultureInfo.InvariantCulture)}")
            .AppendLine($"- recommendation: {recommendation}")
            .AppendLine("- rationale: memory remains the simpler default backend; Azos is kept only if the comparative hit on hot-read and invalidation throughput stays within an acceptable range.")
            .AppendLine("- implementation_note: spike uses the official `Azos` package behind the same projection-cache abstraction.")
            .ToString();
    }

    private static string Row(ProjectionCacheBenchmarkMetrics metrics)
        => $"| {metrics.Backend} | {metrics.HistoryQueriesPerSecond.ToString("F2", CultureInfo.InvariantCulture)} | {metrics.BalanceQueriesPerSecond.ToString("F2", CultureInfo.InvariantCulture)} | {metrics.UtxoQueriesPerSecond.ToString("F2", CultureInfo.InvariantCulture)} | {metrics.TokenHistoryQueriesPerSecond.ToString("F2", CultureInfo.InvariantCulture)} | {metrics.InvalidationCyclesPerSecond.ToString("F2", CultureInfo.InvariantCulture)} | {metrics.CacheEntryCount} |";

    private static double SafeRatio(double numerator, double denominator)
        => denominator <= 0 ? 0 : numerator / denominator;
}
