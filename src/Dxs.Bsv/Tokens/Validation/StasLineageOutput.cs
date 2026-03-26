#nullable enable
using Dxs.Bsv.Script;

namespace Dxs.Bsv.Tokens.Validation;

public sealed record StasLineageOutput(
    ScriptType Type,
    string? Address = null,
    string? TokenId = null,
    string? Hash160 = null,
    bool? DstasFrozen = null,
    string? DstasActionType = null,
    string? DstasOptionalDataFingerprint = null
);
