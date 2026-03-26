using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Transactions;
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
    private const string TrustedRoot = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string UnknownRoot = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

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

    [Fact]
    public async Task RootedFullHistory_FiltersUnknownRootBranchFromOutwardReads()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedTrackedTokenAsync(store, readable: true, fullHistory: true, trustedRoots: [TrustedRoot]);
        await SeedRootedTokenBranchAsync(store);

        var controller = new TokenController();
        var readiness = new TrackedEntityReadinessService(store);
        var reader = new TokenProjectionReader(store);
        var rebuilder = BuildTokenRebuilder(store);

        var stateResult = await controller.GetState(
            TokenId,
            new TestNetworkProvider(),
            readiness,
            reader,
            rebuilder,
            CancellationToken.None);
        var balancesResult = await controller.GetBalances(
            TokenId,
            new TestNetworkProvider(),
            readiness,
            reader,
            rebuilder,
            CancellationToken.None);
        var historyResult = await controller.GetHistory(
            TokenId,
            0,
            100,
            true,
            false,
            new TestNetworkProvider(),
            readiness,
            reader,
            rebuilder,
            CancellationToken.None);

        var state = Assert.IsType<TokenStateResponse>(Assert.IsType<OkObjectResult>(stateResult).Value);
        Assert.Equal("stas", state.ProtocolType);
        Assert.Equal("unknown", state.ValidationStatus);
        Assert.Equal(40, state.TotalKnownSupply);
        Assert.Equal("issuer-rooted", state.Issuer);

        var balances = Assert.IsType<BalanceDto[]>(Assert.IsType<OkObjectResult>(balancesResult).Value);
        Assert.Single(balances);
        Assert.Equal("root-holder", balances[0].Address);
        Assert.Equal(40, balances[0].Satoshis);

        var history = Assert.IsType<TokenHistoryResponse>(Assert.IsType<OkObjectResult>(historyResult).Value);
        Assert.Equal(2, history.TotalCount);
        Assert.Equal(["tx-rooted-transfer", TrustedRoot], history.History.Select(x => x.TxId).ToArray());
        Assert.NotNull(history.HistoryStatus.RootedToken);
        Assert.Equal([TrustedRoot], history.HistoryStatus.RootedToken.TrustedRoots);
        Assert.True(history.HistoryStatus.RootedToken.RootedHistorySecure);
        Assert.Equal(1, history.HistoryStatus.RootedToken.CompletedTrustedRootCount);
    }

    private static TokenProjectionRebuilder BuildTokenRebuilder(IDocumentStore store)
    {
        var journalReader = new RavenObservationJournalReader(store);
        var addressRebuilder = new AddressProjectionRebuilder(store, journalReader);
        return new TokenProjectionRebuilder(store, journalReader, addressRebuilder);
    }

    private static async Task SeedTrackedTokenAsync(
        IDocumentStore store,
        bool readable,
        bool fullHistory = false,
        string[]? trustedRoots = null)
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
            Degraded = false,
            HistoryMode = fullHistory ? TrackedEntityHistoryMode.FullHistory : TrackedEntityHistoryMode.ForwardOnly,
            HistoryReadiness = fullHistory ? TrackedEntityHistoryReadiness.FullHistoryLive : TrackedEntityHistoryReadiness.ForwardLive,
            HistoryCoverage = new TrackedHistoryCoverage
            {
                Mode = fullHistory ? TrackedEntityHistoryMode.FullHistory : TrackedEntityHistoryMode.ForwardOnly,
                FullCoverage = fullHistory,
                AuthoritativeFromBlockHeight = 123,
                AuthoritativeFromObservedAt = DateTimeOffset.Parse("2026-03-26T18:10:00Z").ToUnixTimeMilliseconds()
            },
            HistorySecurity = new TrackedTokenHistorySecurityState
            {
                TrustedRoots = trustedRoots ?? [],
                CompletedTrustedRootCount = (trustedRoots ?? []).Length,
                RootedHistorySecure = fullHistory && (trustedRoots?.Length ?? 0) > 0,
                BlockingUnknownRoot = false
            }
        });
        await session.StoreAsync(new TrackedTokenDocument
        {
            Id = TrackedTokenDocument.GetId(TokenId),
            EntityType = TrackedEntityType.Token,
            EntityId = TokenId,
            TokenId = TokenId,
            Symbol = "TST",
            Tracked = true,
            LifecycleStatus = readable ? TrackedEntityLifecycleStatus.Live : TrackedEntityLifecycleStatus.CatchingUp,
            Readable = readable,
            Authoritative = readable,
            Degraded = false,
            HistoryMode = fullHistory ? TrackedEntityHistoryMode.FullHistory : TrackedEntityHistoryMode.ForwardOnly,
            HistoryReadiness = fullHistory ? TrackedEntityHistoryReadiness.FullHistoryLive : TrackedEntityHistoryReadiness.ForwardLive,
            HistoryCoverage = new TrackedHistoryCoverage
            {
                Mode = fullHistory ? TrackedEntityHistoryMode.FullHistory : TrackedEntityHistoryMode.ForwardOnly,
                FullCoverage = fullHistory,
                AuthoritativeFromBlockHeight = 123,
                AuthoritativeFromObservedAt = DateTimeOffset.Parse("2026-03-26T18:10:00Z").ToUnixTimeMilliseconds()
            },
            HistorySecurity = new TrackedTokenHistorySecurityState
            {
                TrustedRoots = trustedRoots ?? [],
                CompletedTrustedRootCount = (trustedRoots ?? []).Length,
                RootedHistorySecure = fullHistory && (trustedRoots?.Length ?? 0) > 0,
                BlockingUnknownRoot = false
            }
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

    private static async Task SeedRootedTokenBranchAsync(IDocumentStore store)
    {
        using var session = store.OpenAsyncSession();

        var rootedIssue = new MetaTransaction
        {
            Id = TrustedRoot,
            Height = 100,
            Index = 0,
            Timestamp = 1_710_000_100,
            IsStas = true,
            IsIssue = true,
            IsValidIssue = true,
            RedeemAddress = "issuer-rooted",
            Inputs = [],
            Outputs =
            [
                new MetaTransaction.Output
                {
                    Id = MetaOutput.GetId(TrustedRoot, 0),
                    Address = "root-holder",
                    TokenId = TokenId,
                    Satoshis = 40,
                    Type = Dxs.Bsv.Script.ScriptType.P2STAS
                }
            ],
            Addresses = ["root-holder", "issuer-rooted"],
            TokenIds = [TokenId],
            IllegalRoots = [],
            MissingTransactions = [],
            AllStasInputsKnown = true
        };
        var rootedTransfer = new MetaTransaction
        {
            Id = "tx-rooted-transfer",
            Height = 101,
            Index = 0,
            Timestamp = 1_710_000_101,
            IsStas = true,
            Inputs = [new MetaTransaction.Input { TxId = TrustedRoot, Vout = 0, Id = MetaOutput.GetId(TrustedRoot, 0) }],
            Outputs =
            [
                new MetaTransaction.Output
                {
                    Id = MetaOutput.GetId("tx-rooted-transfer", 0),
                    Address = "root-holder",
                    TokenId = TokenId,
                    Satoshis = 40,
                    Type = Dxs.Bsv.Script.ScriptType.P2STAS
                }
            ],
            Addresses = ["root-holder"],
            TokenIds = [TokenId],
            IllegalRoots = [],
            MissingTransactions = [],
            AllStasInputsKnown = true
        };
        var unknownIssue = new MetaTransaction
        {
            Id = UnknownRoot,
            Height = 102,
            Index = 0,
            Timestamp = 1_710_000_102,
            IsStas = true,
            IsIssue = true,
            IsValidIssue = true,
            RedeemAddress = "issuer-unknown",
            Inputs = [],
            Outputs =
            [
                new MetaTransaction.Output
                {
                    Id = MetaOutput.GetId(UnknownRoot, 0),
                    Address = "rogue-holder",
                    TokenId = TokenId,
                    Satoshis = 70,
                    Type = Dxs.Bsv.Script.ScriptType.P2STAS
                }
            ],
            Addresses = ["rogue-holder", "issuer-unknown"],
            TokenIds = [TokenId],
            IllegalRoots = [],
            MissingTransactions = [],
            AllStasInputsKnown = true
        };

        await session.StoreAsync(rootedIssue, rootedIssue.Id);
        await session.StoreAsync(rootedTransfer, rootedTransfer.Id);
        await session.StoreAsync(unknownIssue, unknownIssue.Id);

        await session.StoreAsync(new AddressUtxoProjectionDocument
        {
            Id = AddressUtxoProjectionDocument.GetId("tx-rooted-transfer", 0),
            TxId = "tx-rooted-transfer",
            Vout = 0,
            Address = "root-holder",
            TokenId = TokenId,
            Satoshis = 40,
            ScriptPubKey = "script-rooted",
            ScriptType = Dxs.Bsv.Script.ScriptType.P2STAS
        });
        await session.StoreAsync(new AddressUtxoProjectionDocument
        {
            Id = AddressUtxoProjectionDocument.GetId(UnknownRoot, 0),
            TxId = UnknownRoot,
            Vout = 0,
            Address = "rogue-holder",
            TokenId = TokenId,
            Satoshis = 70,
            ScriptPubKey = "script-unknown",
            ScriptType = Dxs.Bsv.Script.ScriptType.P2STAS
        });

        await session.StoreAsync(new TokenHistoryProjectionDocument
        {
            Id = TokenHistoryProjectionDocument.GetId(TokenId, TrustedRoot),
            TokenId = TokenId,
            TxId = TrustedRoot,
            Timestamp = rootedIssue.Timestamp,
            Height = rootedIssue.Height,
            ReceivedSatoshis = 40,
            IsIssue = true,
            ValidationStatus = "valid",
            ProtocolType = "stas"
        });
        await session.StoreAsync(new TokenHistoryProjectionDocument
        {
            Id = TokenHistoryProjectionDocument.GetId(TokenId, "tx-rooted-transfer"),
            TokenId = TokenId,
            TxId = "tx-rooted-transfer",
            Timestamp = rootedTransfer.Timestamp,
            Height = rootedTransfer.Height,
            ReceivedSatoshis = 40,
            ValidationStatus = "valid",
            ProtocolType = "stas"
        });
        await session.StoreAsync(new TokenHistoryProjectionDocument
        {
            Id = TokenHistoryProjectionDocument.GetId(TokenId, UnknownRoot),
            TokenId = TokenId,
            TxId = UnknownRoot,
            Timestamp = unknownIssue.Timestamp,
            Height = unknownIssue.Height,
            ReceivedSatoshis = 70,
            IsIssue = true,
            ValidationStatus = "valid",
            ProtocolType = "stas"
        });

        await session.SaveChangesAsync();
    }

    private sealed class TestNetworkProvider : INetworkProvider
    {
        public Dxs.Bsv.Network Network => Dxs.Bsv.Network.Mainnet;
    }
}
