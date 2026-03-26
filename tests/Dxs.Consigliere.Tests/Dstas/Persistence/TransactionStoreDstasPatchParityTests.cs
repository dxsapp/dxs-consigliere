using System.Reflection;

using Dxs.Bsv.Tokens.Dstas.Models;
using Dxs.Consigliere.Services.Impl;

namespace Dxs.Consigliere.Tests.Dstas.Persistence;

public class TransactionStoreDstasPatchParityTests
{
    private static readonly string Query = GetUpdateStasAttributesQuery();

    [Fact]
    public void Query_UsesCanonicalDstasActionAndEventConstants()
    {
        Assert.Contains($"var dstasActionSwap = '{DstasActionTypes.Swap}';", Query, StringComparison.Ordinal);
        Assert.Contains($"var dstasActionConfiscation = '{DstasActionTypes.Confiscation}';", Query, StringComparison.Ordinal);
        Assert.Contains($"var dstasEventSwap = '{DstasEventTypes.Swap}';", Query, StringComparison.Ordinal);
        Assert.Contains($"var dstasEventSwapCancel = '{DstasEventTypes.SwapCancel}';", Query, StringComparison.Ordinal);
        Assert.Contains($"var dstasEventConfiscation = '{DstasEventTypes.Confiscation}';", Query, StringComparison.Ordinal);
        Assert.Contains($"var dstasEventFreeze = '{DstasEventTypes.Freeze}';", Query, StringComparison.Ordinal);
        Assert.Contains($"var dstasEventUnfreeze = '{DstasEventTypes.Unfreeze}';", Query, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_MapsCanonicalDstasSpendingTypes()
    {
        Assert.Contains($"dstasSpendingType === {DstasSpendingTypes.SwapCancel}", Query, StringComparison.Ordinal);
        Assert.Contains("dstasEventType = dstasEventSwapCancel", Query, StringComparison.Ordinal);

        Assert.Contains($"dstasSpendingType === {DstasSpendingTypes.Confiscation}", Query, StringComparison.Ordinal);
        Assert.Contains("dstasEventType = dstasEventConfiscation", Query, StringComparison.Ordinal);

        Assert.Contains($"dstasSpendingType === {DstasSpendingTypes.FreezeToggle}", Query, StringComparison.Ordinal);
        Assert.Contains("dstasEventType = dstasEventUnfreeze", Query, StringComparison.Ordinal);
        Assert.Contains("dstasEventType = dstasEventFreeze", Query, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_UsesCanonicalRegularSpendingRulesForRedeemAndSwap()
    {
        Assert.Contains(
            $"var redeemUsesRegularSpending = dstasSpendingType === null || dstasSpendingType === {DstasSpendingTypes.Regular};",
            Query,
            StringComparison.Ordinal);
        Assert.Contains("firstInputActionType === dstasActionSwap", Query, StringComparison.Ordinal);
        Assert.Contains("dstasEventType = dstasEventSwap", Query, StringComparison.Ordinal);
        Assert.Contains("firstInputActionType === dstasActionConfiscation", Query, StringComparison.Ordinal);
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
