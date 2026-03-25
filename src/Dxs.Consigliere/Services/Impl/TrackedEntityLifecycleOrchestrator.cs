using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Services.Impl;

public sealed class TrackedEntityLifecycleOrchestrator(IDocumentStore documentStore) : ITrackedEntityLifecycleOrchestrator
{
    public Task BeginTrackingAddressAsync(string address, CancellationToken cancellationToken = default)
        => MutateAddressAsync(address, document =>
        {
            document.BackfillStartedAt ??= Now();
            document.RealtimeAttachedAt ??= Now();
            document.FailureReason = null;
        }, cancellationToken);

    public Task BeginTrackingTokenAsync(string tokenId, CancellationToken cancellationToken = default)
        => MutateTokenAsync(tokenId, document =>
        {
            document.BackfillStartedAt ??= Now();
            document.RealtimeAttachedAt ??= Now();
            document.FailureReason = null;
        }, cancellationToken);

    public Task MarkAddressBackfillCompletedAsync(string address, CancellationToken cancellationToken = default)
        => MutateAddressAsync(address, document =>
        {
            document.BackfillStartedAt ??= Now();
            document.BackfillCompletedAt = Now();
            document.FailureReason = null;
        }, cancellationToken);

    public Task MarkTokenBackfillCompletedAsync(string tokenId, CancellationToken cancellationToken = default)
        => MutateTokenAsync(tokenId, document =>
        {
            document.BackfillStartedAt ??= Now();
            document.BackfillCompletedAt = Now();
            document.FailureReason = null;
        }, cancellationToken);

    public Task MarkAddressGapClosedAsync(string address, CancellationToken cancellationToken = default)
        => MutateAddressAsync(address, document =>
        {
            document.GapClosedAt = Now();
            document.FailureReason = null;
        }, cancellationToken);

    public Task MarkTokenGapClosedAsync(string tokenId, CancellationToken cancellationToken = default)
        => MutateTokenAsync(tokenId, document =>
        {
            document.GapClosedAt = Now();
            document.FailureReason = null;
        }, cancellationToken);

    public Task MarkAddressDegradedAsync(string address, bool integritySafe, string reason = null, CancellationToken cancellationToken = default)
        => MutateAddressAsync(address, document =>
        {
            document.DegradedAt = Now();
            document.IntegritySafe = integritySafe;
            document.FailureReason = reason;
        }, cancellationToken);

    public Task MarkTokenDegradedAsync(string tokenId, bool integritySafe, string reason = null, CancellationToken cancellationToken = default)
        => MutateTokenAsync(tokenId, document =>
        {
            document.DegradedAt = Now();
            document.IntegritySafe = integritySafe;
            document.FailureReason = reason;
        }, cancellationToken);

    public Task RecoverAddressAsync(string address, CancellationToken cancellationToken = default)
        => MutateAddressAsync(address, document =>
        {
            document.DegradedAt = null;
            document.IntegritySafe = null;
            document.FailureReason = null;
        }, cancellationToken);

    public Task RecoverTokenAsync(string tokenId, CancellationToken cancellationToken = default)
        => MutateTokenAsync(tokenId, document =>
        {
            document.DegradedAt = null;
            document.IntegritySafe = null;
            document.FailureReason = null;
        }, cancellationToken);

    private async Task MutateAddressAsync(
        string address,
        Action<TrackedAddressStatusDocument> mutate,
        CancellationToken cancellationToken
    )
    {
        using var session = documentStore.GetSession();
        var tracked = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address), cancellationToken)
            ?? throw new InvalidOperationException($"Tracked address `{address}` not found");
        var status = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(address), cancellationToken)
            ?? throw new InvalidOperationException($"Tracked address status `{address}` not found");

        mutate(status);
        status.SetUpdate();
        Evaluate(status);
        SyncSnapshot(tracked, status);
        tracked.SetUpdate();

        await session.SaveChangesAsync(cancellationToken);
    }

    private async Task MutateTokenAsync(
        string tokenId,
        Action<TrackedTokenStatusDocument> mutate,
        CancellationToken cancellationToken
    )
    {
        using var session = documentStore.GetSession();
        var tracked = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId), cancellationToken)
            ?? throw new InvalidOperationException($"Tracked token `{tokenId}` not found");
        var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), cancellationToken)
            ?? throw new InvalidOperationException($"Tracked token status `{tokenId}` not found");

        mutate(status);
        status.SetUpdate();
        Evaluate(status);
        SyncSnapshot(tracked, status);
        tracked.SetUpdate();

        await session.SaveChangesAsync(cancellationToken);
    }

    private static void Evaluate(TrackedEntityStatusDocumentBase document)
    {
        if (document.DegradedAt is not null)
        {
            var ready = document.BackfillCompletedAt is not null
                && document.RealtimeAttachedAt is not null
                && document.GapClosedAt is not null;
            var integritySafe = document.IntegritySafe == true;

            document.LifecycleStatus = TrackedEntityLifecycleStatus.Degraded;
            document.Degraded = true;
            document.Readable = ready && integritySafe;
            document.Authoritative = ready && integritySafe;
            return;
        }

        document.Degraded = false;

        if (document.BackfillCompletedAt is not null
            && document.RealtimeAttachedAt is not null
            && document.GapClosedAt is not null)
        {
            document.LifecycleStatus = TrackedEntityLifecycleStatus.Live;
            document.Readable = true;
            document.Authoritative = true;
            return;
        }

        if (document.BackfillCompletedAt is not null)
        {
            document.LifecycleStatus = TrackedEntityLifecycleStatus.CatchingUp;
            document.Readable = false;
            document.Authoritative = false;
            return;
        }

        if (document.BackfillStartedAt is not null || document.RealtimeAttachedAt is not null)
        {
            document.LifecycleStatus = TrackedEntityLifecycleStatus.Backfilling;
            document.Readable = false;
            document.Authoritative = false;
            return;
        }

        document.LifecycleStatus = TrackedEntityLifecycleStatus.Registered;
        document.Readable = false;
        document.Authoritative = false;
    }

    private static void SyncSnapshot(TrackedAddressDocument tracked, TrackedAddressStatusDocument status)
    {
        tracked.Tracked = status.Tracked;
        tracked.LifecycleStatus = status.LifecycleStatus;
        tracked.Readable = status.Readable;
        tracked.Authoritative = status.Authoritative;
        tracked.Degraded = status.Degraded;
        tracked.LagBlocks = status.LagBlocks;
        tracked.Progress = status.Progress;
        tracked.IsTombstoned = status.IsTombstoned;
        tracked.TombstonedAt = status.TombstonedAt;
    }

    private static void SyncSnapshot(TrackedTokenDocument tracked, TrackedTokenStatusDocument status)
    {
        tracked.Tracked = status.Tracked;
        tracked.LifecycleStatus = status.LifecycleStatus;
        tracked.Readable = status.Readable;
        tracked.Authoritative = status.Authoritative;
        tracked.Degraded = status.Degraded;
        tracked.LagBlocks = status.LagBlocks;
        tracked.Progress = status.Progress;
        tracked.IsTombstoned = status.IsTombstoned;
        tracked.TombstonedAt = status.TombstonedAt;
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
