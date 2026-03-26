using Dxs.Bsv.Tokens.Validation;

namespace Dxs.Bsv.Tests.Tokens.Validation;

public class StasIssuePolicyTests
{
    private readonly StasIssuePolicy _sut = new();

    [Fact]
    public void Derive_DetectsValidIssueFromSingleOutputTokenAndKnownFundingHash()
    {
        var result = _sut.Derive(
            new StasOutputFacts(
                WithNote: false,
                RedeemAddress: null,
                FirstOutputFrozen: false,
                OutputTokens: new[] { "token-1" },
                OutputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal)),
            new StasDependencyFacts(
                WithFee: true,
                AllInputsKnown: true,
                StasFrom: null,
                FirstInputHash160: "token-1",
                FirstInputTokenId: null,
                FirstInputFrozen: null,
                FirstInputActionType: null,
                DstasSpendingType: null,
                StasInputsCount: 0,
                InputTokens: Array.Empty<string>(),
                IllegalRoots: Array.Empty<string>(),
                MissingDependencies: Array.Empty<string>(),
                InputOptionalDataFingerprints: new HashSet<string>(StringComparer.Ordinal)));

        Assert.True(result.IsStas);
        Assert.True(result.IsIssue);
        Assert.True(result.IsValidIssue);
        Assert.Equal(new[] { "token-1" }, result.TokenIds);
    }
}
