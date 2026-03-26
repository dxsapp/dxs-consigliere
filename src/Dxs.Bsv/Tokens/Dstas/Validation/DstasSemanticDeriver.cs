#nullable enable
using System;
using System.Linq;

using Dxs.Bsv.Tokens.Dstas.Models;

namespace Dxs.Bsv.Tokens.Dstas.Validation;

public sealed class DstasSemanticDeriver
{
    public DstasDerivedSemantics Derive(DstasLineageFacts facts)
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

        string? eventType = null;
        if (facts.IsStas && facts.DstasSpendingType is not null)
        {
            if (facts.DstasSpendingType == DstasSpendingTypes.SwapCancel)
            {
                eventType = DstasEventTypes.SwapCancel;
            }
            else if (facts.DstasSpendingType == DstasSpendingTypes.Confiscation)
            {
                eventType = DstasEventTypes.Confiscation;
            }
            else if (facts.DstasSpendingType == DstasSpendingTypes.FreezeToggle)
            {
                eventType = facts.FirstInputFrozen == true && facts.FirstOutputFrozen == false
                    ? DstasEventTypes.Unfreeze
                    : DstasEventTypes.Freeze;
            }
        }

        if (eventType is null &&
            facts.IsStas &&
            facts.DstasSpendingType is null or DstasSpendingTypes.Regular &&
            string.Equals(facts.FirstInputActionType, DstasActionTypes.Swap, StringComparison.Ordinal))
        {
            eventType = DstasEventTypes.Swap;
        }

        bool? optionalDataContinuity = null;
        if (facts.IsStas)
        {
            optionalDataContinuity = true;

            if (facts.InputOptionalDataFingerprints.Count > 0 && facts.OutputOptionalDataFingerprints.Count == 0)
            {
                optionalDataContinuity = false;
            }
            else
            {
                foreach (var fingerprint in facts.OutputOptionalDataFingerprints)
                {
                    if (!facts.InputOptionalDataFingerprints.Contains(fingerprint, StringComparer.Ordinal))
                    {
                        optionalDataContinuity = false;
                        break;
                    }
                }
            }
        }

        return new DstasDerivedSemantics(
            eventType,
            facts.DstasSpendingType,
            facts.FirstInputFrozen,
            facts.FirstOutputFrozen,
            optionalDataContinuity,
            isRedeem,
            isRedeem ? facts.RedeemAddress : null,
            redeemUsesRegularSpending,
            redeemBlockedByState,
            redeemByIssuerOwner);
    }
}
