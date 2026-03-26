using Microsoft.Extensions.Caching.Memory;

namespace Dxs.Common.Cache;

internal sealed class InProcessProjectionReadCache : TagIndexedProjectionReadCacheBase
{
    private readonly MemoryCache _cache;
    private readonly TimeSpan? _defaultSafetyTtl;
    private readonly int _maxEntries;

    public InProcessProjectionReadCache(InProcessProjectionReadCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _defaultSafetyTtl = options.DefaultSafetyTtl;
        _maxEntries = options.MaxEntries > 0 ? options.MaxEntries : 10_000;
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = _maxEntries
        });
    }

    protected override bool TryGetValue<T>(ProjectionCacheKey key, out T value)
    {
        if (_cache.TryGetValue(key.Value, out var existing)
            && TryUnboxValue(existing, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    protected override void SetValueCore(
        ProjectionCacheKey key,
        ProjectionCacheBox value,
        ProjectionCacheEntryOptions options,
        long version)
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            Size = options.Size > 0 ? options.Size : 1
        };

        var effectiveTtl = options.SafetyTtl ?? _defaultSafetyTtl;
        if (effectiveTtl.HasValue && effectiveTtl.Value > TimeSpan.Zero)
            cacheOptions.AbsoluteExpirationRelativeToNow = effectiveTtl;

        cacheOptions.RegisterPostEvictionCallback(static (evictedKey, _, _, state) =>
        {
            if (evictedKey is string keyString && state is EvictionState evictionState)
                evictionState.Owner.OnBackendEvicted(keyString, evictionState.Version);
        }, new EvictionState(this, version));

        _cache.Set(key.Value, value, cacheOptions);
    }

    protected override void RemoveValue(ProjectionCacheKey key)
        => _cache.Remove(key.Value);

    protected override void DisposeCore()
        => _cache.Dispose();

    protected override string GetBackendName() => "memory";

    protected override int? GetMaxEntries() => _maxEntries;

    private sealed record EvictionState(InProcessProjectionReadCache Owner, long Version);
}
