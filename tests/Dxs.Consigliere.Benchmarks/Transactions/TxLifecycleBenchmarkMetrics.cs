namespace Dxs.Consigliere.Benchmarks.Transactions;

public sealed record TxLifecycleBenchmarkMetrics(
    string ScenarioName,
    int Observations,
    int Queries,
    long ElapsedMilliseconds,
    double ThroughputPerSecond
);
