namespace Dxs.Consigliere.Benchmarks.FullSystem;

public sealed record VNextFullSystemBenchmarkScenario(
    string Name,
    int TransferCount,
    int QueryCount,
    int SoakCycles
);
