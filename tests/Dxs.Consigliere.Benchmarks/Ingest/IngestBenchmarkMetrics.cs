namespace Dxs.Consigliere.Benchmarks.Ingest;

public sealed record IngestBenchmarkMetrics(
    string Name,
    int TxObservations,
    int BlockObservations,
    int PayloadWrites,
    long ElapsedMilliseconds,
    double ThroughputPerSecond
);
