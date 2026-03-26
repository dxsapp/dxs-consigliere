using System.Reflection;

using Dxs.Consigliere.Services.Impl;

namespace Dxs.Consigliere.Tests.Dstas.Persistence;

public class TransactionStoreDstasPatchParityTests
{
    private static readonly string Query = GetUpdateStasAttributesQuery();

    [Fact]
    public void Query_AssignsPreparedDerivedFieldsFromArgs()
    {
        Assert.Contains("this.IsStas = args.IsStas;", Query, StringComparison.Ordinal);
        Assert.Contains("this.IsIssue = args.IsIssue;", Query, StringComparison.Ordinal);
        Assert.Contains("this.IsValidIssue = args.IsValidIssue;", Query, StringComparison.Ordinal);
        Assert.Contains("this.IsRedeem = args.IsRedeem;", Query, StringComparison.Ordinal);
        Assert.Contains("this.IsWithFee = args.IsWithFee;", Query, StringComparison.Ordinal);
        Assert.Contains("this.IsWithNote = args.IsWithNote;", Query, StringComparison.Ordinal);
        Assert.Contains("this.AllStasInputsKnown = args.AllStasInputsKnown;", Query, StringComparison.Ordinal);
        Assert.Contains("this.DstasEventType = args.DstasEventType;", Query, StringComparison.Ordinal);
        Assert.Contains("this.StasProtocolType = args.StasProtocolType;", Query, StringComparison.Ordinal);
        Assert.Contains("this.StasValidationStatus = args.StasValidationStatus;", Query, StringComparison.Ordinal);
        Assert.Contains("this.CanProjectTokenOutputs = args.CanProjectTokenOutputs;", Query, StringComparison.Ordinal);
        Assert.Contains("this.TokenIds = args.TokenIds;", Query, StringComparison.Ordinal);
        Assert.Contains("this.IllegalRoots = args.IllegalRoots;", Query, StringComparison.Ordinal);
        Assert.Contains("this.MissingTransactions = args.MissingTransactions;", Query, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_DoesNotClassifyProtocolSemanticsInline()
    {
        Assert.DoesNotContain("var stasInputsCount", Query, StringComparison.Ordinal);
        Assert.DoesNotContain("var dstasEventSwap", Query, StringComparison.Ordinal);
        Assert.DoesNotContain("load(inputTxId)", Query, StringComparison.Ordinal);
        Assert.DoesNotContain("redeemUsesRegularSpending", Query, StringComparison.Ordinal);
        Assert.DoesNotContain("optionalDataContinuity", Query, StringComparison.Ordinal);
    }

    private static string GetUpdateStasAttributesQuery()
    {
        var field = typeof(TransactionStore)
            .GetField("UpdateStasAttributesQuery", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        var value = field!.GetValue(null) as string;
        Assert.False(string.IsNullOrWhiteSpace(value));
        return value!;
    }
}
