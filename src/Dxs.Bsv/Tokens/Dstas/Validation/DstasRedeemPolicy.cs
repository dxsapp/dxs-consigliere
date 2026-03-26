#nullable enable
using System;
using Dxs.Bsv.Tokens.Dstas.Models;

namespace Dxs.Bsv.Tokens.Dstas.Validation;

public sealed class DstasRedeemPolicy
{
    public DstasRedeemSemantics Derive(DstasLineageFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var redeemBlockedByState = facts.FirstInputFrozen == true || string.Equals(facts.FirstInputActionType, DstasActionTypes.Confiscation, StringComparison.Ordinal);
        var redeemUsesRegularSpending = facts.DstasSpendingType is null or DstasSpendingTypes.Regular;
        var redeemByIssuerOwner = string.Equals(facts.StasFrom, facts.RedeemAddress, StringComparison.Ordinal);
        var isRedeem =
            facts.AllInputsKnown &&
            facts.StasInputsCount == 1 &&
            facts.FirstOutputIsRedeemType &&
            redeemUsesRegularSpending &&
            redeemByIssuerOwner &&
            !redeemBlockedByState &&
            string.Equals(facts.FirstInputTokenId, facts.FirstOutputHash160, StringComparison.Ordinal);

        return new DstasRedeemSemantics(
            isRedeem,
            isRedeem ? facts.RedeemAddress : null,
            redeemUsesRegularSpending,
            redeemBlockedByState,
            redeemByIssuerOwner);
    }
}
