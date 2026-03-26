#nullable enable
using System.Collections.Generic;

namespace Dxs.Bsv.Tokens.Validation;

public sealed record StasDependencyFacts(
    bool WithFee,
    bool AllInputsKnown,
    string? StasFrom,
    string? FirstInputHash160,
    string? FirstInputTokenId,
    bool? FirstInputFrozen,
    string? FirstInputActionType,
    int? DstasSpendingType,
    int StasInputsCount,
    IReadOnlyList<string> InputTokens,
    IReadOnlyList<string> IllegalRoots,
    IReadOnlyList<string> MissingDependencies,
    IReadOnlySet<string> InputOptionalDataFingerprints
);
