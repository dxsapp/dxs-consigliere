#nullable enable
using System;

using Dxs.Bsv.Tokens.Dstas.Models;

namespace Dxs.Bsv.Tokens.Dstas.Validation;

public sealed class DstasSemanticDeriver
{
    private readonly DstasRedeemPolicy _redeemPolicy = new();
    private readonly DstasEventClassifier _eventClassifier = new();
    private readonly DstasOptionalDataPolicy _optionalDataPolicy = new();

    public DstasDerivedSemantics Derive(DstasLineageFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var redeem = _redeemPolicy.Derive(facts);
        var eventType = _eventClassifier.Classify(facts);
        var optionalDataContinuity = _optionalDataPolicy.Derive(facts);

        return new DstasDerivedSemantics(
            eventType,
            facts.DstasSpendingType,
            facts.FirstInputFrozen,
            facts.FirstOutputFrozen,
            optionalDataContinuity,
            redeem.IsRedeem,
            redeem.RedeemAddress,
            redeem.UsesRegularSpending,
            redeem.RedeemBlockedByState,
            redeem.RedeemByIssuerOwner);
    }
}
