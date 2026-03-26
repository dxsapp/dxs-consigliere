using Dxs.Common.Cache;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Data.Tracking;

public sealed class TrackedEntityRegistrationStore(
    IDocumentStore documentStore,
    IProjectionCacheInvalidationSink cacheInvalidationSink,
    IProjectionReadCacheKeyFactory cacheKeyFactory,
    IProjectionCacheInvalidationTelemetry invalidationTelemetry
) : ITrackedEntityRegistrationStore
{
    public TrackedEntityRegistrationStore(IDocumentStore documentStore)
        : this(documentStore, new NoopProjectionReadCache(), new ProjectionReadCacheKeyFactory(), new ProjectionCacheInvalidationTelemetry())
    {
    }

    public TrackedEntityRegistrationStore(
        IDocumentStore documentStore,
        IProjectionCacheInvalidationSink cacheInvalidationSink,
        IProjectionReadCacheKeyFactory cacheKeyFactory
    )
        : this(documentStore, cacheInvalidationSink, cacheKeyFactory, new ProjectionCacheInvalidationTelemetry())
    {
    }

    public async Task RegisterAddressAsync(
        string address,
        string name,
        string historyMode = TrackedEntityHistoryMode.ForwardOnly,
        CancellationToken cancellationToken = default
    )
    {
        using var session = documentStore.GetSession();

        var tracked = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address), cancellationToken);
        var trackedIsNew = tracked is null;
        tracked ??= new TrackedAddressDocument
            {
                Id = TrackedAddressDocument.GetId(address),
                EntityType = TrackedEntityType.Address,
                EntityId = address,
                Address = address,
                Name = name,
            };
        var status = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(address), cancellationToken);
        var statusIsNew = status is null;
        status ??= new TrackedAddressStatusDocument
            {
                Id = TrackedAddressStatusDocument.GetId(address),
                EntityType = TrackedEntityType.Address,
                EntityId = address,
                Address = address,
            };
        var legacy = await session.Query<WatchingAddress>()
            .FirstOrDefaultAsync(x => x.Address == address, cancellationToken);

        ApplyRegistration(tracked, tracked.Address, tracked.Name, name, historyMode);
        ApplyRegistration(status, status.Address, historyMode);

        if (legacy is null)
        {
            legacy = new WatchingAddress
            {
                Id = $"address/{address}",
                Address = address,
                Name = name,
            };

            await session.StoreAsync(legacy, legacy.Id, cancellationToken);
        }

        if (trackedIsNew)
            await session.StoreAsync(tracked, tracked.Id, cancellationToken);

        if (statusIsNew)
            await session.StoreAsync(status, status.Id, cancellationToken);

        await session.SaveChangesAsync(cancellationToken);
        var invalidationTags = cacheKeyFactory.GetTrackedAddressReadinessInvalidationTags([address]);
        invalidationTelemetry.Record(invalidationTags);
        await cacheInvalidationSink.InvalidateTagsAsync(invalidationTags, cancellationToken);
    }

    public async Task RegisterTokenAsync(
        string tokenId,
        string symbol,
        string historyMode = TrackedEntityHistoryMode.ForwardOnly,
        CancellationToken cancellationToken = default
    )
    {
        using var session = documentStore.GetSession();

        var tracked = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId), cancellationToken);
        var trackedIsNew = tracked is null;
        tracked ??= new TrackedTokenDocument
            {
                Id = TrackedTokenDocument.GetId(tokenId),
                EntityType = TrackedEntityType.Token,
                EntityId = tokenId,
                TokenId = tokenId,
                Symbol = symbol,
            };
        var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), cancellationToken);
        var statusIsNew = status is null;
        status ??= new TrackedTokenStatusDocument
            {
                Id = TrackedTokenStatusDocument.GetId(tokenId),
                EntityType = TrackedEntityType.Token,
                EntityId = tokenId,
                TokenId = tokenId,
            };
        var legacy = await session.Query<WatchingToken>()
            .FirstOrDefaultAsync(x => x.TokenId == tokenId, cancellationToken);

        ApplyRegistration(tracked, tracked.TokenId, tracked.Symbol, symbol, historyMode);
        ApplyRegistration(status, status.TokenId, historyMode);

        if (legacy is null)
        {
            legacy = new WatchingToken
            {
                Id = $"token/{tokenId}/{symbol}",
                TokenId = tokenId,
                Symbol = symbol,
            };

            await session.StoreAsync(legacy, legacy.Id, cancellationToken);
        }

        if (trackedIsNew)
            await session.StoreAsync(tracked, tracked.Id, cancellationToken);

        if (statusIsNew)
            await session.StoreAsync(status, status.Id, cancellationToken);

        await session.SaveChangesAsync(cancellationToken);
        var invalidationTags = cacheKeyFactory.GetTrackedTokenReadinessInvalidationTags([tokenId]);
        invalidationTelemetry.Record(invalidationTags);
        await cacheInvalidationSink.InvalidateTagsAsync(invalidationTags, cancellationToken);
    }

    public async Task<bool> RequestAddressFullHistoryAsync(string address, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var tracked = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address), cancellationToken);
        var status = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(address), cancellationToken);
        if (tracked is null || status is null || !tracked.Tracked || status.IsTombstoned)
            return false;

        PromoteToFullHistory(tracked);
        PromoteToFullHistory(status);
        tracked.SetUpdate();
        status.SetUpdate();

        await session.SaveChangesAsync(cancellationToken);
        var invalidationTags = cacheKeyFactory.GetTrackedAddressReadinessInvalidationTags([address]);
        invalidationTelemetry.Record(invalidationTags);
        await cacheInvalidationSink.InvalidateTagsAsync(invalidationTags, cancellationToken);
        return true;
    }

    public async Task<bool> RequestTokenFullHistoryAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var tracked = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId), cancellationToken);
        var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), cancellationToken);
        if (tracked is null || status is null || !tracked.Tracked || status.IsTombstoned)
            return false;

        PromoteToFullHistory(tracked);
        PromoteToFullHistory(status);
        tracked.SetUpdate();
        status.SetUpdate();

        await session.SaveChangesAsync(cancellationToken);
        var invalidationTags = cacheKeyFactory.GetTrackedTokenReadinessInvalidationTags([tokenId]);
        invalidationTelemetry.Record(invalidationTags);
        await cacheInvalidationSink.InvalidateTagsAsync(invalidationTags, cancellationToken);
        return true;
    }

    private static void ApplyRegistration(
        TrackedAddressDocument document,
        string address,
        string currentName,
        string requestedName,
        string historyMode
    )
    {
        document.Address = address;
        document.Name = string.IsNullOrWhiteSpace(requestedName) ? currentName : requestedName;
        ApplyRegistration(document, historyMode);
    }

    private static void ApplyRegistration(
        TrackedTokenDocument document,
        string tokenId,
        string currentSymbol,
        string requestedSymbol,
        string historyMode
    )
    {
        document.TokenId = tokenId;
        document.Symbol = string.IsNullOrWhiteSpace(requestedSymbol) ? currentSymbol : requestedSymbol;
        ApplyRegistration(document, historyMode);
    }

    private static void ApplyRegistration(TrackedAddressStatusDocument document, string address, string historyMode)
    {
        document.Address = address;
        ApplyRegistration(document, historyMode);
    }

    private static void ApplyRegistration(TrackedTokenStatusDocument document, string tokenId, string historyMode)
    {
        document.TokenId = tokenId;
        ApplyRegistration(document, historyMode);
    }

    private static void ApplyRegistration(TrackedEntityDocumentBase document, string historyMode)
    {
        var wasTombstoned = document.IsTombstoned;
        var normalizedHistoryMode = NormalizeHistoryMode(historyMode);

        document.Tracked = true;
        document.IsTombstoned = false;
        document.TombstonedAt = null;
        document.HistoryMode = normalizedHistoryMode;
        document.HistoryCoverage ??= new TrackedHistoryCoverage();
        document.HistoryCoverage.Mode = normalizedHistoryMode;

        if (wasTombstoned
            || string.IsNullOrWhiteSpace(document.LifecycleStatus)
            || string.Equals(document.LifecycleStatus, TrackedEntityLifecycleStatus.Paused, StringComparison.Ordinal)
            || string.Equals(document.LifecycleStatus, TrackedEntityLifecycleStatus.Failed, StringComparison.Ordinal))
        {
            document.LifecycleStatus = TrackedEntityLifecycleStatus.Registered;
            document.Readable = false;
            document.Authoritative = false;
            document.Degraded = false;
            document.LagBlocks = null;
            document.Progress = null;
        }

        if (string.IsNullOrWhiteSpace(document.HistoryReadiness)
            || string.Equals(document.HistoryReadiness, TrackedEntityHistoryReadiness.NotRequested, StringComparison.Ordinal)
            || string.Equals(document.HistoryReadiness, TrackedEntityHistoryReadiness.Degraded, StringComparison.Ordinal))
        {
            document.HistoryReadiness = string.Equals(normalizedHistoryMode, TrackedEntityHistoryMode.FullHistory, StringComparison.Ordinal)
                ? TrackedEntityHistoryReadiness.BackfillingFullHistory
                : TrackedEntityHistoryReadiness.NotRequested;
        }

        document.SetUpdate();
    }

    private static void PromoteToFullHistory(TrackedEntityDocumentBase document)
    {
        document.HistoryMode = TrackedEntityHistoryMode.FullHistory;
        document.HistoryCoverage ??= new TrackedHistoryCoverage();
        document.HistoryCoverage.Mode = TrackedEntityHistoryMode.FullHistory;

        if (!string.Equals(document.HistoryReadiness, TrackedEntityHistoryReadiness.FullHistoryLive, StringComparison.Ordinal))
            document.HistoryReadiness = TrackedEntityHistoryReadiness.BackfillingFullHistory;
    }

    private static string NormalizeHistoryMode(string historyMode)
        => string.Equals(historyMode, TrackedEntityHistoryMode.FullHistory, StringComparison.OrdinalIgnoreCase)
            ? TrackedEntityHistoryMode.FullHistory
            : TrackedEntityHistoryMode.ForwardOnly;
}
