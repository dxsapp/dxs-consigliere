namespace Dxs.Consigliere.WebSockets;

/// <summary>
/// Frozen by Wave 1 contract freeze (S0.8). W3 owns the emitter; W1
/// only registers the signature and the `block:reorg` subscription
/// group. Property names, types, and arity must not change without a
/// contract-freeze amendment slice in the bsv-headers-chain-wave
/// package.
/// </summary>
public sealed record ReorgEventDto(
    string CommonAncestorHash,
    long CommonAncestorHeight,
    string[] OrphanedHashes,
    string NewTipHash,
    long NewTipHeight,
    bool DegradedState);
