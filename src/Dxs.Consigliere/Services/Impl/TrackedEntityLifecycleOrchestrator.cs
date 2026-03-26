using Dxs.Common.Cache;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Services.Impl;

public sealed class TrackedEntityLifecycleOrchestrator(
    IDocumentStore documentStore,
    IProjectionCacheInvalidationSink cacheInvalidationSink,
    IProjectionReadCacheKeyFactory cacheKeyFactory,
    IProjectionCacheInvalidationTelemetry invalidationTelemetry
) : ITrackedEntityLifecycleOrchestrator
{
    public TrackedEntityLifecycleOrchestrator(IDocumentStore documentStore)
        : this(documentStore, new NoopProjectionReadCache(), new ProjectionReadCacheKeyFactory(), new ProjectionCacheInvalidationTelemetry())
    {
    }

    public TrackedEntityLifecycleOrchestrator(
        IDocumentStore documentStore,
        IProjectionCacheInvalidationSink cacheInvalidationSink,
        IProjectionReadCacheKeyFactory cacheKeyFactory
    )
        : this(documentStore, cacheInvalidationSink, cacheKeyFactory, new ProjectionCacheInvalidationTelemetry())
    {
    }

    public async Task BeginTrackingAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var tracked = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address), cancellationToken)
            ?? throw new InvalidOperationException($"Tracked address `{address}` not found");
        var status = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(address), cancellationToken)
            ?? throw new InvalidOperationException($"Tracked address status `{address}` not found");

        ApplyBoundaryInitialization(status, await GetAnchorBlockHeightAsync(session, cancellationToken));
        SyncSnapshot(tracked, status);
        status.SetUpdate();
        tracked.SetUpdate();

        await session.SaveChangesAsync(cancellationToken);
        await InvalidateTrackedAddressAsync(address, cancellationToken);
    }

    public async Task BeginTrackingTokenAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var tracked = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId), cancellationToken)
            ?? throw new InvalidOperationException($"Tracked token `{tokenId}` not found");
        var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), cancellationToken)
            ?? throw new InvalidOperationException($"Tracked token status `{tokenId}` not found");

        ApplyBoundaryInitialization(status, await GetAnchorBlockHeightAsync(session, cancellationToken));
        SyncSnapshot(tracked, status);
        status.SetUpdate();
        tracked.SetUpdate();

        await session.SaveChangesAsync(cancellationToken);
        await InvalidateTrackedTokenAsync(tokenId, cancellationToken);
    }

    public Task MarkAddressHistoryBackfillQueuedAsync(string address, CancellationToken cancellationToken = default)
        => MutateAddressAsync(address, document =>
        {
            document.HistoryBackfillStatus = HistoryBackfillExecutionStatus.Queued;
            document.HistoryBackfillRequestedAt ??= Now();
            document.HistoryBackfillErrorCode = null;
            ApplyFullHistoryReadiness(document);
        }, cancellationToken);

    public Task MarkTokenHistoryBackfillQueuedAsync(string tokenId, CancellationToken cancellationToken = default)
        => MutateTokenAsync(tokenId, document =>
        {
            document.HistoryBackfillStatus = HistoryBackfillExecutionStatus.Queued;
            document.HistoryBackfillRequestedAt ??= Now();
            document.HistoryBackfillErrorCode = null;
            ApplyFullHistoryReadiness(document);
        }, cancellationToken);

    public Task MarkAddressHistoryBackfillRunningAsync(string address, CancellationToken cancellationToken = default)
        => MutateAddressAsync(address, document =>
        {
            document.HistoryBackfillStatus = HistoryBackfillExecutionStatus.Running;
            document.HistoryBackfillRequestedAt ??= Now();
            document.HistoryBackfillStartedAt ??= Now();
            document.HistoryBackfillLastProgressAt = Now();
            document.HistoryBackfillErrorCode = null;
            ApplyFullHistoryReadiness(document);
        }, cancellationToken);

    public Task MarkTokenHistoryBackfillRunningAsync(string tokenId, CancellationToken cancellationToken = default)
        => MutateTokenAsync(tokenId, document =>
        {
            document.HistoryBackfillStatus = HistoryBackfillExecutionStatus.Running;
            document.HistoryBackfillRequestedAt ??= Now();
            document.HistoryBackfillStartedAt ??= Now();
            document.HistoryBackfillLastProgressAt = Now();
            document.HistoryBackfillErrorCode = null;
            ApplyFullHistoryReadiness(document);
        }, cancellationToken);

    public Task UpdateAddressHistoryBackfillProgressAsync(
        string address,
        int itemsScanned,
        int itemsApplied,
        int? oldestCoveredBlockHeight,
        CancellationToken cancellationToken = default
    )
        => MutateAddressAsync(address, document => UpdateHistoryBackfillProgress(document, itemsScanned, itemsApplied, oldestCoveredBlockHeight), cancellationToken);

    public Task UpdateTokenHistoryBackfillProgressAsync(
        string tokenId,
        int itemsScanned,
        int itemsApplied,
        int? oldestCoveredBlockHeight,
        CancellationToken cancellationToken = default
    )
        => MutateTokenAsync(tokenId, document => UpdateHistoryBackfillProgress(document, itemsScanned, itemsApplied, oldestCoveredBlockHeight), cancellationToken);

    public Task MarkAddressHistoryBackfillWaitingRetryAsync(string address, string errorCode, CancellationToken cancellationToken = default)
        => MutateAddressAsync(address, document =>
        {
            document.HistoryBackfillStatus = HistoryBackfillExecutionStatus.WaitingRetry;
            document.HistoryBackfillLastProgressAt = Now();
            document.HistoryBackfillErrorCode = errorCode;
            ApplyFullHistoryReadiness(document);
        }, cancellationToken);

    public Task MarkTokenHistoryBackfillWaitingRetryAsync(string tokenId, string errorCode, CancellationToken cancellationToken = default)
        => MutateTokenAsync(tokenId, document =>
        {
            document.HistoryBackfillStatus = HistoryBackfillExecutionStatus.WaitingRetry;
            document.HistoryBackfillLastProgressAt = Now();
            document.HistoryBackfillErrorCode = errorCode;
            ApplyFullHistoryReadiness(document);
        }, cancellationToken);

    public Task MarkAddressHistoryBackfillCompletedAsync(string address, CancellationToken cancellationToken = default)
        => MutateAddressAsync(address, document =>
        {
            document.HistoryBackfillStatus = HistoryBackfillExecutionStatus.Completed;
            document.HistoryBackfillLastProgressAt = Now();
            document.HistoryBackfillCompletedAt = Now();
            document.HistoryBackfillErrorCode = null;
            document.HistoryReadiness = TrackedEntityHistoryReadiness.FullHistoryLive;
            document.HistoryCoverage ??= new TrackedHistoryCoverage();
            document.HistoryCoverage.Mode = TrackedEntityHistoryMode.FullHistory;
            document.HistoryCoverage.FullCoverage = true;
        }, cancellationToken);

    public Task MarkTokenHistoryBackfillCompletedAsync(string tokenId, CancellationToken cancellationToken = default)
        => MutateTokenAsync(tokenId, document =>
        {
            document.HistoryBackfillStatus = HistoryBackfillExecutionStatus.Completed;
            document.HistoryBackfillLastProgressAt = Now();
            document.HistoryBackfillCompletedAt = Now();
            document.HistoryBackfillErrorCode = null;
            document.HistoryReadiness = TrackedEntityHistoryReadiness.FullHistoryLive;
            document.HistoryCoverage ??= new TrackedHistoryCoverage();
            document.HistoryCoverage.Mode = TrackedEntityHistoryMode.FullHistory;
            document.HistoryCoverage.FullCoverage = true;
        }, cancellationToken);

    public Task MarkAddressHistoryBackfillFailedAsync(string address, string errorCode, CancellationToken cancellationToken = default)
        => MutateAddressAsync(address, document =>
        {
            document.HistoryBackfillStatus = HistoryBackfillExecutionStatus.Failed;
            document.HistoryBackfillLastProgressAt = Now();
            document.HistoryBackfillErrorCode = errorCode;
            document.HistoryReadiness = TrackedEntityHistoryReadiness.Degraded;
        }, cancellationToken);

    public Task MarkTokenHistoryBackfillFailedAsync(string tokenId, string errorCode, CancellationToken cancellationToken = default)
        => MutateTokenAsync(tokenId, document =>
        {
            document.HistoryBackfillStatus = HistoryBackfillExecutionStatus.Failed;
            document.HistoryBackfillLastProgressAt = Now();
            document.HistoryBackfillErrorCode = errorCode;
            document.HistoryReadiness = TrackedEntityHistoryReadiness.Degraded;
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
            if (string.Equals(document.HistoryReadiness, TrackedEntityHistoryReadiness.Degraded, StringComparison.Ordinal))
            {
                document.HistoryReadiness = string.Equals(document.HistoryMode, TrackedEntityHistoryMode.FullHistory, StringComparison.Ordinal)
                    ? TrackedEntityHistoryReadiness.BackfillingFullHistory
                    : TrackedEntityHistoryReadiness.ForwardLive;
            }
        }, cancellationToken);

    public Task RecoverTokenAsync(string tokenId, CancellationToken cancellationToken = default)
        => MutateTokenAsync(tokenId, document =>
        {
            document.DegradedAt = null;
            document.IntegritySafe = null;
            document.FailureReason = null;
            if (string.Equals(document.HistoryReadiness, TrackedEntityHistoryReadiness.Degraded, StringComparison.Ordinal))
            {
                document.HistoryReadiness = string.Equals(document.HistoryMode, TrackedEntityHistoryMode.FullHistory, StringComparison.Ordinal)
                    ? TrackedEntityHistoryReadiness.BackfillingFullHistory
                    : TrackedEntityHistoryReadiness.ForwardLive;
            }
        }, cancellationToken);

    public Task UpdateTokenHistorySecurityAsync(
        string tokenId,
        TrackedTokenHistorySecurityState historySecurity,
        CancellationToken cancellationToken = default
    )
        => MutateTokenAsync(tokenId, document =>
        {
            document.HistorySecurity = historySecurity?.Clone() ?? new TrackedTokenHistorySecurityState();
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
        await InvalidateTrackedAddressAsync(address, cancellationToken);
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
        await InvalidateTrackedTokenAsync(tokenId, cancellationToken);
    }

    private static void ApplyBoundaryInitialization(TrackedEntityStatusDocumentBase document, int? anchorBlockHeight)
    {
        var now = Now();
        document.BackfillStartedAt ??= now;
        document.BackfillCompletedAt ??= now;
        document.RealtimeAttachedAt ??= now;
        document.GapClosedAt ??= now;
        document.HistoryAnchorBlockHeight ??= anchorBlockHeight;
        document.HistoryAnchorObservedAt ??= now;
        document.HistoryCoverage ??= new TrackedHistoryCoverage();
        document.HistoryCoverage.Mode = document.HistoryMode;
        document.HistoryCoverage.AuthoritativeFromBlockHeight ??= anchorBlockHeight;
        document.HistoryCoverage.AuthoritativeFromObservedAt ??= now;
        document.HistoryCoverage.FullCoverage = string.Equals(document.HistoryMode, TrackedEntityHistoryMode.FullHistory, StringComparison.Ordinal)
            && string.Equals(document.HistoryReadiness, TrackedEntityHistoryReadiness.FullHistoryLive, StringComparison.Ordinal);
        document.HistoryReadiness = string.Equals(document.HistoryMode, TrackedEntityHistoryMode.FullHistory, StringComparison.Ordinal)
            ? TrackedEntityHistoryReadiness.BackfillingFullHistory
            : TrackedEntityHistoryReadiness.ForwardLive;
        document.FailureReason = null;
        Evaluate(document);
    }

    private static void UpdateHistoryBackfillProgress(
        TrackedEntityStatusDocumentBase document,
        int itemsScanned,
        int itemsApplied,
        int? oldestCoveredBlockHeight
    )
    {
        document.HistoryBackfillStatus = HistoryBackfillExecutionStatus.Running;
        document.HistoryBackfillStartedAt ??= Now();
        document.HistoryBackfillLastProgressAt = Now();
        document.HistoryBackfillItemsScanned = itemsScanned;
        document.HistoryBackfillItemsApplied = itemsApplied;
        document.HistoryBackfillErrorCode = null;
        document.HistoryCoverage ??= new TrackedHistoryCoverage();
        document.HistoryCoverage.Mode = TrackedEntityHistoryMode.FullHistory;
        if (oldestCoveredBlockHeight.HasValue)
            document.HistoryCoverage.AuthoritativeFromBlockHeight = oldestCoveredBlockHeight.Value;
        document.HistoryCoverage.AuthoritativeFromObservedAt ??= document.HistoryAnchorObservedAt ?? Now();
        ApplyFullHistoryReadiness(document);
    }

    private static void ApplyFullHistoryReadiness(TrackedEntityStatusDocumentBase document)
    {
        document.HistoryMode = TrackedEntityHistoryMode.FullHistory;
        document.HistoryCoverage ??= new TrackedHistoryCoverage();
        document.HistoryCoverage.Mode = TrackedEntityHistoryMode.FullHistory;
        if (!string.Equals(document.HistoryReadiness, TrackedEntityHistoryReadiness.FullHistoryLive, StringComparison.Ordinal))
            document.HistoryReadiness = TrackedEntityHistoryReadiness.BackfillingFullHistory;
    }

    private static void Evaluate(TrackedEntityStatusDocumentBase document)
    {
        if (document.DegradedAt is not null)
        {
            var ready = document.RealtimeAttachedAt is not null && document.GapClosedAt is not null;
            var integritySafe = document.IntegritySafe == true;

            document.LifecycleStatus = TrackedEntityLifecycleStatus.Degraded;
            document.Degraded = true;
            document.Readable = ready && integritySafe;
            document.Authoritative = ready && integritySafe;
            return;
        }

        document.Degraded = false;

        if (document.RealtimeAttachedAt is not null && document.GapClosedAt is not null)
        {
            document.LifecycleStatus = TrackedEntityLifecycleStatus.Live;
            document.Readable = true;
            document.Authoritative = true;
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
        tracked.HistoryMode = status.HistoryMode;
        tracked.HistoryReadiness = status.HistoryReadiness;
        tracked.HistoryCoverage = status.HistoryCoverage?.Clone();
        tracked.HistoryBackfillStatus = status.HistoryBackfillStatus;
        tracked.HistoryBackfillRequestedAt = status.HistoryBackfillRequestedAt;
        tracked.HistoryBackfillStartedAt = status.HistoryBackfillStartedAt;
        tracked.HistoryBackfillLastProgressAt = status.HistoryBackfillLastProgressAt;
        tracked.HistoryBackfillCompletedAt = status.HistoryBackfillCompletedAt;
        tracked.HistoryBackfillItemsScanned = status.HistoryBackfillItemsScanned;
        tracked.HistoryBackfillItemsApplied = status.HistoryBackfillItemsApplied;
        tracked.HistoryBackfillErrorCode = status.HistoryBackfillErrorCode;
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
        tracked.HistoryMode = status.HistoryMode;
        tracked.HistoryReadiness = status.HistoryReadiness;
        tracked.HistoryCoverage = status.HistoryCoverage?.Clone();
        tracked.HistoryBackfillStatus = status.HistoryBackfillStatus;
        tracked.HistoryBackfillRequestedAt = status.HistoryBackfillRequestedAt;
        tracked.HistoryBackfillStartedAt = status.HistoryBackfillStartedAt;
        tracked.HistoryBackfillLastProgressAt = status.HistoryBackfillLastProgressAt;
        tracked.HistoryBackfillCompletedAt = status.HistoryBackfillCompletedAt;
        tracked.HistoryBackfillItemsScanned = status.HistoryBackfillItemsScanned;
        tracked.HistoryBackfillItemsApplied = status.HistoryBackfillItemsApplied;
        tracked.HistoryBackfillErrorCode = status.HistoryBackfillErrorCode;
        tracked.HistorySecurity = status.HistorySecurity?.Clone() ?? new TrackedTokenHistorySecurityState();
        tracked.IsTombstoned = status.IsTombstoned;
        tracked.TombstonedAt = status.TombstonedAt;
    }

    private async Task<int?> GetAnchorBlockHeightAsync(IAsyncDocumentSession session, CancellationToken cancellationToken)
        => await session.Query<BlockProcessContext>()
            .Where(x => x.Height != 0)
            .OrderByDescending(x => x.Height)
            .Select(x => (int?)x.Height)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task InvalidateTrackedAddressAsync(string address, CancellationToken cancellationToken)
    {
        var invalidationTags = cacheKeyFactory.GetTrackedAddressReadinessInvalidationTags([address]);
        invalidationTelemetry.Record(invalidationTags);
        await cacheInvalidationSink.InvalidateTagsAsync(invalidationTags, cancellationToken);
    }

    private async Task InvalidateTrackedTokenAsync(string tokenId, CancellationToken cancellationToken)
    {
        var invalidationTags = cacheKeyFactory.GetTrackedTokenReadinessInvalidationTags([tokenId]);
        invalidationTelemetry.Record(invalidationTags);
        await cacheInvalidationSink.InvalidateTagsAsync(invalidationTags, cancellationToken);
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
