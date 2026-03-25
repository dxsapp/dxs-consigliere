namespace Dxs.Consigliere.Benchmarks.Journal;

public sealed record JournalBenchmarkScenario(
    string Name,
    int ObservationCount,
    int DuplicateAttempts,
    bool IncludePayloadReferences
);
