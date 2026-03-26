namespace Dxs.Consigliere.Benchmarks.Realtime;

public sealed record RealtimeFanoutBenchmarkScenario(
    string Name,
    int AddressFanout,
    int TokenFanout,
    int TransactionCount
);
