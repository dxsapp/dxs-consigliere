using System.Collections.Concurrent;
using System.Threading;

namespace Dxs.Common.Cache;

internal abstract class TagIndexedProjectionReadCacheBase : IProjectionReadCache, IProjectionCacheInvalidationSink, IDisposable
{
    private readonly ConcurrentDictionary<string, ProjectionCacheEntryMetadata> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _tagIndex = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new(StringComparer.Ordinal);
    private long _nextVersion;

    public int Count => _entries.Count;

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
        RemoveValue(key);
        Cleanup(key.Value, null);
        return ValueTask.CompletedTask;
    }

    public ValueTask InvalidateTagsAsync(IEnumerable<ProjectionCacheTag> tags, CancellationToken cancellationToken = default)
    {
        if (tags is null)
            return ValueTask.CompletedTask;

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag.Value))
                continue;

            if (!_tagIndex.TryGetValue(tag.Value, out var tagKeys))
                continue;

            foreach (var key in tagKeys.Keys)
                keys.Add(key);
        }

        foreach (var key in keys)
        {
            RemoveValue(new ProjectionCacheKey(key));
            Cleanup(key, null);
        }

        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        DisposeCore();
        foreach (var gate in _keyLocks.Values)
            gate.Dispose();
    }

    protected abstract bool TryGetValue<T>(ProjectionCacheKey key, out T value);

    protected abstract void SetValueCore(
        ProjectionCacheKey key,
        ProjectionCacheBox value,
        ProjectionCacheEntryOptions options,
        long version);

    protected abstract void RemoveValue(ProjectionCacheKey key);

    protected virtual void DisposeCore()
    {
    }

    protected void OnBackendEvicted(string key, long version)
        => Cleanup(key, version);

    private void SetValue<T>(ProjectionCacheKey key, T value, ProjectionCacheEntryOptions options)
    {
        var normalizedTags = NormalizeTags(options.Tags);
        var version = Interlocked.Increment(ref _nextVersion);
        var metadata = new ProjectionCacheEntryMetadata(version, normalizedTags);
        _entries[key.Value] = metadata;

        foreach (var tag in normalizedTags)
        {
            var tagKeys = _tagIndex.GetOrAdd(tag, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
            tagKeys[key.Value] = 0;
        }

        SetValueCore(key, new ProjectionCacheBox(value), options, version);
    }

    private void Cleanup(string key, long? expectedVersion)
    {
        if (!_entries.TryGetValue(key, out var metadata))
            return;

        if (expectedVersion.HasValue && metadata.Version != expectedVersion.Value)
            return;

        if (!_entries.TryRemove(new KeyValuePair<string, ProjectionCacheEntryMetadata>(key, metadata)))
            return;

        foreach (var tag in metadata.Tags)
        {
            if (!_tagIndex.TryGetValue(tag, out var tagKeys))
                continue;

            tagKeys.TryRemove(key, out _);
            if (tagKeys.IsEmpty)
                _tagIndex.TryRemove(tag, out _);
        }
    }

    protected static bool TryUnboxValue<T>(object raw, out T value)
    {
        if (raw is ProjectionCacheBox box)
        {
            value = box.Value is null ? default : (T)box.Value;
            return true;
        }

        value = default;
        return false;
    }

    private static string[] NormalizeTags(IEnumerable<ProjectionCacheTag> tags)
        => (tags ?? [])
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    [Serializable]
    protected sealed class ProjectionCacheBox
    {
        public ProjectionCacheBox(object value)
        {
            Value = value;
        }

        public object Value { get; }
    }

    private sealed record ProjectionCacheEntryMetadata(long Version, string[] Tags);
}
