using Dxs.Bsv.Tokens.Dstas.Models;
using Dxs.Bsv.Tokens.Dstas.Validation;

namespace Dxs.Bsv.Tests.Dstas.Validation;

public class DstasOptionalDataPolicyTests
{
    private readonly DstasOptionalDataPolicy _sut = new();

    [Fact]
    public void Derive_FailsWhenOutputFingerprintIsNotPresentOnInput()
    {
        var result = _sut.Derive(new DstasLineageFacts(
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
            DstasSpendingType: DstasSpendingTypes.Regular,
            InputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal) { "in-a" },
            OutputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal) { "out-b" }));

        Assert.False(result);
    }
}
