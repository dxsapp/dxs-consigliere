namespace Dxs.Consigliere.Benchmarks.Recovery;

public sealed record RecoveryBenchmarkScenario(
    string Name,
    int TransferCount,
    int PendingCount
);
