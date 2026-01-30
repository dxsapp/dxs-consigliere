using LazyCache.Providers;

using Microsoft.Extensions.Caching.Memory;

namespace Dxs.Common.Extensions;

public class MemoryCacheProviderExtensions(MemoryCache cache) : MemoryCacheProvider(cache)
{
    public int Count => cache.Count;
}
