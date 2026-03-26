namespace Dxs.Consigliere.Benchmarks.Recovery;

public sealed record RecoveryBenchmarkMetrics(
    string Name,
    int Operations,
    long ElapsedMilliseconds,
    double ThroughputPerSecond
);
