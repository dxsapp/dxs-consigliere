using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Dto.Responses.Readiness;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Services.Impl;

public sealed class TrackedEntityReadinessService(IDocumentStore documentStore) : ITrackedEntityReadinessService
{
    public async Task<TrackedEntityReadinessResponse> GetAddressReadinessAsync(
        string address,
        CancellationToken cancellationToken = default
    )
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var status = await session.LoadAsync<TrackedAddressStatusDocument>(TrackedAddressStatusDocument.GetId(address), cancellationToken);
        if (status is not null)
            return Map(status);

        var tracked = await session.LoadAsync<TrackedAddressDocument>(TrackedAddressDocument.GetId(address), cancellationToken);
        return tracked is not null
            ? Map(tracked)
            : new TrackedEntityReadinessResponse
            {
                Tracked = false,
                EntityType = TrackedEntityType.Address,
                EntityId = address,
                LifecycleStatus = TrackedEntityLifecycleStatus.Registered,
                Readable = false,
                Authoritative = false,
                Degraded = false,
            };
    }

    public async Task<TrackedEntityReadinessResponse> GetTokenReadinessAsync(
        string tokenId,
        CancellationToken cancellationToken = default
    )
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();
        var status = await session.LoadAsync<TrackedTokenStatusDocument>(TrackedTokenStatusDocument.GetId(tokenId), cancellationToken);
        if (status is not null)
            return Map(status);

        var tracked = await session.LoadAsync<TrackedTokenDocument>(TrackedTokenDocument.GetId(tokenId), cancellationToken);
        return tracked is not null
            ? Map(tracked)
            : new TrackedEntityReadinessResponse
            {
                Tracked = false,
                EntityType = TrackedEntityType.Token,
                EntityId = tokenId,
                LifecycleStatus = TrackedEntityLifecycleStatus.Registered,
                Readable = false,
                Authoritative = false,
                Degraded = false,
            };
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
        IReadOnlyDictionary<string, TrackedAddressStatusDocument> addressStatuses = addressList.Length == 0
            ? new Dictionary<string, TrackedAddressStatusDocument>()
            : await session.LoadAsync<TrackedAddressStatusDocument>(addressList.Select(TrackedAddressStatusDocument.GetId), cancellationToken);
        IReadOnlyDictionary<string, TrackedTokenStatusDocument> tokenStatuses = tokenList.Length == 0
            ? new Dictionary<string, TrackedTokenStatusDocument>()
            : await session.LoadAsync<TrackedTokenStatusDocument>(tokenList.Select(TrackedTokenStatusDocument.GetId), cancellationToken);

        var blocked = addressStatuses.Values
            .Where(x => x is not null && !x.Readable)
            .Select(Map)
            .Concat(tokenStatuses.Values.Where(x => x is not null && !x.Readable).Select(Map))
            .ToArray();

        return blocked.Length == 0
            ? null
            : new TrackedEntityReadinessGateResponse { Entities = blocked };
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
        };
}
