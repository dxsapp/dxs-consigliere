using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Tokens;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;
using Microsoft.AspNetCore.Mvc;
using Raven.Client.Documents;
using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Dstas.RootedHistory;

public class RootedDstasTokenReadSurfaceTests : RavenTestDriver
{
    static RootedDstasTokenReadSurfaceTests()
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
    public async Task Controller_ReadsExposeOnlyTrustedRootBranch()
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

        var controller = new TokenController();
        var readiness = new TrackedEntityReadinessService(store);
        var reader = new TokenProjectionReader(store);
        var rebuilder = new TokenProjectionRebuilder(store, new Dxs.Consigliere.Data.Journal.RavenObservationJournalReader(store), new Dxs.Consigliere.Data.Addresses.AddressProjectionRebuilder(store, new Dxs.Consigliere.Data.Journal.RavenObservationJournalReader(store)));

        var stateResult = await controller.GetState(RootedDstasTestHarness.TokenId, new TestNetworkProvider(), readiness, reader, rebuilder, CancellationToken.None);
        var historyResult = await controller.GetHistory(RootedDstasTestHarness.TokenId, 0, 100, true, false, new TestNetworkProvider(), readiness, reader, rebuilder, CancellationToken.None);

        var state = Assert.IsType<TokenStateResponse>(Assert.IsType<OkObjectResult>(stateResult).Value);
        var history = Assert.IsType<TokenHistoryResponse>(Assert.IsType<OkObjectResult>(historyResult).Value);

        Assert.Equal(TokenProjectionProtocolType.Dstas, state.ProtocolType);
        Assert.Equal(TokenProjectionValidationStatus.Unknown, state.ValidationStatus);
        Assert.Equal([RootedDstasTestHarness.UnfreezeTxId, RootedDstasTestHarness.FreezeTxId, RootedDstasTestHarness.TransferTxId, RootedDstasTestHarness.IssueTxId], history.History.Select(x => x.TxId).ToArray());
        Assert.DoesNotContain(history.History, x => x.TxId is RootedDstasTestHarness.UnknownIssueTxId or RootedDstasTestHarness.UnknownTransferTxId or RootedDstasTestHarness.UnknownFreezeTxId);
        Assert.NotNull(history.HistoryStatus.RootedToken);
        Assert.Equal([RootedDstasTestHarness.IssueTxId], history.HistoryStatus.RootedToken.TrustedRoots);
        Assert.True(history.HistoryStatus.RootedToken.RootedHistorySecure);
    }

    private sealed class TestNetworkProvider : INetworkProvider
    {
        public Dxs.Bsv.Network Network => Dxs.Bsv.Network.Mainnet;
    }
}
