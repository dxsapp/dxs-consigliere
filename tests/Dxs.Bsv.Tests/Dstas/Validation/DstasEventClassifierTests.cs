using Dxs.Bsv.Tokens.Dstas.Models;
using Dxs.Bsv.Tokens.Dstas.Validation;

namespace Dxs.Bsv.Tests.Dstas.Validation;

public class DstasEventClassifierTests
{
    private readonly DstasEventClassifier _sut = new();

    [Fact]
    public void Classify_MapsSwapCancelAndFreezeToggle()
    {
        var swapCancel = _sut.Classify(new DstasLineageFacts(
            IsStas: true,
            AllInputsKnown: true,
            StasInputsCount: 1,
            FirstOutputIsRedeemType: false,
            RedeemAddress: null,
            StasFrom: "1Owner",
            FirstInputTokenId: "token-1",
            FirstOutputHash160: null,
            FirstInputFrozen: false,
            FirstOutputFrozen: false,
            FirstInputActionType: DstasActionTypes.Empty,
            DstasSpendingType: DstasSpendingTypes.SwapCancel,
            InputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal),
            OutputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal)));

        var freeze = _sut.Classify(new DstasLineageFacts(
            IsStas: true,
            AllInputsKnown: true,
            StasInputsCount: 1,
            FirstOutputIsRedeemType: false,
            RedeemAddress: null,
            StasFrom: "1Owner",
            FirstInputTokenId: "token-1",
            FirstOutputHash160: null,
            FirstInputFrozen: false,
            FirstOutputFrozen: true,
            FirstInputActionType: DstasActionTypes.Empty,
            DstasSpendingType: DstasSpendingTypes.FreezeToggle,
            InputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal),
            OutputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal)));

        Assert.Equal(DstasEventTypes.SwapCancel, swapCancel);
        Assert.Equal(DstasEventTypes.Freeze, freeze);
    }
}
