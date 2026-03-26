namespace Dxs.Consigliere.Benchmarks.Validation;

public sealed record TokenLineageBenchmarkMetrics(
    string Name,
    int Operations,
    long ElapsedMilliseconds,
    double ThroughputPerSecond
);
