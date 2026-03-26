namespace Dxs.Consigliere.Benchmarks.Validation;

public sealed record TokenLineageBenchmarkScenario(
    string Name,
    int EvaluationCount,
    int DependentCount
);
