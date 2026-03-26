namespace Dxs.Common.Cache;

public readonly record struct ProjectionCacheKey(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ProjectionCacheTag(string Value)
{
    public override string ToString() => Value;
}

public sealed record ProjectionCacheDescriptor(
    ProjectionCacheKey Key,
    IReadOnlyCollection<ProjectionCacheTag> Tags
);

public sealed record ProjectionCacheStatsSnapshot(
    string Backend,
    bool Enabled,
    int Count,
    int? MaxEntries,
    long Hits,
    long Misses,
    long FactoryCalls,
    long InvalidatedKeys,
    long InvalidatedTags,
    long Evictions
)
{
    public double HitRatio
        => Hits + Misses == 0
            ? 0
            : (double)Hits / (Hits + Misses);
}

public sealed class ProjectionCacheEntryOptions
{
    public static ProjectionCacheEntryOptions Default { get; } = new();

    public IReadOnlyCollection<ProjectionCacheTag> Tags { get; init; } = [];
    public TimeSpan? SafetyTtl { get; init; }
    public long Size { get; init; } = 1;
}

public interface IProjectionReadCache
{
    int Count { get; }

    Task<T> GetOrCreateAsync<T>(
        ProjectionCacheKey key,
        ProjectionCacheEntryOptions options,
        Func<CancellationToken, Task<T>> valueFactory,
        CancellationToken cancellationToken = default);

    ValueTask InvalidateAsync(ProjectionCacheKey key, CancellationToken cancellationToken = default);

    ValueTask InvalidateTagsAsync(IEnumerable<ProjectionCacheTag> tags, CancellationToken cancellationToken = default);
}

public interface IProjectionCacheInvalidationSink
{
    ValueTask InvalidateTagsAsync(IEnumerable<ProjectionCacheTag> tags, CancellationToken cancellationToken = default);
}

public interface IProjectionReadCacheTelemetry
{
    ProjectionCacheStatsSnapshot GetSnapshot();
}

public sealed class InProcessProjectionReadCacheOptions
{
    public int MaxEntries { get; set; } = 10_000;
    public TimeSpan? DefaultSafetyTtl { get; set; }
}

public sealed class NoopProjectionReadCache : IProjectionReadCache, IProjectionCacheInvalidationSink, IProjectionReadCacheTelemetry
{
    public int Count => 0;

    public Task<T> GetOrCreateAsync<T>(
        ProjectionCacheKey key,
        ProjectionCacheEntryOptions options,
        Func<CancellationToken, Task<T>> valueFactory,
        CancellationToken cancellationToken = default)
        => valueFactory(cancellationToken);

    public ValueTask InvalidateAsync(ProjectionCacheKey key, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask InvalidateTagsAsync(IEnumerable<ProjectionCacheTag> tags, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ProjectionCacheStatsSnapshot GetSnapshot()
        => new(
            "disabled",
            false,
            0,
            null,
            0,
            0,
            0,
            0,
            0,
            0);
}
