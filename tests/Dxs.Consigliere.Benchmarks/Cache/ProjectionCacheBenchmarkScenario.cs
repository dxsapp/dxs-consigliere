namespace Dxs.Consigliere.Benchmarks.Cache;

public sealed record ProjectionCacheBenchmarkScenario(
    string Name,
    string Backend,
    int AddressCount,
    int HistoryTransactionCount,
    int UtxoCountPerAddress,
    int QueryIterations,
    int Take
);
