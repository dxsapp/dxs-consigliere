namespace Dxs.Consigliere.Dto.Responses;

public sealed class ProjectionCacheStatusResponse
{
    public bool Enabled { get; set; }
    public string Backend { get; set; }
    public int Count { get; set; }
    public int? MaxEntries { get; set; }
    public long Hits { get; set; }
    public long Misses { get; set; }
    public long FactoryCalls { get; set; }
    public long InvalidatedKeys { get; set; }
    public long InvalidatedTags { get; set; }
    public long Evictions { get; set; }
    public double HitRatio { get; set; }
    public ProjectionCacheInvalidationTelemetryResponse Invalidation { get; set; }
    public ProjectionLagResponse ProjectionLag { get; set; }
    public HistoryEnvelopeBackfillStatusResponse HistoryEnvelopeBackfill { get; set; }
}

public sealed class ProjectionCacheInvalidationTelemetryResponse
{
    public long Calls { get; set; }
    public long Tags { get; set; }
    public DateTimeOffset? LastInvalidatedAt { get; set; }
    public ProjectionCacheInvalidationDomainResponse[] Domains { get; set; } = [];
}

public sealed class ProjectionCacheInvalidationDomainResponse
{
    public string Domain { get; set; }
    public long Calls { get; set; }
    public long Tags { get; set; }
    public DateTimeOffset? LastInvalidatedAt { get; set; }
}

public sealed class ProjectionLagResponse
{
    public long JournalTailSequence { get; set; }
    public ProjectionLagItemResponse Address { get; set; }
    public ProjectionLagItemResponse Token { get; set; }
    public ProjectionLagItemResponse TxLifecycle { get; set; }
}

public sealed class ProjectionLagItemResponse
{
    public string Projection { get; set; }
    public long CheckpointSequence { get; set; }
    public long Lag { get; set; }
}

public sealed class HistoryEnvelopeBackfillStatusResponse
{
    public long PendingCount { get; set; }
    public long LastBatchScanned { get; set; }
    public long LastBatchRewritten { get; set; }
    public long LastBatchMissingTransactions { get; set; }
    public long LastTouchedSequence { get; set; }
    public DateTimeOffset? LastRunStartedAt { get; set; }
    public DateTimeOffset? LastRunCompletedAt { get; set; }
}
