using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Tests.Shared;

using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// Wave 1 S2 integration test for <see cref="BlockHeaderStore"/>. Runs
/// against an embedded RavenDB instance via <see cref="RavenTestDriver"/>.
/// Skipped on environments lacking the required .NET runtime (matches
/// the rest of the integration-test suite).
/// </summary>
public class BlockHeaderStoreTests : RavenTestDriver
{
    static BlockHeaderStoreTests()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false,
            },
        });
    }

    private static BlockHeaderDocument Doc(long height, string hash, string prev = "00") => new()
    {
        Id = BlockHeaderDocument.BuildId(hash),
        Hash = hash,
        Height = height,
        PrevHash = prev,
        TimestampMs = 1_700_000_000_000L + height,
        HeaderBytes80 = new byte[80],
    };

    [Fact]
    public async Task SaveAndGetByHash_RoundTripsDocument()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;
        using var store = GetDocumentStore();
        var sut = new BlockHeaderStore(store);

        var doc = Doc(123, "aabb", "1122");
        await sut.SaveAsync(doc);

        var loaded = await sut.GetByHashAsync("aabb");
        Assert.NotNull(loaded);
        Assert.Equal(123, loaded.Height);
        Assert.Equal("aabb", loaded.Hash);
        Assert.Equal("1122", loaded.PrevHash);
    }

    [Fact]
    public async Task GetByHash_Unknown_ReturnsNull()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;
        using var store = GetDocumentStore();
        var sut = new BlockHeaderStore(store);

        var loaded = await sut.GetByHashAsync("deadbeef");
        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetTipAsync_ReturnsHighestHeightDoc()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;
        using var store = GetDocumentStore();
        var sut = new BlockHeaderStore(store);

        await sut.SaveAsync(Doc(100, "h100"));
        await sut.SaveAsync(Doc(102, "h102"));
        await sut.SaveAsync(Doc(101, "h101"));
        WaitForIndexing(store);

        var tip = await sut.GetTipAsync();
        Assert.NotNull(tip);
        Assert.Equal(102, tip.Height);
        Assert.Equal("h102", tip.Hash);
    }

    [Fact]
    public async Task GetTipAsync_Empty_ReturnsNull()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;
        using var store = GetDocumentStore();
        var sut = new BlockHeaderStore(store);

        var tip = await sut.GetTipAsync();
        Assert.Null(tip);
    }

    [Fact]
    public async Task RecentAsync_ReturnsTipFirst_LimitedByCount()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;
        using var store = GetDocumentStore();
        var sut = new BlockHeaderStore(store);

        for (var h = 0; h < 5; h++)
            await sut.SaveAsync(Doc(h, $"h{h}"));
        WaitForIndexing(store);

        var top3 = await sut.RecentAsync(3);
        Assert.Equal(3, top3.Count);
        Assert.Equal(4, top3[0].Height);
        Assert.Equal(3, top3[1].Height);
        Assert.Equal(2, top3[2].Height);

        Assert.Empty(await sut.RecentAsync(0));
    }

    [Fact]
    public async Task PruneBelowAsync_DeletesOnlyHeadersBelowCutoff()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;
        using var store = GetDocumentStore();
        var sut = new BlockHeaderStore(store);

        for (var h = 0; h < 5; h++)
            await sut.SaveAsync(Doc(h, $"h{h}"));
        WaitForIndexing(store);

        await sut.PruneBelowAsync(minHeight: 3);
        WaitForIndexing(store);

        var remaining = await sut.RecentAsync(10);
        Assert.Equal(2, remaining.Count);
        Assert.Equal(4, remaining[0].Height);
        Assert.Equal(3, remaining[1].Height);

        // Cutoff is strict-less-than, so h=3 survives.
        Assert.NotNull(await sut.GetByHashAsync("h3"));
        Assert.Null(await sut.GetByHashAsync("h2"));
    }
}
