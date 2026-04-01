using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Tokens;
using Dxs.Consigliere.Data.Tracking;
using Dxs.Tests.Shared;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Tracking;

public class AdminTrackingQueryServiceIntegrationTests : RavenTestDriver
{
    [Fact]
    public async Task GetTrackedEntitiesAsync_HidesTombstonedByDefault()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var queryService = new AdminTrackingQueryService(store, new TokenProjectionReader(store));

        using (var session = store.OpenAsyncSession())
        {
            await session.StoreAsync(new TrackedAddressDocument
            {
                Id = TrackedAddressDocument.GetId("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa"),
                EntityType = TrackedEntityType.Address,
                EntityId = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
                Address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
                Name = "Genesis",
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = true,
                HistoryReadiness = TrackedEntityHistoryReadiness.ForwardLive,
                CreatedAt = 100,
            });
            await session.StoreAsync(new TrackedAddressStatusDocument
            {
                Id = TrackedAddressStatusDocument.GetId("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa"),
                EntityType = TrackedEntityType.Address,
                EntityId = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
                Address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = true,
                HistoryReadiness = TrackedEntityHistoryReadiness.ForwardLive,
                CreatedAt = 100,
                UpdatedAt = 200,
            });
            await session.StoreAsync(new TrackedTokenDocument
            {
                Id = TrackedTokenDocument.GetId("1111111111111111111111111111111111111111111111111111111111111111"),
                EntityType = TrackedEntityType.Token,
                EntityId = "1111111111111111111111111111111111111111111111111111111111111111",
                TokenId = "1111111111111111111111111111111111111111111111111111111111111111",
                Symbol = "STAMP",
                Tracked = false,
                IsTombstoned = true,
                TombstonedAt = 300,
                LifecycleStatus = TrackedEntityLifecycleStatus.Paused,
                CreatedAt = 150,
            });
            await session.StoreAsync(new TrackedTokenStatusDocument
            {
                Id = TrackedTokenStatusDocument.GetId("1111111111111111111111111111111111111111111111111111111111111111"),
                EntityType = TrackedEntityType.Token,
                EntityId = "1111111111111111111111111111111111111111111111111111111111111111",
                TokenId = "1111111111111111111111111111111111111111111111111111111111111111",
                Tracked = false,
                IsTombstoned = true,
                TombstonedAt = 300,
                LifecycleStatus = TrackedEntityLifecycleStatus.Paused,
                CreatedAt = 150,
                UpdatedAt = 301,
            });
            await session.SaveChangesAsync();
        }

        var addresses = await queryService.GetTrackedAddressesAsync();
        var tokens = await queryService.GetTrackedTokensAsync();
        var tokensWithTombstones = await queryService.GetTrackedTokensAsync(includeTombstoned: true);

