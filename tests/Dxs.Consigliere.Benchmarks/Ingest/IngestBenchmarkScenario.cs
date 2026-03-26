namespace Dxs.Consigliere.Benchmarks.Ingest;

public sealed record IngestBenchmarkScenario(
    string Name,
    int TxObservationCount,
    int BlockObservationCount,
    bool PersistPayloads
);
