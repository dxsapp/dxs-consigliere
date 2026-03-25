using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Tracking;
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class TrackedEntityLifecycleOrchestratorIntegrationTests : RavenTestDriver
{
    [Fact]
    public async Task BeginTrackingAddressAsync_MovesRegisteredEntityToBackfilling()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var registration = new TrackedEntityRegistrationStore(store);
        var orchestrator = new TrackedEntityLifecycleOrchestrator(store);
        const string address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";

        await registration.RegisterAddressAsync(address, "Genesis");
        await orchestrator.BeginTrackingAddressAsync(address);

        using var session = store.OpenAsyncSession();
        var tracked = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address));
        var status = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(address));

        Assert.Equal(TrackedEntityLifecycleStatus.Backfilling, tracked!.LifecycleStatus);
        Assert.Equal(TrackedEntityLifecycleStatus.Backfilling, status!.LifecycleStatus);
        Assert.NotNull(status.BackfillStartedAt);
        Assert.NotNull(status.RealtimeAttachedAt);
        Assert.False(tracked.Readable);
        Assert.False(tracked.Authoritative);
    }

    [Fact]
    public async Task CompletionAndGapClosure_AreRequiredBeforeLive()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var registration = new TrackedEntityRegistrationStore(store);
        var orchestrator = new TrackedEntityLifecycleOrchestrator(store);
        const string tokenId = "1111111111111111111111111111111111111111111111111111111111111111";

        await registration.RegisterTokenAsync(tokenId, "STAMP");
        await orchestrator.BeginTrackingTokenAsync(tokenId);
        await orchestrator.MarkTokenBackfillCompletedAsync(tokenId);

        using (var session = store.OpenAsyncSession())
        {
            var catchingUp = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId));
            Assert.Equal(TrackedEntityLifecycleStatus.CatchingUp, catchingUp!.LifecycleStatus);
            Assert.False(catchingUp.Readable);
        }

        await orchestrator.MarkTokenGapClosedAsync(tokenId);

        using var session2 = store.OpenAsyncSession();
        var live = await session2.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId));
        Assert.Equal(TrackedEntityLifecycleStatus.Live, live!.LifecycleStatus);
        Assert.True(live.Readable);
        Assert.True(live.Authoritative);
    }

    [Fact]
    public async Task DegradedTransition_UsesIntegritySafeFlagToControlReadability()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var registration = new TrackedEntityRegistrationStore(store);
        var orchestrator = new TrackedEntityLifecycleOrchestrator(store);
        const string address = "1dice8EMZmqKvrGE4Qc9bUFf9PX3xaYDp";

        await registration.RegisterAddressAsync(address, "Dice");
        await orchestrator.BeginTrackingAddressAsync(address);
        await orchestrator.MarkAddressBackfillCompletedAsync(address);
        await orchestrator.MarkAddressGapClosedAsync(address);
        await orchestrator.MarkAddressDegradedAsync(address, integritySafe: false, reason: "realtime_gap_detected");

        using (var session = store.OpenAsyncSession())
        {
            var unsafeDegraded = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address));
            Assert.Equal(TrackedEntityLifecycleStatus.Degraded, unsafeDegraded!.LifecycleStatus);
            Assert.True(unsafeDegraded.Degraded);
            Assert.False(unsafeDegraded.Readable);
            Assert.False(unsafeDegraded.Authoritative);
        }

        await orchestrator.RecoverAddressAsync(address);
        await orchestrator.MarkAddressDegradedAsync(address, integritySafe: true, reason: "provider_latency");

        using var session2 = store.OpenAsyncSession();
        var safeDegraded = await session2.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address));
        Assert.Equal(TrackedEntityLifecycleStatus.Degraded, safeDegraded!.LifecycleStatus);
        Assert.True(safeDegraded.Readable);
        Assert.True(safeDegraded.Authoritative);
    }
}
