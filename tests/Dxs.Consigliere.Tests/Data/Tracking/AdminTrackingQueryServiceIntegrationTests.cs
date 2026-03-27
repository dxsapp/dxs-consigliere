using Dxs.Consigliere.Data.Models.Tracking;
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
        var queryService = new AdminTrackingQueryService(store);

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
        var queryService = new AdminTrackingQueryService(store);

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
}
