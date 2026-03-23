using System.Reflection;

using Dxs.Consigliere.Services.Impl;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class TransactionStoreQueryContractTests
{
    private static readonly string Query = GetUpdateStasAttributesQuery();

    [Fact]
    public void Query_UsesDstasAndP2MpkhTypes()
    {
        Assert.Contains("var stasType2 = 'DSTAS';", Query, StringComparison.Ordinal);
        Assert.Contains("var p2mpkhType = 'P2MPKH';", Query, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_BlocksRedeemWhenFrozenOrConfiscationMarked()
    {
        Assert.Contains("redeemBlockedByState", Query, StringComparison.Ordinal);
        Assert.Contains("firstInputFrozen === true || firstInputActionType === 'confiscation'", Query, StringComparison.Ordinal);
        Assert.Contains("!redeemBlockedByState", Query, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_RequiresRegularSpendingTypeForRedeem()
    {
        Assert.Contains("redeemUsesRegularSpending", Query, StringComparison.Ordinal);
        Assert.Contains("dstasSpendingType === null || dstasSpendingType === 1", Query, StringComparison.Ordinal);
        Assert.Contains("redeemUsesRegularSpending", Query, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_MapsDstasEventTypesFromSpendingType()
    {
        Assert.Contains("dstasSpendingType === 4", Query, StringComparison.Ordinal);
        Assert.Contains("dstasEventType = 'swap_cancel'", Query, StringComparison.Ordinal);

        Assert.Contains("dstasSpendingType === 3", Query, StringComparison.Ordinal);
        Assert.Contains("dstasEventType = 'confiscation'", Query, StringComparison.Ordinal);

        Assert.Contains("dstasSpendingType === 2", Query, StringComparison.Ordinal);
        Assert.Contains("dstasEventType = 'unfreeze'", Query, StringComparison.Ordinal);
        Assert.Contains("dstasEventType = 'freeze'", Query, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_ContainsOptionalDataContinuityChecks()
    {
        Assert.Contains("optionalDataContinuity", Query, StringComparison.Ordinal);
        Assert.Contains("inputOptionalDataFingerprints", Query, StringComparison.Ordinal);
        Assert.Contains("outputOptionalDataFingerprints", Query, StringComparison.Ordinal);
        Assert.Contains("optionalDataContinuity = false", Query, StringComparison.Ordinal);
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
