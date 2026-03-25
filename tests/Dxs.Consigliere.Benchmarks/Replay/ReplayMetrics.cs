namespace Dxs.Consigliere.Benchmarks.Replay;

public sealed record ReplayMetrics(
    int ObservationCount,
    int TxObservationCount,
    int BlockObservationCount,
    long LastSequence
);
