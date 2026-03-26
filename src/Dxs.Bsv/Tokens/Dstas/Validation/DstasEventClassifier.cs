#nullable enable
using System;
using Dxs.Bsv.Tokens.Dstas.Models;

namespace Dxs.Bsv.Tokens.Dstas.Validation;

public sealed class DstasEventClassifier
{
    public string? Classify(DstasLineageFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.IsStas && facts.DstasSpendingType is not null)
        {
            if (facts.DstasSpendingType == DstasSpendingTypes.SwapCancel)
                return DstasEventTypes.SwapCancel;

            if (facts.DstasSpendingType == DstasSpendingTypes.Confiscation)
                return DstasEventTypes.Confiscation;

            if (facts.DstasSpendingType == DstasSpendingTypes.FreezeToggle)
            {
                return facts.FirstInputFrozen == true && facts.FirstOutputFrozen == false
                    ? DstasEventTypes.Unfreeze
                    : DstasEventTypes.Freeze;
            }
        }

        if (facts.IsStas
            && facts.DstasSpendingType is null or DstasSpendingTypes.Regular
            && string.Equals(facts.FirstInputActionType, DstasActionTypes.Swap, StringComparison.Ordinal))
        {
            return DstasEventTypes.Swap;
        }

        return null;
    }
}
