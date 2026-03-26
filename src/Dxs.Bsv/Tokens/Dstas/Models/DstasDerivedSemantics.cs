#nullable enable
namespace Dxs.Bsv.Tokens.Dstas.Models;

public sealed record DstasDerivedSemantics(
    string? EventType,
    int? SpendingType,
    bool? InputFrozen,
    bool? OutputFrozen,
    bool? OptionalDataContinuity,
    bool IsRedeem,
    string? RedeemAddress,
    bool UsesRegularSpending,
    bool RedeemBlockedByState,
    bool RedeemByIssuerOwner
);
