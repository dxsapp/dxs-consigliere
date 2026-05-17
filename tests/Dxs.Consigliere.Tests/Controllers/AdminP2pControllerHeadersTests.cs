using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Chain;
using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.P2p;
using Dxs.Tests.Shared;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Controllers;

/// <summary>
/// Wave 1 S6 — controller tests for the headers/tip and headers/recent
/// endpoints on <see cref="AdminP2pController"/>. Auth-attribute parity
/// with the rest of the controller is structurally enforced by the
/// class-level [Authorize] attribute; tests focus on JSON shape and
/// count-clamp behavior.
/// </summary>
public class AdminP2pControllerHeadersTests : RavenTestDriver
{
    static AdminP2pControllerHeadersTests()
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

    private static AdminP2pController Build(BlockHeaderStore store, int retainedHeaderCount = 200)
        => new(
            new BsvP2pHealth(),
            store,
            Options.Create(new HeadersChainOptions { RetainedHeaderCount = retainedHeaderCount }));

    [Fact]
    public async Task HeadersTip_Empty_Returns404()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        var controller = Build(store);

        var result = await controller.HeadersTip(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task HeadersTip_ReturnsHighestHeight()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        await store.SaveAsync(Doc(100, "h100"));
        await store.SaveAsync(Doc(102, "h102", prev: "h101"));
        await store.SaveAsync(Doc(101, "h101"));
        WaitForIndexing(docStore);

        var controller = Build(store);
        var result = await controller.HeadersTip(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<HeadersTipDto>(ok.Value);
        Assert.Equal(102, dto.Height);
        Assert.Equal("h102", dto.Hash);
        Assert.Equal("h101", dto.PrevHash);
    }

    [Fact]
    public async Task HeadersRecent_ZeroOrNegativeCount_ReturnsEmpty()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        await store.SaveAsync(Doc(1, "h1"));
        WaitForIndexing(docStore);

        var controller = Build(store);

        var zero = await controller.HeadersRecent(0, CancellationToken.None);
        var okZero = Assert.IsType<OkObjectResult>(zero.Result);
        Assert.Empty((HeadersTipDto[])okZero.Value!);

        var neg = await controller.HeadersRecent(-5, CancellationToken.None);
        var okNeg = Assert.IsType<OkObjectResult>(neg.Result);
        Assert.Empty((HeadersTipDto[])okNeg.Value!);
    }

    [Fact]
    public async Task HeadersRecent_ReturnsTipFirst_ClampsToRetainedCount()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        for (var h = 0; h < 5; h++) await store.SaveAsync(Doc(h, $"h{h}"));
        WaitForIndexing(docStore);

        // Retained = 3 → count=10 must clamp to 3.
        var controller = Build(store, retainedHeaderCount: 3);
        var result = await controller.HeadersRecent(10, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsType<HeadersTipDto[]>(ok.Value);
        Assert.Equal(3, dtos.Length);
        Assert.Equal(4, dtos[0].Height);
        Assert.Equal(3, dtos[1].Height);
        Assert.Equal(2, dtos[2].Height);
    }
}
