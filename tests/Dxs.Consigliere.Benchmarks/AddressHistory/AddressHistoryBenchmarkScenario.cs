namespace Dxs.Consigliere.Benchmarks.AddressHistory;

public sealed record AddressHistoryBenchmarkScenario(
    string Name,
    int TransferCount,
    int QueryCount,
    int Take,
    int Skip = 0
);
