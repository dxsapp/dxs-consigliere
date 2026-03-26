using Dxs.Common.Cache;
using Dxs.Consigliere.Data.Cache;

namespace Dxs.Consigliere.Dto.Responses;

internal static class ProjectionCacheStatusResponseFactory
{
    public static ProjectionCacheStatusResponse Build(
        ProjectionCacheStatsSnapshot snapshot,
        ProjectionCacheRuntimeStatusSnapshot runtime,
        bool enabled
    )
        => new()
        {
            Enabled = enabled,
            Backend = snapshot.Backend,
            Count = snapshot.Count,
            MaxEntries = snapshot.MaxEntries,
            Hits = snapshot.Hits,
            Misses = snapshot.Misses,
            FactoryCalls = snapshot.FactoryCalls,
            InvalidatedKeys = snapshot.InvalidatedKeys,
            InvalidatedTags = snapshot.InvalidatedTags,
            Evictions = snapshot.Evictions,
            HitRatio = snapshot.HitRatio,
            Invalidation = new ProjectionCacheInvalidationTelemetryResponse
            {
                Calls = runtime.Invalidation.Calls,
                Tags = runtime.Invalidation.Tags,
                LastInvalidatedAt = runtime.Invalidation.LastInvalidatedAt,
                Domains = runtime.Invalidation.Domains
                    .Select(x => new ProjectionCacheInvalidationDomainResponse
                    {
                        Domain = x.Domain,
                        Calls = x.Calls,
                        Tags = x.Tags,
                        LastInvalidatedAt = x.LastInvalidatedAt
                    })
                    .ToArray()
            },
            ProjectionLag = new ProjectionLagResponse
            {
                JournalTailSequence = runtime.ProjectionLag.JournalTailSequence,
                Address = BuildProjectionLagItem(runtime.ProjectionLag.Address),
                Token = BuildProjectionLagItem(runtime.ProjectionLag.Token),
                TxLifecycle = BuildProjectionLagItem(runtime.ProjectionLag.TxLifecycle)
            },
            HistoryEnvelopeBackfill = new HistoryEnvelopeBackfillStatusResponse
            {
                PendingCount = runtime.HistoryEnvelopeBackfill.PendingCount,
                LastBatchScanned = runtime.HistoryEnvelopeBackfill.LastBatchScanned,
                LastBatchRewritten = runtime.HistoryEnvelopeBackfill.LastBatchRewritten,
                LastBatchMissingTransactions = runtime.HistoryEnvelopeBackfill.LastBatchMissingTransactions,
                LastTouchedSequence = runtime.HistoryEnvelopeBackfill.LastTouchedSequence,
                LastRunStartedAt = runtime.HistoryEnvelopeBackfill.LastRunStartedAt,
                LastRunCompletedAt = runtime.HistoryEnvelopeBackfill.LastRunCompletedAt
            }
        };

    private static ProjectionLagItemResponse BuildProjectionLagItem(ProjectionLagItemSnapshot snapshot)
        => new()
        {
            Projection = snapshot.Projection,
            CheckpointSequence = snapshot.CheckpointSequence,
            Lag = snapshot.Lag
        };
}
