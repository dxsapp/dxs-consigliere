namespace Dxs.Consigliere.Benchmarks.AddressHistory;

public sealed record AddressHistoryBenchmarkMetrics(
    string Name,
    int ProjectedTransactions,
    int Queries,
    int RowsReturned,
    long ElapsedMilliseconds,
    double ThroughputPerSecond
);
