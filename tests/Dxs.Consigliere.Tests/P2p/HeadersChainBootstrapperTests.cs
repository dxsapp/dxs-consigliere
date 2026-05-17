using System;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.P2p;
using Dxs.Tests.Shared;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// Wave 1 S4 unit tests for <see cref="HeadersChainBootstrapper"/>.
/// Exercises both code paths (fetch-and-apply, skipped-when-disabled)
/// using a fake <see cref="IHeadersBootstrapSource"/> — no real HTTP.
/// </summary>
public class HeadersChainBootstrapperTests : RavenTestDriver
{
    static HeadersChainBootstrapperTests()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false,
            },
        });
    }

    private sealed class StubSource(BootstrapSeed? seed) : IHeadersBootstrapSource
    {
        public int CallCount;
        public Task<BootstrapSeed?> FetchAsync(CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(seed);
        }
    }

    private static byte[] BuildRegtestHeaderBytes(byte[] prev32, byte merkleFill, uint timestamp = 1700000000)
    {
        var bytes = new byte[BlockHeader.Size];
        bytes[0] = 1;
        Buffer.BlockCopy(prev32, 0, bytes, 4, 32);
        for (var i = 36; i < 68; i++) bytes[i] = merkleFill;
        bytes[68] = (byte)(timestamp & 0xff);
        bytes[69] = (byte)((timestamp >> 8) & 0xff);
        bytes[70] = (byte)((timestamp >> 16) & 0xff);
        bytes[71] = (byte)((timestamp >> 24) & 0xff);
        bytes[72] = 0xff; bytes[73] = 0xff; bytes[74] = 0x7f; bytes[75] = 0x20;
        for (uint n = 0; n < uint.MaxValue; n++)
        {
            bytes[76] = (byte)(n & 0xff);
            bytes[77] = (byte)((n >> 8) & 0xff);
            bytes[78] = (byte)((n >> 16) & 0xff);
            bytes[79] = (byte)((n >> 24) & 0xff);
            if (BlockHeaderHasher.MeetsTarget(new BlockHeader(bytes)))
                return (byte[])bytes.Clone();
        }
        throw new InvalidOperationException("no PoW nonce found");
    }

    private static HeadersChainBootstrapper Build(
        HeadersChain chain,
        BlockHeaderStore store,
        IHeadersBootstrapSource source,
        HeadersChainOptions? options = null)
    {
        return new HeadersChainBootstrapper(
            chain,
            store,
            source,
            Options.Create(options ?? new HeadersChainOptions()),
            NullLogger<HeadersChainBootstrapper>.Instance);
    }

    [SkippableFact]
    public async Task SeedFromBitailsFalse_DoesNotCallSource()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        var chain = new HeadersChain();
        var source = new StubSource(seed: null);
        var sut = Build(chain, store, source, new HeadersChainOptions { SeedFromBitails = false });

        var applied = await sut.BootstrapAsync(CancellationToken.None);

        Assert.False(applied);
        Assert.Equal(0, source.CallCount);
        Assert.Null(chain.Tip);
    }

    [SkippableFact]
    public async Task ChainAlreadyHasTip_SkipsSource()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        var chain = new HeadersChain();
        // Pre-populate chain with a header.
        chain.LoadFromStore(new[] { (new BlockHeader(BuildRegtestHeaderBytes(new byte[32], 0x77)), 42L) });

        var source = new StubSource(seed: null);
        var sut = Build(chain, store, source, new HeadersChainOptions { SeedFromBitails = true });

        var applied = await sut.BootstrapAsync(CancellationToken.None);

        Assert.False(applied);
        Assert.Equal(0, source.CallCount);
        Assert.Equal(42, chain.TipHeight);
    }

    [SkippableFact]
    public async Task FetchedSeed_LoadsIntoChainAndStore()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        var chain = new HeadersChain();
        var headerBytes = BuildRegtestHeaderBytes(new byte[32], 0x99);
        var source = new StubSource(new BootstrapSeed(headerBytes, Height: 123_456));
        var sut = Build(chain, store, source, new HeadersChainOptions { SeedFromBitails = true });

        var applied = await sut.BootstrapAsync(CancellationToken.None);

        Assert.True(applied);
        Assert.Equal(1, source.CallCount);
        Assert.Equal(123_456, chain.TipHeight);

        WaitForIndexing(docStore);
        var tipDoc = await store.GetTipAsync();
        Assert.NotNull(tipDoc);
        Assert.Equal(123_456, tipDoc.Height);
    }

    [SkippableFact]
    public async Task SourceReturnsNull_SkipsApply()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        var chain = new HeadersChain();
        var source = new StubSource(seed: null);
        var sut = Build(chain, store, source, new HeadersChainOptions { SeedFromBitails = true });

        var applied = await sut.BootstrapAsync(CancellationToken.None);

        Assert.False(applied);
        Assert.Equal(1, source.CallCount);
        Assert.Null(chain.Tip);
    }

    [SkippableFact]
    public async Task SourceMalformedHeaderSize_SkipsApply()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        var chain = new HeadersChain();
        // Wrong size — bootstrapper should refuse.
        var source = new StubSource(new BootstrapSeed(new byte[40], 1));
        var sut = Build(chain, store, source, new HeadersChainOptions { SeedFromBitails = true });

        var applied = await sut.BootstrapAsync(CancellationToken.None);

        Assert.False(applied);
        Assert.Null(chain.Tip);
    }
}
