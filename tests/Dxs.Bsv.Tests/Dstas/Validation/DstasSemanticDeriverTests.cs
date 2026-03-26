using Dxs.Bsv.Tokens.Dstas.Models;
using Dxs.Bsv.Tokens.Dstas.Validation;

namespace Dxs.Bsv.Tests.Dstas.Validation;

public class DstasSemanticDeriverTests
{
    private readonly DstasSemanticDeriver _sut = new();

    [Fact]
    public void Derive_MapsSwapFromRegularSpendOfSwapMarkedInput()
    {
        var result = _sut.Derive(new DstasLineageFacts(
            IsStas: true,
            AllInputsKnown: true,
            StasInputsCount: 1,
            FirstOutputIsRedeemType: false,
            RedeemAddress: null,
            StasFrom: "1SwapOwner",
            FirstInputTokenId: "token-1",
            FirstOutputHash160: null,
            FirstInputFrozen: false,
            FirstOutputFrozen: false,
            FirstInputActionType: DstasActionTypes.Swap,
            DstasSpendingType: DstasSpendingTypes.Regular,
            InputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal) { "fp-1" },
            OutputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal) { "fp-1" }));

        Assert.Equal(DstasEventTypes.Swap, result.EventType);
        Assert.Equal(DstasSpendingTypes.Regular, result.SpendingType);
        Assert.True(result.OptionalDataContinuity);
        Assert.False(result.IsRedeem);
        Assert.True(result.UsesRegularSpending);
    }

    [Fact]
    public void Derive_MapsRedeemGuardsWhenFrozenOrConfiscated()
    {
        var result = _sut.Derive(new DstasLineageFacts(
            IsStas: true,
            AllInputsKnown: true,
            StasInputsCount: 1,
            FirstOutputIsRedeemType: true,
            RedeemAddress: "1IssuerRedeem",
            StasFrom: "1IssuerRedeem",
            FirstInputTokenId: "token-1",
            FirstOutputHash160: "token-1",
            FirstInputFrozen: true,
            FirstOutputFrozen: false,
            FirstInputActionType: DstasActionTypes.Freeze,
            DstasSpendingType: DstasSpendingTypes.Regular,
            InputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal),
            OutputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal)));

        Assert.False(result.IsRedeem);
        Assert.True(result.RedeemBlockedByState);
        Assert.True(result.RedeemByIssuerOwner);
        Assert.True(result.UsesRegularSpending);
    }
}
