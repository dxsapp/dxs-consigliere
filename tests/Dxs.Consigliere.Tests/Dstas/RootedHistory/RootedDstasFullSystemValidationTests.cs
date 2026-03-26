using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Tests.Shared;
using Raven.Embedded;
using Raven.Client.Documents;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Dstas.RootedHistory;

public class RootedDstasFullSystemValidationTests : RavenTestDriver
{
    static RootedDstasFullSystemValidationTests()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }

    [Fact]
    public async Task TrustedLifecycle_RemainsCanonicalAcrossStateHistoryAndUtxos()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await RootedDstasTestHarness.SeedTrackedScopeAsync(store, [RootedDstasTestHarness.IssueTxId]);
        await RootedDstasTestHarness.SeedTrustedLifecycleAsync(store);
        var stack = RootedDstasTestHarness.BuildProjectionStack(store);
        await RootedDstasTestHarness.AppendTrustedLifecycleAsync(stack.txJournal, stack.txRebuilder, stack.addressRebuilder, stack.tokenRebuilder);

        var state = await stack.tokenReader.LoadStateAsync(RootedDstasTestHarness.TokenId);
        var history = await stack.tokenReader.LoadHistoryAsync(RootedDstasTestHarness.TokenId);
        var balances = await stack.tokenReader.LoadBalancesAsync(RootedDstasTestHarness.TokenId);
        var utxos = await stack.tokenReader.LoadUtxosAsync(RootedDstasTestHarness.TokenId);

        Assert.NotNull(state);
        Assert.Equal(TokenProjectionProtocolType.Dstas, state!.ProtocolType);
        Assert.Equal(TokenProjectionValidationStatus.Valid, state.ValidationStatus);
        Assert.Equal(50, state.TotalKnownSupply);
        Assert.Equal(RootedDstasTestHarness.IssuerAddress, state.Issuer);
        Assert.Equal([RootedDstasTestHarness.UnfreezeTxId, RootedDstasTestHarness.FreezeTxId, RootedDstasTestHarness.TransferTxId, RootedDstasTestHarness.IssueTxId], history.Select(x => x.TxId).ToArray());
        Assert.Single(balances);
        Assert.Equal(RootedDstasTestHarness.HolderAddress, balances[0].Address);
        Assert.Equal(50, balances[0].Satoshis);
        Assert.Single(utxos);
        Assert.Equal(RootedDstasTestHarness.UnfreezeTxId, utxos[0].TxId);
    }

    [Fact]
    public async Task UnknownRootLifecycle_IsExcludedButMarksValidationUnknown()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await RootedDstasTestHarness.SeedTrackedScopeAsync(store, [RootedDstasTestHarness.IssueTxId]);
        await RootedDstasTestHarness.SeedTrustedLifecycleAsync(store);
        await RootedDstasTestHarness.SeedUnknownRootLifecycleAsync(store);
        var stack = RootedDstasTestHarness.BuildProjectionStack(store);
        await RootedDstasTestHarness.AppendTrustedLifecycleAsync(stack.txJournal, stack.txRebuilder, stack.addressRebuilder, stack.tokenRebuilder);
        await RootedDstasTestHarness.AppendUnknownRootLifecycleAsync(stack.txJournal, stack.txRebuilder, stack.addressRebuilder, stack.tokenRebuilder);

        var state = await stack.tokenReader.LoadStateAsync(RootedDstasTestHarness.TokenId);
        var history = await stack.tokenReader.LoadHistoryAsync(RootedDstasTestHarness.TokenId);
        var balances = await stack.tokenReader.LoadBalancesAsync(RootedDstasTestHarness.TokenId);
        var utxos = await stack.tokenReader.LoadUtxosAsync(RootedDstasTestHarness.TokenId);

        Assert.NotNull(state);
        Assert.Equal(TokenProjectionProtocolType.Dstas, state!.ProtocolType);
        Assert.Equal(TokenProjectionValidationStatus.Unknown, state.ValidationStatus);
        Assert.Equal([RootedDstasTestHarness.UnfreezeTxId, RootedDstasTestHarness.FreezeTxId, RootedDstasTestHarness.TransferTxId, RootedDstasTestHarness.IssueTxId], history.Select(x => x.TxId).ToArray());
        Assert.DoesNotContain(history, x => x.TxId is RootedDstasTestHarness.UnknownIssueTxId or RootedDstasTestHarness.UnknownTransferTxId or RootedDstasTestHarness.UnknownFreezeTxId);
        Assert.Single(balances);
        Assert.Equal(RootedDstasTestHarness.HolderAddress, balances[0].Address);
        Assert.Single(utxos);
        Assert.DoesNotContain(utxos, x => x.TxId is RootedDstasTestHarness.UnknownIssueTxId or RootedDstasTestHarness.UnknownTransferTxId or RootedDstasTestHarness.UnknownFreezeTxId);
    }

    [Fact]
    public async Task Replay_RebuildPreservesTrustedCanonicalHistory()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await RootedDstasTestHarness.SeedTrackedScopeAsync(store, [RootedDstasTestHarness.IssueTxId]);
        await RootedDstasTestHarness.SeedTrustedLifecycleAsync(store);
        var stack = RootedDstasTestHarness.BuildProjectionStack(store);
        await RootedDstasTestHarness.AppendTrustedLifecycleAsync(stack.txJournal, stack.txRebuilder, stack.addressRebuilder, stack.tokenRebuilder);

        var firstState = await stack.tokenReader.LoadStateAsync(RootedDstasTestHarness.TokenId);
        var firstHistory = await stack.tokenReader.LoadHistoryAsync(RootedDstasTestHarness.TokenId);
        var firstUtxos = await stack.tokenReader.LoadUtxosAsync(RootedDstasTestHarness.TokenId);

        await stack.txRebuilder.RebuildAsync();
        await stack.addressRebuilder.RebuildAsync();
        await stack.tokenRebuilder.RebuildAsync();

        var replayedState = await stack.tokenReader.LoadStateAsync(RootedDstasTestHarness.TokenId);
        var replayedHistory = await stack.tokenReader.LoadHistoryAsync(RootedDstasTestHarness.TokenId);
        var replayedUtxos = await stack.tokenReader.LoadUtxosAsync(RootedDstasTestHarness.TokenId);

        Assert.Equal(firstState!.ValidationStatus, replayedState!.ValidationStatus);
        Assert.Equal(firstState.TotalKnownSupply, replayedState.TotalKnownSupply);
        Assert.Equal(firstHistory.Select(x => x.TxId).ToArray(), replayedHistory.Select(x => x.TxId).ToArray());
        Assert.Equal(firstUtxos.Select(x => x.TxId).ToArray(), replayedUtxos.Select(x => x.TxId).ToArray());
    }
}
