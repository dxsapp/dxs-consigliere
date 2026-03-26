using System.Reflection;

using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Services.Impl;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class TransactionStoreQueryContractTests
{
    private static readonly string Query = GetUpdateStasAttributesQuery();

    [Fact]
    public void Query_PersistsPreparedDerivedFieldsFromArgs()
    {
        Assert.Contains($"this.{nameof(MetaTransaction.IsStas)} = args.{nameof(MetaTransaction.IsStas)};", Query, StringComparison.Ordinal);
        Assert.Contains($"this.{nameof(MetaTransaction.IsIssue)} = args.{nameof(MetaTransaction.IsIssue)};", Query, StringComparison.Ordinal);
        Assert.Contains($"this.{nameof(MetaTransaction.IsValidIssue)} = args.{nameof(MetaTransaction.IsValidIssue)};", Query, StringComparison.Ordinal);
        Assert.Contains($"this.{nameof(MetaTransaction.IsRedeem)} = args.{nameof(MetaTransaction.IsRedeem)};", Query, StringComparison.Ordinal);
        Assert.Contains($"this.{nameof(MetaTransaction.DstasEventType)} = args.{nameof(MetaTransaction.DstasEventType)};", Query, StringComparison.Ordinal);
        Assert.Contains($"this.{nameof(MetaTransaction.DstasSpendingType)} = args.{nameof(MetaTransaction.DstasSpendingType)};", Query, StringComparison.Ordinal);
        Assert.Contains($"this.{nameof(MetaTransaction.StasProtocolType)} = args.{nameof(MetaTransaction.StasProtocolType)};", Query, StringComparison.Ordinal);
        Assert.Contains($"this.{nameof(MetaTransaction.StasValidationStatus)} = args.{nameof(MetaTransaction.StasValidationStatus)};", Query, StringComparison.Ordinal);
        Assert.Contains($"this.{nameof(MetaTransaction.CanProjectTokenOutputs)} = args.{nameof(MetaTransaction.CanProjectTokenOutputs)};", Query, StringComparison.Ordinal);
        Assert.Contains($"this.{nameof(MetaTransaction.TokenIds)} = args.{nameof(MetaTransaction.TokenIds)};", Query, StringComparison.Ordinal);
        Assert.Contains($"this.{nameof(MetaTransaction.IllegalRoots)} = args.{nameof(MetaTransaction.IllegalRoots)};", Query, StringComparison.Ordinal);
        Assert.Contains($"this.{nameof(MetaTransaction.MissingTransactions)} = args.{nameof(MetaTransaction.MissingTransactions)};", Query, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_NoLongerContainsDstasSemanticBranching()
    {
        Assert.DoesNotContain("redeemBlockedByState", Query, StringComparison.Ordinal);
        Assert.DoesNotContain("redeemUsesRegularSpending", Query, StringComparison.Ordinal);
        Assert.DoesNotContain("optionalDataContinuity", Query, StringComparison.Ordinal);
        Assert.DoesNotContain("dstasEventSwapCancel", Query, StringComparison.Ordinal);
        Assert.DoesNotContain("dstasSpendingType ===", Query, StringComparison.Ordinal);
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
