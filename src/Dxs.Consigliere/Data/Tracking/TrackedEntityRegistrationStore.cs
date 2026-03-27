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
        IReadOnlyCollection<string>? trustedRoots = null,
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

        var normalizedTrustedRoots = NormalizeTrustedRoots(trustedRoots);
        ApplyRegistration(tracked, tracked.TokenId, tracked.Symbol, symbol, historyMode, normalizedTrustedRoots);
        ApplyRegistration(status, status.TokenId, historyMode, normalizedTrustedRoots);

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

    public async Task<bool> RequestTokenFullHistoryAsync(
        string tokenId,
        IReadOnlyCollection<string> trustedRoots,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedTrustedRoots = NormalizeTrustedRoots(trustedRoots);
        if (normalizedTrustedRoots.Length == 0)
            return false;

        using var session = documentStore.GetSession();
        var tracked = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId), cancellationToken);
        var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), cancellationToken);
        if (tracked is null || status is null || !tracked.Tracked || status.IsTombstoned)
            return false;

        PromoteToFullHistory(tracked, normalizedTrustedRoots);
        PromoteToFullHistory(status, normalizedTrustedRoots);
        tracked.SetUpdate();
        status.SetUpdate();

        await session.SaveChangesAsync(cancellationToken);
        var invalidationTags = cacheKeyFactory.GetTrackedTokenReadinessInvalidationTags([tokenId]);
        invalidationTelemetry.Record(invalidationTags);
        await cacheInvalidationSink.InvalidateTagsAsync(invalidationTags, cancellationToken);
        return true;
    }

    public async Task<bool> UntrackAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var tracked = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address), cancellationToken);
        var status = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(address), cancellationToken);
        if (tracked is null && status is null)
            return false;

        var legacy = await session.Query<WatchingAddress>()
            .Where(x => x.Address == address)
            .ToListAsync(cancellationToken);

        var tombstonedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (tracked is not null)
        {
            ApplyUntrack(tracked, tombstonedAt);
            tracked.SetUpdate();
        }

        if (status is not null)
        {
            ApplyUntrack(status, tombstonedAt);
            status.SetUpdate();
        }

        foreach (var entry in legacy)
            session.Delete(entry);

        await session.SaveChangesAsync(cancellationToken);
        var invalidationTags = cacheKeyFactory.GetTrackedAddressReadinessInvalidationTags([address]);
        invalidationTelemetry.Record(invalidationTags);
        await cacheInvalidationSink.InvalidateTagsAsync(invalidationTags, cancellationToken);
        return true;
    }

    public async Task<bool> UntrackTokenAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        using var session = documentStore.GetSession();
        var tracked = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId), cancellationToken);
        var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), cancellationToken);
        if (tracked is null && status is null)
            return false;

        var legacy = await session.Query<WatchingToken>()
            .Where(x => x.TokenId == tokenId)
            .ToListAsync(cancellationToken);

        var tombstonedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (tracked is not null)
        {
            ApplyUntrack(tracked, tombstonedAt);
            tracked.SetUpdate();
        }

        if (status is not null)
        {
            ApplyUntrack(status, tombstonedAt);
            status.SetUpdate();
        }

        foreach (var entry in legacy)
            session.Delete(entry);

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
        string historyMode,
        IReadOnlyCollection<string> trustedRoots
    )
    {
        document.TokenId = tokenId;
        document.Symbol = string.IsNullOrWhiteSpace(requestedSymbol) ? currentSymbol : requestedSymbol;
        ApplyRegistration(document, historyMode, trustedRoots);
    }

    private static void ApplyRegistration(TrackedAddressStatusDocument document, string address, string historyMode)
    {
        document.Address = address;
        ApplyRegistration(document, historyMode);
    }

    private static void ApplyRegistration(TrackedTokenStatusDocument document, string tokenId, string historyMode, IReadOnlyCollection<string> trustedRoots)
    {
        document.TokenId = tokenId;
        ApplyRegistration(document, historyMode, trustedRoots);
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

    private static void ApplyRegistration(TrackedTokenDocument document, string historyMode, IReadOnlyCollection<string> trustedRoots)
    {
        ApplyRegistration((TrackedEntityDocumentBase)document, historyMode);
        document.HistorySecurity = CreateOrResetSecurityState(document.HistorySecurity, trustedRoots);
        if (!string.Equals(document.HistoryMode, TrackedEntityHistoryMode.FullHistory, StringComparison.Ordinal))
            document.HistorySecurity = CreateOrResetSecurityState(document.HistorySecurity, []);
        document.SetUpdate();
    }

    private static void ApplyRegistration(TrackedTokenStatusDocument document, string historyMode, IReadOnlyCollection<string> trustedRoots)
    {
        ApplyRegistration((TrackedEntityDocumentBase)document, historyMode);
        document.HistorySecurity = CreateOrResetSecurityState(document.HistorySecurity, trustedRoots);
        if (!string.Equals(document.HistoryMode, TrackedEntityHistoryMode.FullHistory, StringComparison.Ordinal))
            document.HistorySecurity = CreateOrResetSecurityState(document.HistorySecurity, []);
        document.SetUpdate();
    }

    private static void PromoteToFullHistory(TrackedEntityDocumentBase document, IReadOnlyCollection<string>? trustedRoots = null)
    {
        document.HistoryMode = TrackedEntityHistoryMode.FullHistory;
        document.HistoryCoverage ??= new TrackedHistoryCoverage();
        document.HistoryCoverage.Mode = TrackedEntityHistoryMode.FullHistory;

        if (!string.Equals(document.HistoryReadiness, TrackedEntityHistoryReadiness.FullHistoryLive, StringComparison.Ordinal))
            document.HistoryReadiness = TrackedEntityHistoryReadiness.BackfillingFullHistory;
    }

    private static void PromoteToFullHistory(TrackedTokenDocument document, IReadOnlyCollection<string> trustedRoots)
    {
        PromoteToFullHistory((TrackedEntityDocumentBase)document, trustedRoots);
        document.HistorySecurity = CreateOrResetSecurityState(document.HistorySecurity, trustedRoots);
    }

    private static void PromoteToFullHistory(TrackedTokenStatusDocument document, IReadOnlyCollection<string> trustedRoots)
    {
        PromoteToFullHistory((TrackedEntityDocumentBase)document, trustedRoots);
        document.HistorySecurity = CreateOrResetSecurityState(document.HistorySecurity, trustedRoots);
    }

    private static string NormalizeHistoryMode(string historyMode)
        => string.Equals(historyMode, TrackedEntityHistoryMode.FullHistory, StringComparison.OrdinalIgnoreCase)
            ? TrackedEntityHistoryMode.FullHistory
            : TrackedEntityHistoryMode.ForwardOnly;

    private static string[] NormalizeTrustedRoots(IReadOnlyCollection<string>? trustedRoots)
        => (trustedRoots ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static TrackedTokenHistorySecurityState CreateOrResetSecurityState(
        TrackedTokenHistorySecurityState? existing,
        IReadOnlyCollection<string> trustedRoots
    )
    {
        var state = existing?.Clone() ?? new TrackedTokenHistorySecurityState();
        state.TrustedRoots = NormalizeTrustedRoots(trustedRoots);
        state.UnknownRootFindings = [];
        state.CompletedTrustedRootCount = 0;
        state.RootedHistorySecure = state.TrustedRoots.Length == 0;
        state.BlockingUnknownRoot = false;
        return state;
    }

    private static void ApplyUntrack(TrackedEntityDocumentBase document, long tombstonedAt)
    {
        document.Tracked = false;
        document.Readable = false;
        document.Authoritative = false;
        document.IsTombstoned = true;
        document.TombstonedAt = tombstonedAt;
        document.LifecycleStatus = TrackedEntityLifecycleStatus.Paused;
        document.Progress = null;
        document.LagBlocks = null;
    }
}
