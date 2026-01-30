using Dxs.Common.Extensions;
using Dxs.Common.Interfaces;

using LazyCache;

using Microsoft.Extensions.Caching.Memory;

using TrustMargin.Common.Extensions;

namespace Dxs.Common.Cache;

public class LazyCacheAppCache<TService> : IAppCache<TService>
{
    private readonly struct Entry<T>
    {
        public T Value { get; }
        public bool HasValue { get; }

        public Entry(T value) : this()
        {
            Value = value;
            HasValue = true;
        }
    }

    private readonly CachingService _cachingService = new(new MemoryCacheProviderExtensions(new MemoryCache(new MemoryCacheOptions())));

    public int Count => ((MemoryCacheProviderExtensions)_cachingService.CacheProvider).Count;

    public bool TryGet<T>(string key, out T value)
    {
        var entry = _cachingService.Get<Entry<T>>(key);
        value = entry.Value;

        return entry.HasValue;
    }

    public T Get<T>(string key) =>
        TryGet(key, out T value) ? value : throw new KeyNotFoundException($"Key \"{key}\" is not present in the cache.");

    public void Set<T>(string key, T item, TimeSpan? relativeExpiration = null)
    {
        var options = relativeExpiration != null
            ? new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = relativeExpiration }
            : null;

        _cachingService.Add(key, new Entry<T>(item), options);
    }

    public void Set<T>(string key, T item, DateTime absolutExpiration)
    {
        _cachingService.Add(
            key,
            new Entry<T>(item),
            new MemoryCacheEntryOptions { AbsoluteExpiration = new DateTimeOffset(absolutExpiration) }
        );
    }

    public T GetOrAdd<T>(string key, Func<T> addItemFactory, TimeSpan? relativeExpiration = null, TimeSpan? slidingExpiration = null)
        => _cachingService.GetOrAdd(
            key,
            addItemFactory,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = relativeExpiration,
                SlidingExpiration = slidingExpiration
            }
        );

    public async Task<T> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T>> addItemFactory)
    {
        var entry = await _cachingService.GetOrAddAsync(key, async e => new Entry<T>(await addItemFactory(e)));
        return entry.Value;
    }

    public async Task<T> GetOrAddAsync<T>(
        string key,
        Func<Task<T>> addItemFactory,
        TimeSpan? relativeExpiration = null,
        TimeSpan? slidingExpiration = null
    )
    {
        Entry<T> entry;

        if (relativeExpiration != null && slidingExpiration != null)
        {
            entry = await _cachingService.GetOrAddAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = relativeExpiration;
                entry.SlidingExpiration = slidingExpiration;
                return new Entry<T>(await addItemFactory());
            });
        }

        else if (relativeExpiration != null)
        {
            entry = await _cachingService.GetOrAddAsync(key, async () => new Entry<T>(await addItemFactory()),
                expires: DateTimeOffset.Now + (TimeSpan)relativeExpiration
            );
        }

        else if (slidingExpiration != null)
        {
            entry = await _cachingService.GetOrAddAsync(key, async () => new Entry<T>(await addItemFactory()),
                slidingExpiration: (TimeSpan)slidingExpiration
            );
        }

        else
        {
            entry = await _cachingService.GetOrAddAsync(key, async () => new Entry<T>(await addItemFactory()));
        }

        return entry.Value;
    }

    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> addItemFactory,
        DateTimeOffset? absoluteExpiration)
    {
        Entry<T> entry;

        if (absoluteExpiration != null)
        {
            entry = await _cachingService.GetOrAddAsync(key, async () => new Entry<T>(await addItemFactory()),
                expires: (DateTimeOffset)absoluteExpiration
            );
        }

        else
        {
            entry = await _cachingService.GetOrAddAsync(key, async () => new Entry<T>(await addItemFactory()));
        }

        return entry.Value;
    }

    public void Remove(string key) =>
        _cachingService.Remove(key);
}
