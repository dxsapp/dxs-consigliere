#nullable enable

using System.Collections.Generic;

namespace Dxs.Bsv.ScriptEvaluation;

public sealed record ScriptEvaluationResult(
    int InputIndex,
    bool Success,
    string ErrorCode,
    string? Detail = null,
    IReadOnlyList<BsvScriptTraceStep>? Trace = null
);
