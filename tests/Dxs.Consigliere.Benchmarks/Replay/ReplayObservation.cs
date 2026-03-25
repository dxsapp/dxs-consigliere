namespace Dxs.Consigliere.Benchmarks.Replay;

public sealed record ReplayObservation(
    long Sequence,
    ReplayEventType EventType,
    string Source,
    string EntityId,
    string? BlockHash,
    int? BlockHeight,
    DateTimeOffset ObservedAtUtc,
    string? PayloadRef
);
