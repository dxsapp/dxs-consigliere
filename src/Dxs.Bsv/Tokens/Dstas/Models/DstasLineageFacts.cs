#nullable enable
using System.Collections.Generic;

namespace Dxs.Bsv.Tokens.Dstas.Models;

public sealed record DstasLineageFacts(
    bool IsStas,
    bool AllInputsKnown,
    int StasInputsCount,
    bool FirstOutputIsRedeemType,
    string? RedeemAddress,
    string? StasFrom,
    string? FirstInputTokenId,
    string? FirstOutputHash160,
    bool? FirstInputFrozen,
    bool? FirstOutputFrozen,
    string? FirstInputActionType,
    int? DstasSpendingType,
    IReadOnlyCollection<string> InputOptionalDataFingerprints,
    IReadOnlyCollection<string> OutputOptionalDataFingerprints
);
