using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;

namespace Dxs.Common.Interfaces;

[SuppressMessage("ReSharper", "UnusedTypeParameter")]
public interface IAppCache<out TService>
{
    int Count { get; }

    bool TryGet<T>(string key, out T value);

    T Get<T>(string key);

    void Set<T>(string key, T item, TimeSpan? relativeExpiration = null);
        
    void Set<T>(string key, T item, DateTime absolutExpiration);
        
    T GetOrAdd<T>(
        string key, 
        Func<T> addItemFactory,
        TimeSpan? relativeExpiration = null, 
        TimeSpan? slidingExpiration = null
    );

    Task<T> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T>> addItemFactory);

    Task<T> GetOrAddAsync<T>(
        string key, 
        Func<Task<T>> addItemFactory,
        TimeSpan? relativeExpiration = null, 
        TimeSpan? slidingExpiration = null
    );

    Task<T> GetOrAddAsync<T>(
        string key,
        Func<Task<T>> addItemFactory,
        DateTimeOffset? absoluteExpiration
    );

    void Remove(string key);
}