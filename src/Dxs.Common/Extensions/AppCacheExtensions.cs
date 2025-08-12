using Dxs.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Nito.AsyncEx.Synchronous;

namespace Dxs.Common.Extensions;

public static class AppCacheExtensions
{
    private const string LockKeyPostfix = "$lock";
    private const string DateKeyPostfix = "$date";

    public static T GetOrAdd<TService, T>(this IAppCache<TService> cache, string key, Func<T> addItemFactory,
        TimeSpan? relativeExpiration = null, TimeSpan? slidingExpiration = null)
    {
        return cache.GetOrAddAsync(key, () => Task.FromResult(addItemFactory()),
            relativeExpiration: relativeExpiration, slidingExpiration: slidingExpiration
        ).WaitAndUnwrapException();
    }

    public static T GetOrAdd<TService, T>(this IAppCache<TService> cache, string key, T item,
        TimeSpan? relativeExpiration = null, TimeSpan? slidingExpiration = null)
    {
        return cache.GetOrAddAsync(key, () => Task.FromResult(item),
            relativeExpiration: relativeExpiration, slidingExpiration: slidingExpiration
        ).WaitAndUnwrapException();
    }

    public static Task<T> GetOrAddWithAgeAsync<TService, T>(this IAppCache<TService> cache, string key, Func<ICacheEntry, Task<T>> addItemFactory,
        TimeSpan maxAge)
    {
        if (maxAge < TimeSpan.Zero)
            throw new ArgumentException($"Value should be non-negative ({maxAge}).", nameof(maxAge));

        var (lockKey, dateKey) = ($"{key}:{LockKeyPostfix}", $"{key}:{DateKeyPostfix}");

        return cache.GetOrAddAsync(lockKey, async entry =>
        {
            T value;
            if (cache.TryGet<T>(key, out var oldValue) && cache.TryGet<DateTime>(dateKey, out var addedAt) && DateTime.UtcNow - addedAt <= maxAge)
            {
                value = oldValue;
            }
            else
            {
                value = await addItemFactory(entry);
                cache.Set(key, value);
                cache.Set(dateKey, DateTime.UtcNow);
            }

            entry.AbsoluteExpiration = DateTimeOffset.Now;
            return value;
        });
    }
}