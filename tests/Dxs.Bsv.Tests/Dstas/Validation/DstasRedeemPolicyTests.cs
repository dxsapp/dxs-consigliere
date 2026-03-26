using Dxs.Bsv.Tokens.Dstas.Models;
using Dxs.Bsv.Tokens.Dstas.Validation;

namespace Dxs.Bsv.Tests.Dstas.Validation;

public class DstasRedeemPolicyTests
{
    private readonly DstasRedeemPolicy _sut = new();

    [Fact]
    public void Derive_AllowsIssuerOwnedRegularRedeem()
    {
        var result = _sut.Derive(new DstasLineageFacts(
            IsStas: true,
            AllInputsKnown: true,
            StasInputsCount: 1,
            FirstOutputIsRedeemType: true,
            RedeemAddress: "1Issuer",
            StasFrom: "1Issuer",
            FirstInputTokenId: "token-1",
            FirstOutputHash160: "token-1",
            FirstInputFrozen: false,
            FirstOutputFrozen: false,
            FirstInputActionType: DstasActionTypes.Empty,
            DstasSpendingType: DstasSpendingTypes.Regular,
            InputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal),
            OutputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal)));

        Assert.True(result.IsRedeem);
        Assert.Equal("1Issuer", result.RedeemAddress);
        Assert.True(result.UsesRegularSpending);
        Assert.False(result.RedeemBlockedByState);
        Assert.True(result.RedeemByIssuerOwner);
    }
}
