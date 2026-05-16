namespace Dxs.Consigliere.WebSockets;

/// <summary>
/// Frozen by Wave 1 contract freeze (S0.8). Property names, types, and
/// arity must not change without a contract-freeze amendment slice in
/// the bsv-headers-chain-wave package. Consumed by W3 (`OnReorg.NewTipHash`
/// resolves against this), W6 (admin panel).
/// </summary>
public sealed record BlockTipDto(
    string Hash,
    long Height,
    long TimestampMs,
    string PrevHash,
    int HeaderSize);
