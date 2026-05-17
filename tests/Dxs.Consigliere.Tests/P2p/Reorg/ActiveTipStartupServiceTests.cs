using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.P2p;
using Dxs.Consigliere.WebSockets;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.P2p.Reorg;

/// <summary>
/// Wave 3 A2-followup-3 N5a fix — drive the real
/// <see cref="HeadersChainService.StartAsync"/> with a fake
/// <see cref="IBlockHeaderStore"/> populated such that
/// <c>RecentAsync(N)</c> alone would miss the active-tip header
/// (taller rejected fork headers fill the top-N). Assert the
/// in-memory chain tip ends up at the persisted active-tip pointer,
/// not the raw-max forked header.
///
/// The pre-fix `ActiveTipPointerStartupTests.ActiveTipWalkBack_*`
/// simulated the union recipe manually; this test exercises the
/// production code path.
/// </summary>
public class ActiveTipStartupServiceTests
{
    private sealed class FakeStore : IBlockHeaderStore
    {
        public readonly Dictionary<string, BlockHeaderDocument> Headers = new();
        private BlockHeaderActiveTip? _activeTip;

        public Task SaveAsync(BlockHeaderDocument doc, CancellationToken ct = default)
        {
            Headers[doc.Hash] = doc;
            return Task.CompletedTask;
        }

        public Task<BlockHeaderDocument> GetByHashAsync(string hashHex, CancellationToken ct = default)
            => Task.FromResult(Headers.TryGetValue(hashHex, out var d) ? d : null!);

        public Task<BlockHeaderDocument> GetTipAsync(CancellationToken ct = default)
        {
            BlockHeaderDocument? tip = null;
            foreach (var d in Headers.Values)
                if (tip is null || d.Height > tip.Height) tip = d;
            return Task.FromResult(tip!);
        }

        public Task<IReadOnlyList<BlockHeaderDocument>> RecentAsync(int count, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BlockHeaderDocument>>(
                Headers.Values.OrderByDescending(h => h.Height).Take(count).ToList());

        public Task PruneBelowAsync(long minHeight, CancellationToken ct = default)
        {
            var stale = Headers.Where(kv => kv.Value.Height < minHeight).Select(kv => kv.Key).ToList();
            foreach (var k in stale) Headers.Remove(k);
            return Task.CompletedTask;
        }

        public Task SetActiveTipAsync(string blockHashHex, long height, CancellationToken ct = default)
        {
            _activeTip = new BlockHeaderActiveTip(blockHashHex, height);
            return Task.CompletedTask;
        }

        public Task<BlockHeaderActiveTip?> GetActiveTipAsync(CancellationToken ct = default)
            => Task.FromResult(_activeTip);
    }

    private sealed class NoopNotifier : INewBlockNotifier
    {
        public Task NotifyAsync(BlockTipDto tip, CancellationToken ct) => Task.CompletedTask;
    }

    private static string ToHex(BlockHeader h)
        => System.Convert.ToHexString(BlockHeaderHasher.Hash(h)).ToLowerInvariant();

    private static BlockHeaderDocument BuildDoc(BlockHeader header, long height, string prevHashWireHex) => new()
    {
        Id = BlockHeaderDocument.BuildId(ToHex(header)),
        Hash = ToHex(header),
        Height = height,
        PrevHash = prevHashWireHex,
        TimestampMs = (long)BlockHeaderHasher.TimestampUnixSeconds(header) * 1000L,
        HeaderBytes80 = header.Bytes80,
    };

    [Fact]
    public async Task StartAsync_LoadsActiveTipViaWalkBack_WhenForksFillTopN()
    {
        // Setup: retention = 3. Store holds:
        //   - active chain: genesis@0 → active1@5
        //   - rejected fork chain: genesis@0 → fork1@10 → fork2@11 → fork3@12
        // RecentAsync(3) returns [fork3, fork2, fork1] by height. Without
        // walk-back, active1 is not loaded and the override would warn-
        // and-fallback to the raw-max forked header. With walk-back,
        // active1 (+ genesis ancestor) loads, the override calls
        // PromoteFork(active1), and chain.Tip ends up at active1@5.
        var chain = new HeadersChain(new HeadersChainOptions { RetainedHeaderCount = 3 });
        var store = new FakeStore();

        var genesis = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x01);
        var active1 = HeaderTestUtil.BuildChild(genesis, merkleFill: 0xA0);
        var fork1 = HeaderTestUtil.BuildChild(genesis, merkleFill: 0xB0, timestamp: 1700000005);
        var fork2 = HeaderTestUtil.BuildChild(fork1, merkleFill: 0xB0, timestamp: 1700000006);
        var fork3 = HeaderTestUtil.BuildChild(fork2, merkleFill: 0xB0, timestamp: 1700000007);

        // The pointer stores wire-order hex. Genesis prev_block is a
        // 32-zero placeholder.
        var zeroPrevHex = System.Convert.ToHexString(new byte[32]).ToLowerInvariant();
        store.Headers[ToHex(genesis)] = BuildDoc(genesis, 0, zeroPrevHex);
        store.Headers[ToHex(active1)] = BuildDoc(active1, 5, ToHex(genesis));
        store.Headers[ToHex(fork1)] = BuildDoc(fork1, 10, ToHex(genesis));
        store.Headers[ToHex(fork2)] = BuildDoc(fork2, 11, ToHex(fork1));
        store.Headers[ToHex(fork3)] = BuildDoc(fork3, 12, ToHex(fork2));
        await store.SetActiveTipAsync(ToHex(active1), 5, CancellationToken.None);

        var options = Options.Create(new HeadersChainOptions { RetainedHeaderCount = 3 });
        var bootstrapper = new HeadersChainBootstrapper(
            chain, store, new NoopHeadersBootstrapSource(),
            options, NullLogger<HeadersChainBootstrapper>.Instance);
        var service = new HeadersChainService(
            new BsvP2pHealth(),
            chain,
            options,
            store,
            new NoopNotifier(),
            bootstrapper,
            Options.Create(new BsvP2pConfig { Enabled = true }),
            NullLogger<HeadersChainService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            // Real production-path assertion: after StartAsync, the
            // in-memory tip is active1@5 — NOT fork3@12, which would
            // have been picked by raw-height ordering without the
            // walk-back fix.
            Assert.NotNull(chain.Tip);
            Assert.Equal(ToHex(active1), ToHex(chain.Tip!));
            Assert.Equal(5, chain.TipHeight);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
            await service.DisposeAsync();
        }
    }
}
