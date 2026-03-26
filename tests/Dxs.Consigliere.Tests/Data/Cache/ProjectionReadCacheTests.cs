using Dxs.Common.Cache;

using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Consigliere.Tests.Data.Cache;

public class ProjectionReadCacheTests
{
    [Fact]
    public async Task InvalidateTagsAsync_RemovesAllEntriesForSharedTag()
    {
        var services = new ServiceCollection();
        services.AddProjectionReadCache(
            options =>
            {
                options.MaxEntries = 128;
                options.DefaultSafetyTtl = TimeSpan.FromMinutes(1);
            });

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IProjectionReadCache>();
        var invalidationSink = provider.GetRequiredService<IProjectionCacheInvalidationSink>();
        var calls = 0;

        await cache.GetOrCreateAsync(
            new ProjectionCacheKey("address-history|addr-1|page=1"),
            new ProjectionCacheEntryOptions
            {
                Tags =
                [
                    new ProjectionCacheTag("address-history:addr-1")
                ]
            },
            _ =>
            {
                calls++;
                return Task.FromResult("history-page-1");
            });

        await cache.GetOrCreateAsync(
            new ProjectionCacheKey("address-history|addr-1|page=2"),
            new ProjectionCacheEntryOptions
            {
                Tags =
                [
                    new ProjectionCacheTag("address-history:addr-1")
                ]
            },
            _ =>
            {
                calls++;
                return Task.FromResult("history-page-2");
            });

        Assert.Equal(2, calls);
        Assert.Equal(2, cache.Count);

        await invalidationSink.InvalidateTagsAsync([new ProjectionCacheTag("address-history:addr-1")]);

        await cache.GetOrCreateAsync(
            new ProjectionCacheKey("address-history|addr-1|page=1"),
            ProjectionCacheEntryOptions.Default,
            _ =>
            {
                calls++;
                return Task.FromResult("history-page-1-rebuilt");
            });

        await cache.GetOrCreateAsync(
            new ProjectionCacheKey("address-history|addr-1|page=2"),
            ProjectionCacheEntryOptions.Default,
            _ =>
            {
                calls++;
                return Task.FromResult("history-page-2-rebuilt");
            });

        Assert.Equal(4, calls);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public async Task InvalidateTagsAsync_KeepsEntriesOutsideTagScope()
    {
        var services = new ServiceCollection();
        services.AddProjectionReadCache(options => options.MaxEntries = 128);

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IProjectionReadCache>();
        var invalidationSink = provider.GetRequiredService<IProjectionCacheInvalidationSink>();
        var calls = 0;

        await cache.GetOrCreateAsync(
            new ProjectionCacheKey("address-utxos|addr-1"),
            new ProjectionCacheEntryOptions
            {
                Tags =
                [
                    new ProjectionCacheTag("address-utxo:addr-1")
                ]
            },
            _ =>
            {
                calls++;
                return Task.FromResult("utxos-1");
            });

        await cache.GetOrCreateAsync(
            new ProjectionCacheKey("token-history|token-1"),
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
                return Task.FromResult("history-1");
            });

        await invalidationSink.InvalidateTagsAsync([new ProjectionCacheTag("address-utxo:addr-1")]);

        await cache.GetOrCreateAsync(
            new ProjectionCacheKey("address-utxos|addr-1"),
            ProjectionCacheEntryOptions.Default,
            _ =>
            {
                calls++;
                return Task.FromResult("utxos-2");
            });

        await cache.GetOrCreateAsync(
            new ProjectionCacheKey("token-history|token-1"),
            ProjectionCacheEntryOptions.Default,
            _ =>
            {
                calls++;
                return Task.FromResult("history-2");
            });

        Assert.Equal(3, calls);
    }
}
