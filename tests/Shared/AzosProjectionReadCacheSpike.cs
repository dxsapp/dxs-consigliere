using System.Collections.Concurrent;
using System.Threading;

using Azos.Apps;
using Azos.Pile;

using Dxs.Common.Cache;

namespace Dxs.Tests.Shared;

public sealed class AzosProjectionReadCacheSpike : IProjectionReadCache, IProjectionCacheInvalidationSink, IDisposable
{
    private readonly LocalCache _cache;
    private readonly ICacheTable<string> _table;
    private readonly ConcurrentDictionary<string, string[]> _entryTags = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _tagIndex = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new(StringComparer.Ordinal);
    private readonly TimeSpan? _defaultSafetyTtl;

    public AzosProjectionReadCacheSpike(int maxEntries = 10_000, TimeSpan? defaultSafetyTtl = null, string? tableName = null)
    {
        _defaultSafetyTtl = defaultSafetyTtl;
        _cache = new LocalCache(NOPApplication.Instance);
        _cache.Pile = new DefaultPile(_cache);
        _cache.Start();
        _cache.DefaultTableOptions = new TableOptions(tableName ?? $"projection-cache-{Guid.NewGuid():N}")
        {
            CollisionMode = CollisionMode.Durable,
            MaximumCapacity = maxEntries > 0 ? maxEntries : 10_000,
            DefaultMaxAgeSec = ToMaxAgeSeconds(defaultSafetyTtl) ?? 0
        };
        _table = _cache.GetOrCreateTable<string>(_cache.DefaultTableOptions.Name, StringComparer.Ordinal);
    }

    public int Count => _entryTags.Count;

    public async Task<T> GetOrCreateAsync<T>(
        ProjectionCacheKey key,
        ProjectionCacheEntryOptions options,
        Func<CancellationToken, Task<T>> valueFactory,
        CancellationToken cancellationToken = default)
    {
        if (TryGetValue(key, out T existing))
            return existing;

        var gate = _keyLocks.GetOrAdd(key.Value, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (TryGetValue(key, out existing))
                return existing;

            var value = await valueFactory(cancellationToken);
            SetValue(key, value, options ?? ProjectionCacheEntryOptions.Default);
            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    public ValueTask InvalidateAsync(ProjectionCacheKey key, CancellationToken cancellationToken = default)
    {
        _table.Remove(key.Value);
        Cleanup(key.Value);
        return ValueTask.CompletedTask;
    }

    public ValueTask InvalidateTagsAsync(IEnumerable<ProjectionCacheTag> tags, CancellationToken cancellationToken = default)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in tags ?? [])
        {
            if (!_tagIndex.TryGetValue(tag.Value, out var tagKeys))
                continue;

            foreach (var key in tagKeys.Keys)
                keys.Add(key);
        }

        foreach (var key in keys)
        {
            _table.Remove(key);
            Cleanup(key);
        }

        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _cache.Dispose();
        foreach (var gate in _keyLocks.Values)
            gate.Dispose();
    }

    private bool TryGetValue<T>(ProjectionCacheKey key, out T value)
    {
        if (_table.Get(key.Value) is ProjectionCacheValue cached)
        {
            value = cached.Value is null ? default! : (T)cached.Value;
            return true;
        }

        value = default!;
        return false;
    }

    private void SetValue<T>(ProjectionCacheKey key, T value, ProjectionCacheEntryOptions options)
    {
        var tags = NormalizeTags(options.Tags);
        _entryTags[key.Value] = tags;
        foreach (var tag in tags)
        {
            var tagKeys = _tagIndex.GetOrAdd(tag, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
            tagKeys[key.Value] = 0;
        }

        _table.Put(
            key.Value,
            new ProjectionCacheValue(value),
            ToMaxAgeSeconds(options.SafetyTtl ?? _defaultSafetyTtl));
    }

    private void Cleanup(string key)
    {
        if (!_entryTags.TryRemove(key, out var tags))
            return;

        foreach (var tag in tags)
        {
            if (!_tagIndex.TryGetValue(tag, out var tagKeys))
                continue;

            tagKeys.TryRemove(key, out _);
            if (tagKeys.IsEmpty)
                _tagIndex.TryRemove(tag, out _);
        }
    }

    private static string[] NormalizeTags(IEnumerable<ProjectionCacheTag> tags)
        => (tags ?? [])
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static int? ToMaxAgeSeconds(TimeSpan? ttl)
        => !ttl.HasValue || ttl.Value <= TimeSpan.Zero
            ? null
            : Math.Max(1, (int)Math.Ceiling(ttl.Value.TotalSeconds));

    [Serializable]
    private sealed class ProjectionCacheValue
    {
        public ProjectionCacheValue(object? value)
        {
            Value = value;
        }

        public object? Value { get; }
    }
}
