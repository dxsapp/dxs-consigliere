namespace Dxs.Consigliere.Benchmarks.Storage;

public sealed record StorageGrowthBenchmarkScenario(
    string Name,
    int TxObservationCount,
    int RawTransactionBytes,
    bool PersistPayloads
);
