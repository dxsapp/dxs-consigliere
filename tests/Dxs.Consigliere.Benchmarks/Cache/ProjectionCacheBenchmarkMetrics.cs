namespace Dxs.Consigliere.Benchmarks.Cache;

public sealed record ProjectionCacheBenchmarkMetrics(
    string Name,
    string Backend,
    int QueryIterations,
    long HistoryElapsedMilliseconds,
    double HistoryQueriesPerSecond,
    long BalanceElapsedMilliseconds,
    double BalanceQueriesPerSecond,
    long UtxoElapsedMilliseconds,
    double UtxoQueriesPerSecond,
    long TokenHistoryElapsedMilliseconds,
    double TokenHistoryQueriesPerSecond,
    long InvalidationElapsedMilliseconds,
    double InvalidationCyclesPerSecond,
    int CacheEntryCount
);
