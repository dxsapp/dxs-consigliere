using Dxs.Bsv.Script;
using Dxs.Bsv.Tokens.Validation;

namespace Dxs.Bsv.Tests.Tokens.Validation;

public class StasDependencyPolicyTests
{
    private readonly StasDependencyPolicy _sut = new();

    [Fact]
    public void Derive_TracksIllegalRootsMissingDependenciesAndFirstStasInput()
    {
        var result = _sut.Derive(new StasLineageTransaction(
            "tx-1",
            [
                new StasLineageInput("missing-parent", 0, Parent: null),
                new StasLineageInput(
                    "bad-issue",
                    0,
                    1,
                    new StasLineageParentTransaction(
                        [new StasLineageOutput(ScriptType.DSTAS, Address: "1Owner", TokenId: "token-1", Hash160: "issuer-hash")],
                        HasMissingDependencies: false,
                        IsIssue: true,
                        IsValidIssue: false,
                        IllegalRoots: Array.Empty<string>()))
            ],
            [new StasLineageOutput(ScriptType.DSTAS, Address: "1Receiver", TokenId: "token-1", Hash160: "receiver-hash")]));

        Assert.False(result.AllInputsKnown);
        Assert.Equal("1Owner", result.StasFrom);
        Assert.Equal("token-1", result.FirstInputTokenId);
        Assert.Equal(1, result.DstasSpendingType);
        Assert.Contains("missing-parent", result.MissingDependencies);
        Assert.Contains("bad-issue", result.IllegalRoots);
        Assert.Equal(new[] { "token-1" }, result.InputTokens);
        Assert.Equal(1, result.StasInputsCount);
    }
}
