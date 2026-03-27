using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Dto.Responses.History;
using Dxs.Consigliere.Dto.Responses.Readiness;

namespace Dxs.Consigliere.Data.Tracking;

internal static class TrackedEntityReadinessMapper
{
    public static TrackedEntityReadinessResponse Map(TrackedEntityDocumentBase document)
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

    public static TrackedEntityReadinessResponse CreateUntrackedAddress(string address)
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

    public static TrackedEntityReadinessResponse CreateUntrackedToken(string tokenId)
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

    public static TrackedHistoryStatusResponse MapHistory(TrackedEntityDocumentBase document)
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
                },
            RootedToken = document switch
            {
                TrackedTokenDocument token => MapRootedToken(token.HistorySecurity),
                TrackedTokenStatusDocument status => MapRootedToken(status.HistorySecurity),
                _ => null
            }
        };

    public static RootedTokenHistoryStatusResponse MapRootedToken(TrackedTokenHistorySecurityState state)
        => state is null
            ? null
            : new RootedTokenHistoryStatusResponse
            {
                TrustedRoots = state.TrustedRoots?.ToArray() ?? [],
                TrustedRootCount = state.TrustedRoots?.Length ?? 0,
                CompletedTrustedRootCount = state.CompletedTrustedRootCount,
                UnknownRootFindingCount = state.UnknownRootFindings?.Length ?? 0,
                RootedHistorySecure = state.RootedHistorySecure,
                BlockingUnknownRoot = state.BlockingUnknownRoot,
                UnknownRootFindings = state.UnknownRootFindings?.ToArray() ?? []
            };

    public static TrackedHistoryStatusResponse CreateDefaultHistoryStatus()
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
