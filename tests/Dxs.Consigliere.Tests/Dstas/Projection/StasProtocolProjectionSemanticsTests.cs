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
    public void ShouldProjectOutput_AllowsDstasForValidIssueAndKnownInputs()
    {
        var output = new MetaOutput
        {
            Type = ScriptType.DSTAS,
            Address = "1Holder"
        };

        var validIssue = new MetaTransaction
        {
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
}
