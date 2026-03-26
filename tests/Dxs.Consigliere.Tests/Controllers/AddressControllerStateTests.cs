using Dxs.Bsv;
using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Dto.Responses.Readiness;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;

using Microsoft.AspNetCore.Mvc;

using Raven.Embedded;
using Raven.Client.Documents;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Controllers;

public class AddressControllerStateTests : RavenTestDriver
{
    static AddressControllerStateTests()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }

    private const string Address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    private const string TokenId = "1111111111111111111111111111111111111111";

    [Fact]
    public async Task GetBalances_ReturnsNotTrackedConflict_ForUntrackedAddress()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var controller = new AddressController();

        var result = await controller.GetBalances(
            Address,
            [],
            new TestNetworkProvider(),
            new TrackedEntityReadinessService(store),
            new AddressProjectionReader(store),
            new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store)),
            CancellationToken.None
        );

        var conflict = Assert.IsType<ObjectResult>(result);
        var payload = Assert.IsType<TrackedEntityReadinessGateResponse>(conflict.Value);
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal("not_tracked", payload.Code);
    }

    [Fact]
    public async Task GetState_ReturnsProjectionBackedBalancesAndUtxos()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTrackedAddressAsync(store, readable: true);
        await SeedTrackedTokenAsync(store, readable: true);
        await SeedAddressProjectionAsync(store);

        var controller = new AddressController();

        var result = await controller.GetState(
            Address,
            [TokenId],
            new TestNetworkProvider(),
            new TrackedEntityReadinessService(store),
            new AddressProjectionReader(store),
            new AddressProjectionRebuilder(store, new RavenObservationJournalReader(store)),
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AddressStateResponse>(ok.Value);
        Assert.Equal(Address, payload.Address);
        Assert.Equal(2, payload.Balances.Length);
        Assert.Equal(2, payload.UtxoSet.Length);
    }

    private static async Task SeedTrackedAddressAsync(IDocumentStore store, bool readable)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new TrackedAddressStatusDocument
        {
            Id = TrackedAddressStatusDocument.GetId(Address),
            EntityType = TrackedEntityType.Address,
            EntityId = Address,
            Address = Address,
            Tracked = true,
            LifecycleStatus = readable ? TrackedEntityLifecycleStatus.Live : TrackedEntityLifecycleStatus.CatchingUp,
            Readable = readable,
            Authoritative = readable,
            Degraded = false
        });
        await session.SaveChangesAsync();
    }

    private static async Task SeedTrackedTokenAsync(IDocumentStore store, bool readable)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new TrackedTokenStatusDocument
        {
            Id = TrackedTokenStatusDocument.GetId(TokenId),
            EntityType = TrackedEntityType.Token,
            EntityId = TokenId,
            TokenId = TokenId,
            Tracked = true,
            LifecycleStatus = readable ? TrackedEntityLifecycleStatus.Live : TrackedEntityLifecycleStatus.CatchingUp,
            Readable = readable,
            Authoritative = readable,
            Degraded = false
        });
        await session.SaveChangesAsync();
    }

    private static async Task SeedAddressProjectionAsync(IDocumentStore store)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new AddressBalanceProjectionDocument
        {
            Id = AddressBalanceProjectionDocument.GetId(Address, null),
            Address = Address,
            TokenId = null,
            Satoshis = 10
        });
        await session.StoreAsync(new AddressBalanceProjectionDocument
        {
            Id = AddressBalanceProjectionDocument.GetId(Address, TokenId),
            Address = Address,
            TokenId = TokenId,
            Satoshis = 5
        });
        await session.StoreAsync(new AddressUtxoProjectionDocument
        {
            Id = AddressUtxoProjectionDocument.GetId("tx-bsv", 0),
            TxId = "tx-bsv",
            Vout = 0,
            Address = Address,
            Satoshis = 10,
            ScriptPubKey = "script-bsv",
            ScriptType = Dxs.Bsv.Script.ScriptType.P2PKH
        });
        await session.StoreAsync(new AddressUtxoProjectionDocument
        {
            Id = AddressUtxoProjectionDocument.GetId("tx-token", 1),
            TxId = "tx-token",
            Vout = 1,
            Address = Address,
            TokenId = TokenId,
            Satoshis = 5,
            ScriptPubKey = "script-token",
            ScriptType = Dxs.Bsv.Script.ScriptType.P2STAS
        });
        await session.SaveChangesAsync();
    }

    private sealed class TestNetworkProvider : INetworkProvider
    {
        public Network Network => Network.Mainnet;
    }
}
