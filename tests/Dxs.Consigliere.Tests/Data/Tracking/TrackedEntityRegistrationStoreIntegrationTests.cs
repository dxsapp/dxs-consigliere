using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Tracking;
using Dxs.Tests.Shared;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Tracking;

public class TrackedEntityRegistrationStoreIntegrationTests : RavenTestDriver
{
    [Fact]
    public async Task RegisterAddressAsync_CreatesTrackedAndStatusDocsWithDeterministicIds()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var registrationStore = new TrackedEntityRegistrationStore(store);

        await registrationStore.RegisterAddressAsync("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa", "Genesis");

        using var session = store.OpenAsyncSession();
        var tracked = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa"));
        var status = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa"));
        var legacy = await session.LoadAsync<WatchingAddress>("address/1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa");

        Assert.NotNull(tracked);
        Assert.NotNull(status);
        Assert.NotNull(legacy);
        Assert.Equal(TrackedEntityType.Address, tracked.EntityType);
        Assert.Equal(TrackedEntityLifecycleStatus.Registered, tracked.LifecycleStatus);
        Assert.True(tracked.Tracked);
        Assert.False(tracked.IsTombstoned);
        Assert.Equal("Genesis", tracked.Name);
        Assert.Equal(TrackedEntityType.Address, status.EntityType);
        Assert.Equal(TrackedEntityLifecycleStatus.Registered, status.LifecycleStatus);
        Assert.False(status.Readable);
        Assert.False(status.Authoritative);
    }

    [Fact]
    public async Task RegisterTokenAsync_IsIdempotentAndDoesNotDuplicateLegacyWatchers()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var registrationStore = new TrackedEntityRegistrationStore(store);
        const string tokenId = "1111111111111111111111111111111111111111111111111111111111111111";

        await registrationStore.RegisterTokenAsync(tokenId, "STAMP");
        await registrationStore.RegisterTokenAsync(tokenId, "STAMP-V2");

        using var session = store.OpenAsyncSession();
        var tracked = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId));
        var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId));
        var legacy = await session.Advanced.AsyncDocumentQuery<WatchingToken>()
            .WhereEquals(nameof(WatchingToken.TokenId), tokenId)
            .ToListAsync();

        Assert.NotNull(tracked);
        Assert.NotNull(status);
        Assert.Single(legacy);
        Assert.Equal("STAMP-V2", tracked.Symbol);
        Assert.Equal(TrackedEntityLifecycleStatus.Registered, tracked.LifecycleStatus);
        Assert.Equal(TrackedEntityLifecycleStatus.Registered, status.LifecycleStatus);
        Assert.False(status.IsTombstoned);
    }
}
