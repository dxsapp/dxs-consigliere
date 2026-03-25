namespace Dxs.Consigliere.Benchmarks.Journal;

public sealed record JournalBenchmarkMetrics(
    string Name,
    int Observations,
    int DuplicateAttempts,
    int DuplicatesDetected,
    long ElapsedMilliseconds,
    double ThroughputPerSecond,
    long LastSequence
);
