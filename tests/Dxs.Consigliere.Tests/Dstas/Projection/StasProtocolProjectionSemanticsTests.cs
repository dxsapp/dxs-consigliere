using Dxs.Bsv.Script;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Tokens.Dstas;

namespace Dxs.Consigliere.Tests.Dstas.Projection;

public class StasProtocolProjectionSemanticsTests
{
    [Fact]
    public void GetProtocolType_TreatsDstasIssueRootAsDstas()
    {
        var transaction = new MetaTransaction
        {
            IsStas = true,
            Outputs =
            [
                new MetaTransaction.Output
                {
                    Type = ScriptType.DSTAS,
                    TokenId = "token-1",
                    Address = "1Holder"
                }
            ]
        };

        var protocolType = StasProtocolProjectionSemantics.GetProtocolType(transaction);

        Assert.Equal(TokenProjectionProtocolType.Dstas, protocolType);
    }

    [Fact]
    public void GetProtocolType_PrefersPreparedDerivedField()
    {
        var transaction = new MetaTransaction
        {
            StasProtocolType = TokenProjectionProtocolType.Stas,
            Outputs =
            [
                new MetaTransaction.Output
                {
                    Type = ScriptType.DSTAS,
                    TokenId = "token-1",
                    Address = "1Holder"
                }
            ]
        };

        Assert.Equal(TokenProjectionProtocolType.Stas, StasProtocolProjectionSemantics.GetProtocolType(transaction));
    }

    [Fact]
    public void GetValidationStatus_PrefersPreparedDerivedField()
    {
        var transaction = new MetaTransaction
        {
            IsIssue = false,
            IllegalRoots = [],
            AllStasInputsKnown = true,
            StasValidationStatus = TokenProjectionValidationStatus.Unknown
        };

        Assert.Equal(TokenProjectionValidationStatus.Unknown, StasProtocolProjectionSemantics.GetValidationStatus(transaction));
    }

    [Fact]
    public void ShouldProjectOutput_AllowsDstasForValidIssueAndKnownInputs()
    {
        var output = new MetaOutput
        {
            Type = ScriptType.DSTAS,
            Address = "1Holder"
        };

        var validIssue = new MetaTransaction
        {
            IsIssue = true,
            IsValidIssue = true,
            IllegalRoots = [],
            AllStasInputsKnown = false
        };

        var knownInputs = new MetaTransaction
        {
            IsValidIssue = false,
            IllegalRoots = [],
            AllStasInputsKnown = true
        };

        Assert.True(StasProtocolProjectionSemantics.ShouldProjectOutput(validIssue, output));
        Assert.True(StasProtocolProjectionSemantics.ShouldProjectOutput(knownInputs, output));
    }

    [Fact]
    public void ShouldProjectOutput_UsesPreparedProjectionGateForTokenOutputs()
    {
        var output = new MetaOutput
        {
            Type = ScriptType.DSTAS,
            Address = "1Holder"
        };

        var transaction = new MetaTransaction
        {
            CanProjectTokenOutputs = false,
            IsValidIssue = true,
            IllegalRoots = [],
            AllStasInputsKnown = true
        };

        Assert.False(StasProtocolProjectionSemantics.ShouldProjectOutput(transaction, output));
    }
}
