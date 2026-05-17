using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.P2p;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// Wave 1 S4 unit tests for <see cref="HeadersChainBootstrapper"/> that
/// run **without** an embedded Raven instance — exercise the
/// <see cref="IBlockHeaderStore"/> interface contract with a tiny
/// in-memory fake. Complements the Raven-gated
/// <see cref="HeadersChainBootstrapperTests"/> by giving the same
/// scenarios deterministic coverage in every environment (audit A2 M1).
/// </summary>
public class HeadersChainBootstrapperUnitTests
{
    private sealed class InMemoryStore : IBlockHeaderStore
    {
        public readonly Dictionary<string, BlockHeaderDocument> Saved = new();
        public Task SaveAsync(BlockHeaderDocument doc, CancellationToken ct = default)
        {
            Saved[doc.Hash] = doc;
            return Task.CompletedTask;
        }
        public Task<BlockHeaderDocument> GetByHashAsync(string hashHex, CancellationToken ct = default)
            => Task.FromResult(Saved.TryGetValue(hashHex, out var d) ? d : null!);
        public Task<BlockHeaderDocument> GetTipAsync(CancellationToken ct = default)
        {
            BlockHeaderDocument? tip = null;
            foreach (var d in Saved.Values) if (tip is null || d.Height > tip.Height) tip = d;
            return Task.FromResult(tip!);
        }
        public Task<IReadOnlyList<BlockHeaderDocument>> RecentAsync(int count, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BlockHeaderDocument>>(Array.Empty<BlockHeaderDocument>());
        public Task PruneBelowAsync(long minHeight, CancellationToken ct = default) => Task.CompletedTask;
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

    private static byte[] BuildRegtestHeaderBytes(byte merkleFill)
    {
        var bytes = new byte[BlockHeader.Size];
        bytes[0] = 1;
        // prev = zeros (anchoring use only — bootstrapper doesn't link)
        for (var i = 36; i < 68; i++) bytes[i] = merkleFill;
        bytes[68] = 0x00; bytes[69] = 0xc0; bytes[70] = 0x4a; bytes[71] = 0x65; // timestamp
        bytes[72] = 0xff; bytes[73] = 0xff; bytes[74] = 0x7f; bytes[75] = 0x20; // regtest bits
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
        IBlockHeaderStore store,
        IHeadersBootstrapSource source,
        HeadersChainOptions? options = null)
        => new(chain, store, source,
            Options.Create(options ?? new HeadersChainOptions()),
            NullLogger<HeadersChainBootstrapper>.Instance);

    [Fact]
    public async Task FetchedSeed_SeedsChainAndPersistsDoc()
    {
        var chain = new HeadersChain();
        var store = new InMemoryStore();
        var seed = new BootstrapSeed(BuildRegtestHeaderBytes(0x99), Height: 850_000);
        var sut = Build(chain, store, new StubSource(seed), new HeadersChainOptions { SeedFromBitails = true });

        var applied = await sut.BootstrapAsync(CancellationToken.None);

        Assert.True(applied);
        Assert.Equal(850_000, chain.TipHeight);
        Assert.NotNull(chain.Tip);
        Assert.Single(store.Saved);
    }

    [Fact]
    public async Task SourceReturnsNull_ChainStaysEmpty()
    {
        var chain = new HeadersChain();
        var store = new InMemoryStore();
        var source = new StubSource(seed: null);
        var sut = Build(chain, store, source, new HeadersChainOptions { SeedFromBitails = true });

        var applied = await sut.BootstrapAsync(CancellationToken.None);

        Assert.False(applied);
        Assert.Equal(1, source.CallCount);
        Assert.Null(chain.Tip);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task SeedFromBitailsFalse_SkipsSource()
    {
        var chain = new HeadersChain();
        var store = new InMemoryStore();
        var source = new StubSource(new BootstrapSeed(BuildRegtestHeaderBytes(0x11), 1));
        var sut = Build(chain, store, source, new HeadersChainOptions { SeedFromBitails = false });

        var applied = await sut.BootstrapAsync(CancellationToken.None);

        Assert.False(applied);
        Assert.Equal(0, source.CallCount);
        Assert.Null(chain.Tip);
    }

    [Fact]
    public async Task ChainAlreadyHasTip_SkipsSource()
    {
        var chain = new HeadersChain();
        var anchor = new BlockHeader(BuildRegtestHeaderBytes(0x77));
        chain.Seed(anchor, height: 42);
        var source = new StubSource(new BootstrapSeed(BuildRegtestHeaderBytes(0x99), 1000));
        var sut = Build(chain, new InMemoryStore(), source, new HeadersChainOptions { SeedFromBitails = true });

        var applied = await sut.BootstrapAsync(CancellationToken.None);

        Assert.False(applied);
        Assert.Equal(0, source.CallCount);
        Assert.Equal(42, chain.TipHeight);
    }

    [Fact]
    public async Task MalformedHeaderSize_RejectedNotSeeded()
    {
        var chain = new HeadersChain();
        var store = new InMemoryStore();
        var source = new StubSource(new BootstrapSeed(new byte[40], 1));
        var sut = Build(chain, store, source, new HeadersChainOptions { SeedFromBitails = true });

        var applied = await sut.BootstrapAsync(CancellationToken.None);

        Assert.False(applied);
        Assert.Null(chain.Tip);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task ChainUnanchored_AfterBootstrapFails_FirstP2PHeaderIsDropped()
    {
        // Audit A2 H1 contract: with no bootstrap success, the chain
        // stays Unanchored. The first arbitrary P2P header returns
        // Unanchored (NOT auto-promoted to height 0).
        var chain = new HeadersChain();
        var source = new StubSource(seed: null);
        var sut = Build(chain, new InMemoryStore(), source, new HeadersChainOptions { SeedFromBitails = true });
        await sut.BootstrapAsync(CancellationToken.None);

        var arbitraryHeader = new BlockHeader(BuildRegtestHeaderBytes(0xab));
        var result = chain.TryExtend(arbitraryHeader);

        Assert.IsType<ExtendResult.Unanchored>(result);
        Assert.Null(chain.Tip);
    }
}