        Assert.Single(addresses);
        Assert.Empty(tokens);
        Assert.Single(tokensWithTombstones);
        Assert.True(tokensWithTombstones[0].IsTombstoned);
    }

    [Fact]
    public async Task GetFindingsAndDashboardSummary_ReturnsFailureAndUnknownRootSignals()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var queryService = new AdminTrackingQueryService(store, new TokenProjectionReader(store));

        using (var session = store.OpenAsyncSession())
        {
            await session.StoreAsync(new TrackedAddressDocument
            {
                Id = TrackedAddressDocument.GetId("1PMycacnJaSqwwJqjawXBErnLsZ7RkXUAs"),
                EntityType = TrackedEntityType.Address,
                EntityId = "1PMycacnJaSqwwJqjawXBErnLsZ7RkXUAs",
                Address = "1PMycacnJaSqwwJqjawXBErnLsZ7RkXUAs",
                Name = "Ops",
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = true,
                Degraded = true,
                HistoryReadiness = TrackedEntityHistoryReadiness.Degraded,
                CreatedAt = 100,
            });
            await session.StoreAsync(new TrackedAddressStatusDocument
            {
                Id = TrackedAddressStatusDocument.GetId("1PMycacnJaSqwwJqjawXBErnLsZ7RkXUAs"),
                EntityType = TrackedEntityType.Address,
                EntityId = "1PMycacnJaSqwwJqjawXBErnLsZ7RkXUAs",
                Address = "1PMycacnJaSqwwJqjawXBErnLsZ7RkXUAs",
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = false,
                Degraded = true,
                FailureReason = "backfill_failed",
                DegradedAt = 400,
                HistoryReadiness = TrackedEntityHistoryReadiness.Degraded,
                CreatedAt = 100,
                UpdatedAt = 450,
            });
            await session.StoreAsync(new TrackedTokenDocument
            {
                Id = TrackedTokenDocument.GetId("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                EntityType = TrackedEntityType.Token,
                EntityId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                TokenId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                Symbol = "ROOT",
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = true,
                HistoryMode = TrackedEntityHistoryMode.FullHistory,
                HistoryReadiness = TrackedEntityHistoryReadiness.BackfillingFullHistory,
                CreatedAt = 200,
            });
            await session.StoreAsync(new TrackedTokenStatusDocument
            {
                Id = TrackedTokenStatusDocument.GetId("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                EntityType = TrackedEntityType.Token,
                EntityId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                TokenId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = false,
                Degraded = true,
                FailureReason = "unknown_root_blocked",
                HistoryMode = TrackedEntityHistoryMode.FullHistory,
                HistoryReadiness = TrackedEntityHistoryReadiness.BackfillingFullHistory,
                HistorySecurity = new TrackedTokenHistorySecurityState
                {
                    TrustedRoots = ["root-1", "root-2"],
                    UnknownRootFindings = ["rogue-root-1", "rogue-root-2"],
                    BlockingUnknownRoot = true,
                    CompletedTrustedRootCount = 1,
                    RootedHistorySecure = false,
                },
                CreatedAt = 200,
                UpdatedAt = 500,
            });
            await session.SaveChangesAsync();
        }

        var findings = await queryService.GetFindingsAsync();
        var summary = await queryService.GetDashboardSummaryAsync();
        var token = await queryService.GetTrackedTokenAsync("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        Assert.Equal(4, findings.Length);
        Assert.Equal("blocking_unknown_root", findings[0].Code);
        Assert.Equal(1, summary.ActiveAddressCount);
        Assert.Equal(1, summary.ActiveTokenCount);
        Assert.Equal(1, summary.DegradedAddressCount);
        Assert.Equal(1, summary.DegradedTokenCount);
        Assert.Equal(1, summary.BackfillingTokenCount);
        Assert.Equal(2, summary.UnknownRootFindingCount);
        Assert.Equal(1, summary.BlockingUnknownRootTokenCount);
        Assert.Equal(2, summary.FailureCount);
        Assert.NotNull(token);
        Assert.Equal(2, token.Readiness.History.RootedToken.TrustedRootCount);
        Assert.True(token.Readiness.History.RootedToken.BlockingUnknownRoot);
    }

    [Fact]
    public async Task GetTrackedEntityDetails_ReturnsOperationalSummaries()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var queryService = new AdminTrackingQueryService(store, new TokenProjectionReader(store));
        const string address = "1KFHE7w8BhaENAswwryaoccDb6qcT6DbYY";
        const string tokenId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        using (var session = store.OpenAsyncSession())
        {
            await session.StoreAsync(new TrackedAddressDocument
            {
                Id = TrackedAddressDocument.GetId(address),
                EntityType = TrackedEntityType.Address,
                EntityId = address,
                Address = address,
                Name = "Treasury",
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = true,
                HistoryReadiness = TrackedEntityHistoryReadiness.FullHistoryLive,
                CreatedAt = 100
            });
            await session.StoreAsync(new TrackedAddressStatusDocument
            {
                Id = TrackedAddressStatusDocument.GetId(address),
                EntityType = TrackedEntityType.Address,
                EntityId = address,
                Address = address,
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = true,
                HistoryReadiness = TrackedEntityHistoryReadiness.FullHistoryLive,
                UpdatedAt = 250,
                CreatedAt = 100
            });
            await session.StoreAsync(new AddressBalanceProjectionDocument
            {
                Id = AddressBalanceProjectionDocument.GetId(address, null),
                Address = address,
                Satoshis = 1250,
                LastSequence = 14
            });
            await session.StoreAsync(new AddressBalanceProjectionDocument
            {
                Id = AddressBalanceProjectionDocument.GetId(address, tokenId),
                Address = address,
                TokenId = tokenId,
                Satoshis = 400,
                LastSequence = 12
            });
            await session.StoreAsync(new AddressUtxoProjectionDocument
            {
                Id = AddressUtxoProjectionDocument.GetId("tx-bsv", 0),
                TxId = "tx-bsv",
                Vout = 0,
                Address = address,
                Satoshis = 1000,
                LastSequence = 11
            });
            await session.StoreAsync(new AddressUtxoProjectionDocument
            {
                Id = AddressUtxoProjectionDocument.GetId("tx-token-1", 1),
                TxId = "tx-token-1",
                Vout = 1,
                Address = address,
                TokenId = tokenId,
                Satoshis = 250,
                LastSequence = 12
            });
            await session.StoreAsync(new AddressUtxoProjectionDocument
            {
                Id = AddressUtxoProjectionDocument.GetId("tx-token-2", 2),
                TxId = "tx-token-2",
                Vout = 2,
                Address = address,
                TokenId = tokenId,
                Satoshis = 150,
                LastSequence = 13
            });
            await session.StoreAsync(new AddressProjectionAppliedTransactionDocument
            {
                Id = AddressProjectionAppliedTransactionDocument.GetId("addr-first"),
                TxId = "addr-first",
                Timestamp = 1_710_000_000,
                Height = 1000,
                LastSequence = 10,
                Credits =
                [
                    new AddressProjectionUtxoSnapshot
                    {
                        Id = "utxo-1",
                        TxId = "addr-first",
                        Vout = 0,
                        Address = address,
                        Satoshis = 1000
                    }
                ]
            });
            await session.StoreAsync(new AddressProjectionAppliedTransactionDocument
            {
                Id = AddressProjectionAppliedTransactionDocument.GetId("addr-last"),
                TxId = "addr-last",
                Timestamp = 1_710_000_500,
                Height = 1012,
                LastSequence = 14,
                Debits =
                [
                    new AddressProjectionUtxoSnapshot
                    {
                        Id = "utxo-1",
                        TxId = "addr-first",
                        Vout = 0,
                        Address = address,
                        Satoshis = 1000
                    }
                ]
            });

            await session.StoreAsync(new TrackedTokenDocument
            {
                Id = TrackedTokenDocument.GetId(tokenId),
                EntityType = TrackedEntityType.Token,
                EntityId = tokenId,
                TokenId = tokenId,
                Symbol = "BETA",
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = true,
                HistoryReadiness = TrackedEntityHistoryReadiness.FullHistoryLive,
                CreatedAt = 150
            });
            await session.StoreAsync(new TrackedTokenStatusDocument
            {
                Id = TrackedTokenStatusDocument.GetId(tokenId),
                EntityType = TrackedEntityType.Token,
                EntityId = tokenId,
                TokenId = tokenId,
                Tracked = true,
                LifecycleStatus = TrackedEntityLifecycleStatus.Live,
                Readable = true,
                Authoritative = true,
                HistoryReadiness = TrackedEntityHistoryReadiness.FullHistoryLive,
                UpdatedAt = 350,
                CreatedAt = 150
            });
            await session.StoreAsync(new TokenStateProjectionDocument
            {
                Id = TokenStateProjectionDocument.GetId(tokenId),
                TokenId = tokenId,
                ProtocolType = "stas",
                ValidationStatus = TokenProjectionValidationStatus.Valid,
                Issuer = "issuer-address",
                RedeemAddress = "redeem-address",
                TotalKnownSupply = 10_000,
                BurnedSatoshis = 50,
                LastIndexedHeight = 1012,
                LastSequence = 22
            });
            await session.StoreAsync(new AddressBalanceProjectionDocument
            {
                Id = AddressBalanceProjectionDocument.GetId("holder-2", tokenId),
                Address = "holder-2",
                TokenId = tokenId,
                Satoshis = 600,
                LastSequence = 18
            });
            await session.StoreAsync(new TokenHistoryProjectionDocument
            {
                Id = TokenHistoryProjectionDocument.GetId(tokenId, "token-first"),
                TokenId = tokenId,
                TxId = "token-first",
                Timestamp = 1_710_000_100,
                Height = 1001,
                IsIssue = true,
                ValidationStatus = TokenProjectionValidationStatus.Valid,
                ProtocolType = "stas",
                LastSequence = 20
            });
            await session.StoreAsync(new TokenHistoryProjectionDocument
            {
                Id = TokenHistoryProjectionDocument.GetId(tokenId, "token-last"),
                TokenId = tokenId,
                TxId = "token-last",
                Timestamp = 1_710_000_900,
                Height = 1015,
                ValidationStatus = TokenProjectionValidationStatus.Valid,
                ProtocolType = "stas",
                LastSequence = 24
            });
            await session.SaveChangesAsync();
        }

        var addressDetail = await queryService.GetTrackedAddressAsync(address);
        var tokenDetail = await queryService.GetTrackedTokenAsync(tokenId);

        Assert.NotNull(addressDetail?.Summary);
        Assert.Equal(1250, addressDetail.Summary.CurrentBsvBalanceSatoshis);
        Assert.Equal(3, addressDetail.Summary.TotalUtxoCount);
        Assert.Equal(1, addressDetail.Summary.BsvUtxoCount);
        Assert.Equal(2, addressDetail.Summary.TokenUtxoCount);
        Assert.Equal(2, addressDetail.Summary.TransactionCount);
        Assert.Equal(1000, addressDetail.Summary.FirstTransactionBlockHeight);
        Assert.Equal(1012, addressDetail.Summary.LastTransactionBlockHeight);
        Assert.Equal(14, addressDetail.Summary.LastProjectionSequence);
        Assert.Single(addressDetail.Summary.TokenBalances);
        Assert.Equal(tokenId, addressDetail.Summary.TokenBalances[0].TokenId);
        Assert.Equal(400, addressDetail.Summary.TokenBalances[0].Satoshis);

        Assert.NotNull(tokenDetail?.Summary);
        Assert.Equal("stas", tokenDetail.Summary.ProtocolType);
        Assert.Equal(TokenProjectionValidationStatus.Valid, tokenDetail.Summary.ValidationStatus);
        Assert.Equal("issuer-address", tokenDetail.Summary.Issuer);
        Assert.Equal("redeem-address", tokenDetail.Summary.RedeemAddress);
        Assert.Equal(10_000, tokenDetail.Summary.LocalKnownSupplySatoshis);
        Assert.Equal(50, tokenDetail.Summary.BurnedSatoshis);
        Assert.Equal(2, tokenDetail.Summary.HolderCount);
        Assert.Equal(2, tokenDetail.Summary.UtxoCount);
        Assert.Equal(2, tokenDetail.Summary.TransactionCount);
        Assert.Equal(1001, tokenDetail.Summary.FirstTransactionBlockHeight);
        Assert.Equal(1015, tokenDetail.Summary.LastTransactionBlockHeight);
        Assert.Equal(22, tokenDetail.Summary.LastProjectionSequence);
    }
}
