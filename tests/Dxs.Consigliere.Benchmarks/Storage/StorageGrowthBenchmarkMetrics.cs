namespace Dxs.Consigliere.Benchmarks.Storage;

public sealed record StorageGrowthBenchmarkMetrics(
    string Name,
    int TxObservations,
    int RawTransactionBytes,
    bool PersistPayloads,
    int JournalDocumentCount,
    int PayloadDocumentCount,
    long JournalObservationJsonBytes,
    long JournalDocumentBytes,
    long PayloadHexBytes,
    long PayloadDocumentBytes,
    long ElapsedMilliseconds,
    double ThroughputPerSecond
);
