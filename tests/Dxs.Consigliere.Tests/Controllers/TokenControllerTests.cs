using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Tokens;
using Dxs.Consigliere.Dto;
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

public class TokenControllerTests : RavenTestDriver
{
    static TokenControllerTests()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }

    private const string TokenId = "1111111111111111111111111111111111111111";

    [Fact]
    public async Task GetState_ReturnsProjectionBackedTokenState()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTrackedTokenAsync(store, readable: true);
        await SeedTokenStateAsync(store);

        var controller = new TokenController();

        var result = await controller.GetState(
            TokenId,
            new TestNetworkProvider(),
            new TrackedEntityReadinessService(store),
            new TokenProjectionReader(store),
            BuildTokenRebuilder(store),
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<TokenStateResponse>(ok.Value);
        Assert.Equal("stas", payload.ProtocolType);
        Assert.Equal("valid", payload.ValidationStatus);
        Assert.Equal(500, payload.TotalKnownSupply);
    }

    [Fact]
    public async Task GetHistory_ReturnsNotTrackedConflict_ForUntrackedToken()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var controller = new TokenController();

        var result = await controller.GetHistory(
            TokenId,
            0,
            100,
            true,
            false,
            new TestNetworkProvider(),
            new TrackedEntityReadinessService(store),
            new TokenProjectionReader(store),
            BuildTokenRebuilder(store),
            CancellationToken.None
        );

        var conflict = Assert.IsType<ObjectResult>(result);
        var payload = Assert.IsType<TrackedEntityReadinessGateResponse>(conflict.Value);
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal("not_tracked", payload.Code);
    }

    [Fact]
    public async Task GetBalances_GroupsTokenUtxosByAddress()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTrackedTokenAsync(store, readable: true);
        await SeedTokenBalancesAsync(store);

        var controller = new TokenController();

        var result = await controller.GetBalances(
            TokenId,
            new TestNetworkProvider(),
            new TrackedEntityReadinessService(store),
            new TokenProjectionReader(store),
            BuildTokenRebuilder(store),
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<BalanceDto[]>(ok.Value);
        Assert.Equal(2, payload.Length);
        Assert.Equal(12, payload[0].Satoshis);
        Assert.Equal(11, payload[1].Satoshis);
    }

    private static TokenProjectionRebuilder BuildTokenRebuilder(IDocumentStore store)
    {
        var journalReader = new RavenObservationJournalReader(store);
        var addressRebuilder = new AddressProjectionRebuilder(store, journalReader);
        return new TokenProjectionRebuilder(store, journalReader, addressRebuilder);
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

    private static async Task SeedTokenStateAsync(IDocumentStore store)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new TokenStateProjectionDocument
        {
            Id = TokenStateProjectionDocument.GetId(TokenId),
            TokenId = TokenId,
            ProtocolType = "stas",
            ProtocolVersion = "v2",
            IssuanceKnown = true,
            ValidationStatus = TokenProjectionValidationStatus.Valid,
            Issuer = "issuer-address",
            TotalKnownSupply = 500,
            BurnedSatoshis = 5,
            LastIndexedHeight = 123
        });
        await session.SaveChangesAsync();
    }

    private static async Task SeedTokenBalancesAsync(IDocumentStore store)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new AddressUtxoProjectionDocument
        {
            Id = AddressUtxoProjectionDocument.GetId("tx-1", 0),
            TxId = "tx-1",
            Vout = 0,
            Address = "addr-1",
            TokenId = TokenId,
            Satoshis = 5,
            ScriptPubKey = "script-1",
            ScriptType = Dxs.Bsv.Script.ScriptType.P2STAS
        });
        await session.StoreAsync(new AddressUtxoProjectionDocument
        {
            Id = AddressUtxoProjectionDocument.GetId("tx-2", 1),
            TxId = "tx-2",
            Vout = 1,
            Address = "addr-1",
            TokenId = TokenId,
            Satoshis = 7,
            ScriptPubKey = "script-2",
            ScriptType = Dxs.Bsv.Script.ScriptType.P2STAS
        });
        await session.StoreAsync(new AddressUtxoProjectionDocument
        {
            Id = AddressUtxoProjectionDocument.GetId("tx-3", 0),
            TxId = "tx-3",
            Vout = 0,
            Address = "addr-2",
            TokenId = TokenId,
            Satoshis = 11,
            ScriptPubKey = "script-3",
            ScriptType = Dxs.Bsv.Script.ScriptType.P2STAS
        });
        await session.StoreAsync(new AddressBalanceProjectionDocument
        {
            Id = AddressBalanceProjectionDocument.GetId("addr-1", TokenId),
            Address = "addr-1",
            TokenId = TokenId,
            Satoshis = 12,
            LastSequence = 1
        });
        await session.StoreAsync(new AddressBalanceProjectionDocument
        {
            Id = AddressBalanceProjectionDocument.GetId("addr-2", TokenId),
            Address = "addr-2",
            TokenId = TokenId,
            Satoshis = 11,
            LastSequence = 1
        });
        await session.SaveChangesAsync();
    }

    private sealed class TestNetworkProvider : INetworkProvider
    {
        public Dxs.Bsv.Network Network => Dxs.Bsv.Network.Mainnet;
    }
}
