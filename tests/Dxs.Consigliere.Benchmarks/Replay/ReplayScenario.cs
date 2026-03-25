namespace Dxs.Consigliere.Benchmarks.Replay;

public sealed record ReplayScenario(
    string Name,
    IReadOnlyList<ReplayObservation> Observations
);
