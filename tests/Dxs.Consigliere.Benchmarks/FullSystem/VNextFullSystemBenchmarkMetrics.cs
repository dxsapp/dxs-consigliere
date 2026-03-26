namespace Dxs.Consigliere.Benchmarks.FullSystem;

public sealed record VNextFullSystemBenchmarkMetrics(
    string Name,
    int Operations,
    long ElapsedMilliseconds,
    double ThroughputPerSecond
);
