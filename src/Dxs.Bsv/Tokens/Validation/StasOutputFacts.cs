#nullable enable
using System.Collections.Generic;

namespace Dxs.Bsv.Tokens.Validation;

public sealed record StasOutputFacts(
    bool WithNote,
    string? RedeemAddress,
    bool? FirstOutputFrozen,
    IReadOnlyList<string> OutputTokens,
    IReadOnlySet<string> OutputOptionalDataFingerprints
);
