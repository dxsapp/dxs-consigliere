using Dxs.Common.Cache;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Dto.Responses.History;
using Dxs.Consigliere.Dto.Responses.Readiness;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Services.Impl;

public sealed class TrackedEntityReadinessService(
    IDocumentStore documentStore,
    IProjectionReadCache projectionReadCache,
    IProjectionReadCacheKeyFactory cacheKeyFactory
) : ITrackedEntityReadinessService
{
    public TrackedEntityReadinessService(IDocumentStore documentStore)
        : this(documentStore, new NoopProjectionReadCache(), new ProjectionReadCacheKeyFactory())
    {
    }

    public async Task<TrackedEntityReadinessResponse> GetAddressReadinessAsync(
        string address,
        CancellationToken cancellationToken = default
    )
    {
        var descriptor = cacheKeyFactory.CreateTrackedAddressReadiness(address);
        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            new ProjectionCacheEntryOptions { Tags = descriptor.Tags },
            async ct =>
            {
                using var session = documentStore.GetNoCacheNoTrackingSession();
                var status = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(address), ct);
                if (status is not null)
                    return Map(status);

                var tracked = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address), ct);
                return tracked is not null
                    ? Map(tracked)
                    : CreateUntrackedAddress(address);
            },
            cancellationToken);
    }

    public async Task<TrackedEntityReadinessResponse> GetTokenReadinessAsync(
        string tokenId,
        CancellationToken cancellationToken = default
    )
    {
        var descriptor = cacheKeyFactory.CreateTrackedTokenReadiness(tokenId);
        return await projectionReadCache.GetOrCreateAsync(
            descriptor.Key,
            new ProjectionCacheEntryOptions { Tags = descriptor.Tags },
            async ct =>
            {
                using var session = documentStore.GetNoCacheNoTrackingSession();
                var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), ct);
                if (status is not null)
                    return Map(status);

                var tracked = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId), ct);
                return tracked is not null
                    ? Map(tracked)
                    : CreateUntrackedToken(tokenId);
            },
            cancellationToken);
    }

    public async Task<TrackedEntityReadinessGateResponse> GetBlockingReadinessAsync(
        IEnumerable<string> addresses,
        IEnumerable<string> tokenIds,
        CancellationToken cancellationToken = default
    )
    {
        var addressList = addresses?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        var tokenList = tokenIds?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        if (addressList.Length == 0 && tokenList.Length == 0)
            return null;

        using var session = documentStore.GetNoCacheNoTrackingSession();
        var blocked = new List<TrackedEntityReadinessResponse>();

        foreach (var address in addressList)
        {
            var readiness = await LoadAddressReadinessAsync(session, address, cancellationToken);
            if (!readiness.Tracked || !readiness.Readable)
                blocked.Add(readiness);
        }

        foreach (var tokenId in tokenList)
        {
            var readiness = await LoadTokenReadinessAsync(session, tokenId, cancellationToken);
            if (!readiness.Tracked || !readiness.Readable)
                blocked.Add(readiness);
        }

        if (blocked.Count == 0)
            return null;

        return new TrackedEntityReadinessGateResponse
        {
            Code = blocked.Any(x => !x.Tracked) ? "not_tracked" : "scope_not_ready",
            Entities = blocked.ToArray()
        };
    }

    public async Task<TrackedEntityReadinessGateResponse> GetBlockingHistoryReadinessAsync(
        IEnumerable<string> addresses,
        IEnumerable<string> tokenIds,
        bool acceptPartialHistory,
        CancellationToken cancellationToken = default
    )
    {
        var addressList = addresses?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        var tokenList = tokenIds?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        if (addressList.Length == 0 && tokenList.Length == 0)
            return null;

        using var session = documentStore.GetNoCacheNoTrackingSession();
        var blocked = new List<TrackedEntityReadinessResponse>();
        var gateCode = "history_not_ready";

        foreach (var address in addressList)
        {
            var readiness = await LoadAddressReadinessAsync(session, address, cancellationToken);
            if (IsHistoryAllowed(readiness, acceptPartialHistory, out var code))
                continue;

            gateCode = PrioritizeGateCode(gateCode, code);
            blocked.Add(readiness);
        }

        foreach (var tokenId in tokenList)
        {
            var readiness = await LoadTokenReadinessAsync(session, tokenId, cancellationToken);
            if (IsHistoryAllowed(readiness, acceptPartialHistory, out var code))
                continue;

            gateCode = PrioritizeGateCode(gateCode, code);
            blocked.Add(readiness);
        }

        return blocked.Count == 0
            ? null
            : new TrackedEntityReadinessGateResponse
            {
                Code = gateCode,
                Entities = blocked.ToArray()
            };
    }

    private static TrackedEntityReadinessResponse Map(TrackedEntityDocumentBase document)
        => new()
        {
            Tracked = document.Tracked,
            EntityType = document.EntityType,
            EntityId = document.EntityId,
            LifecycleStatus = document.LifecycleStatus,
            Readable = document.Readable,
            Authoritative = document.Authoritative,
            Degraded = document.Degraded,
            LagBlocks = document.LagBlocks,
            Progress = document.Progress,
            History = MapHistory(document),
        };

    private static TrackedEntityReadinessResponse CreateUntrackedAddress(string address)
        => new()
        {
            Tracked = false,
            EntityType = TrackedEntityType.Address,
            EntityId = address,
            LifecycleStatus = TrackedEntityLifecycleStatus.Registered,
            Readable = false,
            Authoritative = false,
            Degraded = false,
            History = CreateDefaultHistoryStatus(),
        };

    private static TrackedEntityReadinessResponse CreateUntrackedToken(string tokenId)
        => new()
        {
            Tracked = false,
            EntityType = TrackedEntityType.Token,
            EntityId = tokenId,
            LifecycleStatus = TrackedEntityLifecycleStatus.Registered,
            Readable = false,
            Authoritative = false,
            Degraded = false,
            History = CreateDefaultHistoryStatus(),
        };

    private async Task<TrackedEntityReadinessResponse> LoadAddressReadinessAsync(
        IAsyncDocumentSession session,
        string address,
        CancellationToken cancellationToken
    )
    {
        var status = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(address), cancellationToken);
        if (status is not null)
            return Map(status);

        var tracked = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address), cancellationToken);
        return tracked is not null
            ? Map(tracked)
            : CreateUntrackedAddress(address);
    }

    private async Task<TrackedEntityReadinessResponse> LoadTokenReadinessAsync(
        IAsyncDocumentSession session,
        string tokenId,
        CancellationToken cancellationToken
    )
    {
        var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), cancellationToken);
        if (status is not null)
            return Map(status);

        var tracked = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId), cancellationToken);
        return tracked is not null
            ? Map(tracked)
            : CreateUntrackedToken(tokenId);
    }

    private static bool IsHistoryAllowed(
        TrackedEntityReadinessResponse readiness,
        bool acceptPartialHistory,
        out string gateCode
    )
    {
        if (!readiness.Tracked)
        {
            gateCode = "not_tracked";
            return false;
        }

        var historyReadiness = readiness.History?.HistoryReadiness ?? TrackedEntityHistoryReadiness.NotRequested;
        if (string.Equals(historyReadiness, TrackedEntityHistoryReadiness.FullHistoryLive, StringComparison.Ordinal))
        {
            gateCode = null;
            return true;
        }

        if (!acceptPartialHistory)
        {
            gateCode = string.Equals(historyReadiness, TrackedEntityHistoryReadiness.NotRequested, StringComparison.Ordinal)
                ? "history_not_requested"
                : "partial_history_opt_in_required";
            return false;
        }

        if (string.Equals(historyReadiness, TrackedEntityHistoryReadiness.ForwardLive, StringComparison.Ordinal)
            || string.Equals(historyReadiness, TrackedEntityHistoryReadiness.BackfillingFullHistory, StringComparison.Ordinal))
        {
            gateCode = null;
            return true;
        }

        gateCode = string.Equals(historyReadiness, TrackedEntityHistoryReadiness.Degraded, StringComparison.Ordinal)
            ? "history_degraded"
            : "history_not_requested";
        return false;
    }

    private static string PrioritizeGateCode(string current, string candidate)
    {
        if (string.Equals(candidate, "not_tracked", StringComparison.Ordinal))
            return candidate;

        if (string.Equals(candidate, "history_degraded", StringComparison.Ordinal)
            && !string.Equals(current, "not_tracked", StringComparison.Ordinal))
            return candidate;

        if (string.Equals(candidate, "history_not_requested", StringComparison.Ordinal)
            && !string.Equals(current, "not_tracked", StringComparison.Ordinal)
            && !string.Equals(current, "history_degraded", StringComparison.Ordinal))
            return candidate;

        return current;
    }

    private static TrackedHistoryStatusResponse MapHistory(TrackedEntityDocumentBase document)
        => new()
        {
            HistoryReadiness = document.HistoryReadiness,
            Coverage = new TrackedHistoryCoverageResponse
            {
                Mode = document.HistoryCoverage?.Mode ?? document.HistoryMode,
                FullCoverage = document.HistoryCoverage?.FullCoverage ?? false,
                AuthoritativeFromBlockHeight = document.HistoryCoverage?.AuthoritativeFromBlockHeight,
                AuthoritativeFromObservedAt = document.HistoryCoverage?.AuthoritativeFromObservedAt,
            },
            BackfillStatus = string.IsNullOrWhiteSpace(document.HistoryBackfillStatus)
                ? null
                : new TrackedHistoryBackfillStatusResponse
                {
                    Status = document.HistoryBackfillStatus,
                    RequestedAt = document.HistoryBackfillRequestedAt,
                    StartedAt = document.HistoryBackfillStartedAt,
                    LastProgressAt = document.HistoryBackfillLastProgressAt,
                    CompletedAt = document.HistoryBackfillCompletedAt,
                    ItemsScanned = document.HistoryBackfillItemsScanned,
                    ItemsApplied = document.HistoryBackfillItemsApplied,
                    ErrorCode = document.HistoryBackfillErrorCode,
                }
        };

    private static TrackedHistoryStatusResponse CreateDefaultHistoryStatus()
        => new()
        {
            HistoryReadiness = TrackedEntityHistoryReadiness.NotRequested,
            Coverage = new TrackedHistoryCoverageResponse
            {
                Mode = TrackedEntityHistoryMode.ForwardOnly,
                FullCoverage = false,
            }
        };
}
