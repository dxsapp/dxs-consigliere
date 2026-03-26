#nullable enable

namespace Dxs.Bsv.Tokens.Dstas.Validation;

public sealed record DstasRedeemSemantics(
    bool IsRedeem,
    string? RedeemAddress,
    bool UsesRegularSpending,
    bool RedeemBlockedByState,
    bool RedeemByIssuerOwner
);
