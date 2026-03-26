using Dxs.Common.Cache;
using Dxs.Tests.Shared;

namespace Dxs.Consigliere.Tests.Data.Cache;

public class AzosProjectionReadCacheTests
{
    [Fact]
    public async Task InvalidateTagsAsync_RemovesSharedAzosEntries()
    {
        using var cache = new AzosProjectionReadCacheSpike(128, TimeSpan.FromMinutes(1), "cache-tests");
        var invalidationSink = (IProjectionCacheInvalidationSink)cache;
        var calls = 0;

        await cache.GetOrCreateAsync(
            new ProjectionCacheKey("token-history|token-1|page=1"),
            new ProjectionCacheEntryOptions
            {
                Tags =
                [
                    new ProjectionCacheTag("token-history:token-1")
                ]
            },
            _ =>
            {
                calls++;
                return Task.FromResult(new[] { "page-1" });
            });

        await cache.GetOrCreateAsync(
            new ProjectionCacheKey("token-history|token-1|page=2"),
            new ProjectionCacheEntryOptions
            {
                Tags =
                [
                    new ProjectionCacheTag("token-history:token-1")
                ]
            },
            _ =>
            {
                calls++;
                return Task.FromResult(new[] { "page-2" });
            });

        Assert.Equal(2, calls);

        await invalidationSink.InvalidateTagsAsync([new ProjectionCacheTag("token-history:token-1")]);

        await cache.GetOrCreateAsync(
            new ProjectionCacheKey("token-history|token-1|page=1"),
            ProjectionCacheEntryOptions.Default,
            _ =>
            {
                calls++;
                return Task.FromResult(new[] { "page-1-rebuilt" });
            });

        await cache.GetOrCreateAsync(
            new ProjectionCacheKey("token-history|token-1|page=2"),
            ProjectionCacheEntryOptions.Default,
            _ =>
            {
                calls++;
                return Task.FromResult(new[] { "page-2-rebuilt" });
            });

        Assert.Equal(4, calls);
    }

    [Fact]
    public async Task GetOrCreateAsync_CachesNullDocumentResults()
    {
        using var cache = new AzosProjectionReadCacheSpike(64, null, "cache-tests-null");
        var calls = 0;

        var first = await cache.GetOrCreateAsync<string?>(
            new ProjectionCacheKey("token-state|missing"),
            new ProjectionCacheEntryOptions
            {
                Tags =
                [
                    new ProjectionCacheTag("token-state:missing")
                ]
            },
            _ =>
            {
                calls++;
                return Task.FromResult<string?>(null);
            });

        var second = await cache.GetOrCreateAsync<string?>(
            new ProjectionCacheKey("token-state|missing"),
            ProjectionCacheEntryOptions.Default,
            _ =>
            {
                calls++;
                return Task.FromResult("should-not-run");
            });

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(1, calls);
    }
}
