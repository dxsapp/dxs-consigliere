using System.Collections.Generic;

namespace Dxs.Bsv.ScriptEvaluation;

public sealed record TransactionEvaluationResult(
    bool Success,
    IReadOnlyList<ScriptEvaluationResult> Inputs
);
