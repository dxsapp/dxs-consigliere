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

    /// <summary>
    /// Build a doc with real 32-byte wire-order hex hashes. The controller
    /// now hex-decodes doc.Hash / doc.PrevHash to convert them to display
    /// order, so artificial strings like "h102" would fail (audit A2 H3).
    /// </summary>
    private static BlockHeaderDocument Doc(long height, byte fillHash, byte fillPrev = 0) => new()
    {
        Id = BlockHeaderDocument.BuildId(MakeHex(fillHash)),
        Hash = MakeHex(fillHash),
        Height = height,
        PrevHash = MakeHex(fillPrev),
        TimestampMs = 1_700_000_000_000L + height,
        HeaderBytes80 = new byte[80],
    };

    private static string MakeHex(byte fill)
    {
        var b = new byte[32];
        for (var i = 0; i < 32; i++) b[i] = fill;
        return Convert.ToHexString(b).ToLowerInvariant();
    }

    private static AdminP2pController Build(BlockHeaderStore store, int retainedHeaderCount = 200)
        => new(
            new BsvP2pHealth(),
            store,
            Options.Create(new HeadersChainOptions { RetainedHeaderCount = retainedHeaderCount }),
            // Wave 6 S3 — headers tests do not exercise alerts; the
            // injected repo is a no-op stand-in.
            new NoopAlertRepository(),
            Options.Create(new Dxs.Consigliere.Configs.BsvP2pConfig()));

    private sealed class NoopAlertRepository : IAlertEventRepository
    {
        public Task SaveAsync(P2pAlertEvent ev, CancellationToken ct) => Task.CompletedTask;
        public Task<System.Collections.Generic.IReadOnlyList<string>> GetAllIdsOrderedAsync(CancellationToken ct)
            => Task.FromResult<System.Collections.Generic.IReadOnlyList<string>>(System.Array.Empty<string>());
        public Task DeleteAsync(System.Collections.Generic.IReadOnlyList<string> ids, CancellationToken ct) => Task.CompletedTask;
        public Task<System.Collections.Generic.IReadOnlyList<P2pAlertEvent>> GetRecentAsync(
            int limit, long? sinceUnixMs, CancellationToken ct)
            => Task.FromResult<System.Collections.Generic.IReadOnlyList<P2pAlertEvent>>(System.Array.Empty<P2pAlertEvent>());
    }

    [SkippableFact]
    public async Task HeadersTip_Empty_Returns404()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        var controller = Build(store);

        var result = await controller.HeadersTip(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [SkippableFact]
    public async Task HeadersTip_ReturnsHighestHeight()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        await store.SaveAsync(Doc(100, 0xaa));
        await store.SaveAsync(Doc(102, 0xcc, fillPrev: 0xbb));
        await store.SaveAsync(Doc(101, 0xbb));
        WaitForIndexing(docStore);

        var controller = Build(store);
        var result = await controller.HeadersTip(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<HeadersTipDto>(ok.Value);
        Assert.Equal(102, dto.Height);
        // Wire-order 0xcc * 32 reversed = display-order is also 0xcc * 32
        // (all-same-byte case). Real headers differ; covered by the
        // ToDisplayHex unit test on BSV genesis.
        Assert.Equal(MakeHex(0xcc), dto.Hash);
        Assert.Equal(MakeHex(0xbb), dto.PrevHash);
    }

    [SkippableFact]
    public async Task HeadersRecent_ZeroOrNegativeCount_ReturnsEmpty()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        await store.SaveAsync(Doc(1, 0x01));
        WaitForIndexing(docStore);

        var controller = Build(store);

        var zero = await controller.HeadersRecent(0, CancellationToken.None);
        var okZero = Assert.IsType<OkObjectResult>(zero.Result);
        Assert.Empty((HeadersTipDto[])okZero.Value!);

        var neg = await controller.HeadersRecent(-5, CancellationToken.None);
        var okNeg = Assert.IsType<OkObjectResult>(neg.Result);
        Assert.Empty((HeadersTipDto[])okNeg.Value!);
    }

    [SkippableFact]
    public async Task HeadersRecent_ReturnsTipFirst_ClampsToRetainedCount()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        for (var h = 0; h < 5; h++) await store.SaveAsync(Doc(h, (byte)(0x10 + h)));
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
