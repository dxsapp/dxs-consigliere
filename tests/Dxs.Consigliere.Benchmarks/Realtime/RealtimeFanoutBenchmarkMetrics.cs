namespace Dxs.Consigliere.Benchmarks.Realtime;

public sealed record RealtimeFanoutBenchmarkMetrics(
    string Name,
    int Operations,
    int PublishedEvents,
    long ElapsedMilliseconds,
    double ThroughputPerSecond
);
