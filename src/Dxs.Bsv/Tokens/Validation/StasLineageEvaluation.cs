#nullable enable
using System.Collections.Generic;

namespace Dxs.Bsv.Tokens.Validation;

public sealed record StasLineageEvaluation(
    bool IsStas,
    bool IsIssue,
    bool IsValidIssue,
    bool IsRedeem,
    bool IsWithFee,
    bool IsWithNote,
    bool AllInputsKnown,
    string? RedeemAddress,
    string? StasFrom,
    string? DstasEventType,
    int? DstasSpendingType,
    bool? DstasInputFrozen,
    bool? DstasOutputFrozen,
    bool? DstasOptionalDataContinuity,
    IReadOnlyList<string> TokenIds,
    IReadOnlyList<string> IllegalRoots,
    IReadOnlyList<string> MissingDependencies
);
