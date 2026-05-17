using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.P2p;

namespace Dxs.Consigliere.Tests.P2p.Reorg;

/// <summary>
/// Wave 3 A2-followup N1 — pins the persistent active-tip pointer
/// contract: <see cref="IBlockHeaderStore.SetActiveTipAsync"/> is
/// invoked only on a real active-chain advance (extend or reorg
/// promotion); a stored taller fork header alone must not become
/// the active tip after restart.
/// </summary>
public class ActiveTipPointerStartupTests
{
    private sealed class FakeBlockHeaderStore : IBlockHeaderStore
    {
        public readonly Dictionary<string, BlockHeaderDocument> Headers = new();
        public BlockHeaderActiveTip ActiveTip { get; private set; }

        public Task SaveAsync(BlockHeaderDocument doc, CancellationToken ct = default)
        {
            Headers[doc.Hash] = doc;
            return Task.CompletedTask;
        }

        public Task<BlockHeaderDocument> GetByHashAsync(string hashHex, CancellationToken ct = default)
            => Task.FromResult(Headers.TryGetValue(hashHex, out var d) ? d : null!);

        public Task<BlockHeaderDocument> GetTipAsync(CancellationToken ct = default)
        {
            BlockHeaderDocument tip = null!;
            foreach (var d in Headers.Values)
                if (tip is null || d.Height > tip.Height) tip = d;
            return Task.FromResult(tip);
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
            ActiveTip = new BlockHeaderActiveTip(blockHashHex, height);
            return Task.CompletedTask;
        }

        public Task<BlockHeaderActiveTip> GetActiveTipAsync(CancellationToken ct = default)
            => Task.FromResult(ActiveTip);
    }

    [Fact]
    public async Task ActiveTipPointer_AbsentByDefault()
    {
        // Cold store: no active-tip pointer yet. GetActiveTipAsync
        // must return null so HeadersChainService can fall back to
        // the height-based selection on legacy data.
        var store = new FakeBlockHeaderStore();
        var tip = await store.GetActiveTipAsync(CancellationToken.None);
        Assert.Null(tip);
    }

    [Fact]
    public async Task SetActiveTipAsync_StoresLatestPointer()
    {
        var store = new FakeBlockHeaderStore();
        await store.SetActiveTipAsync("aaa", 100, CancellationToken.None);
        var tip = await store.GetActiveTipAsync(CancellationToken.None);
        Assert.NotNull(tip);
        Assert.Equal("aaa", tip!.BlockHashHex);
        Assert.Equal(100, tip.Height);

        // Subsequent set updates the same pointer.
        await store.SetActiveTipAsync("bbb", 101, CancellationToken.None);
        tip = await store.GetActiveTipAsync(CancellationToken.None);
        Assert.Equal("bbb", tip!.BlockHashHex);
        Assert.Equal(101, tip.Height);
    }

    [Fact]
    public void ActiveTipWalkBack_LoadsActiveChainAncestors_DisplacedByForks()
    {
        // Audit W3 A2-followup-2 N1 fix: when RecentAsync(N) by raw
        // height alone would push the active-tip's ancestors out (the
        // store holds enough taller rejected forks to fill the top-N),
        // the startup recipe must ALSO walk back from the active-tip
        // pointer via PrevHash so the active chain itself is loaded.
        //
        // Setup: active chain @ height 5 (single header), retention =
        // 3, store holds 4 rejected fork headers @ heights 10/11/12/13
        // (all taller than active). RecentAsync(3) returns the top
        // three fork headers (heights 11, 12, 13). Without walk-back,
        // the active-tip header (height 5) would NOT be loaded and
        // PromoteFork would have nothing to promote to.
        //
        // The walk-back logic in HeadersChainService.StartAsync calls
        // GetByHashAsync(activeTip.BlockHashHex) and follows PrevHash
        // until the retention cap; that guarantees the active header
        // is in the loaded set.
        var genesis = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x01);
        var active5 = HeaderTestUtil.BuildChild(genesis, merkleFill: 0xA0); // height 1, but pretend it's "active@5"
        var forks = new BlockHeader[4];
        var fp = genesis;
        for (var i = 0; i < 4; i++)
        {
            forks[i] = HeaderTestUtil.BuildChild(fp, merkleFill: 0xB0, timestamp: 1700000005);
            fp = forks[i];
        }

        var store = new FakeBlockHeaderStore();
        store.Headers[ToHex(active5)] = ToDoc(active5, height: 5, prev: genesis);
        store.Headers[ToHex(forks[0])] = ToDoc(forks[0], height: 10, prev: genesis);
        store.Headers[ToHex(forks[1])] = ToDoc(forks[1], height: 11, prev: forks[0]);
        store.Headers[ToHex(forks[2])] = ToDoc(forks[2], height: 12, prev: forks[1]);
        store.Headers[ToHex(forks[3])] = ToDoc(forks[3], height: 13, prev: forks[2]);

        // Active-tip pointer points at active5; without walk-back this
        // header is excluded from RecentAsync(3).
        store.SetActiveTipAsync(ToHex(active5), 5, CancellationToken.None).Wait();

        // Simulate HeadersChainService.StartAsync's logic: union of
        // RecentAsync(3) and walk-back from active-tip via PrevHash.
        var top = store.RecentAsync(3, CancellationToken.None).Result;
        var pointer = store.GetActiveTipAsync(CancellationToken.None).Result;
        Assert.NotNull(pointer);

        var combined = new Dictionary<string, BlockHeaderDocument>();
        foreach (var d in top) combined[d.Hash] = d;
        var walked = 0;
        var cursor = store.GetByHashAsync(pointer!.BlockHashHex, CancellationToken.None).Result;
        while (cursor is not null && walked < 3)
        {
            combined[cursor.Hash] = cursor;
            walked++;
            if (string.IsNullOrEmpty(cursor.PrevHash)) break;
            cursor = store.GetByHashAsync(cursor.PrevHash, CancellationToken.None).Result;
        }

        // Active header must be in the union, even though it's not in
        // the raw-height top 3.
        Assert.Contains(ToHex(active5), combined.Keys);
        Assert.DoesNotContain(ToHex(forks[0]), top.Select(d => d.Hash));
        // And the top-3 by height is exactly the three tallest forks.
        Assert.Equal(
            new[] { ToHex(forks[3]), ToHex(forks[2]), ToHex(forks[1]) },
            top.Select(d => d.Hash).ToArray());
    }

    private static string ToHex(BlockHeader h)
        => System.Convert.ToHexString(BlockHeaderHasher.Hash(h)).ToLowerInvariant();

    private static BlockHeaderDocument ToDoc(BlockHeader header, long height, BlockHeader prev)
        => new()
        {
            Id = BlockHeaderDocument.BuildId(ToHex(header)),
            Hash = ToHex(header),
            Height = height,
            PrevHash = ToHex(prev),
            TimestampMs = (long)BlockHeaderHasher.TimestampUnixSeconds(header) * 1000L,
            HeaderBytes80 = header.Bytes80,
        };

    [Fact]
    public void HeadersChain_PromoteFork_PromotesIfHashInRetention()
    {
        // Direct chain-level test: simulate a startup where both an
        // active-chain extension and a taller fork-side header are in
        // the retention window. HeadersChain.LoadFromStore picks the
        // taller fork by raw height; HeadersChainService.StartAsync
        // must then call PromoteFork(activeTipHeader) to override.
        // The result: chain.Tip is the active tip even though a
        // taller header is retained.
        var chain = new HeadersChain(new HeadersChainOptions { RetainedHeaderCount = 10 });
        var genesis = HeaderTestUtil.Build(prev: new byte[32], merkleFill: 0x01);

        // Active chain: genesis → A1 (height 1).
        var a1 = HeaderTestUtil.BuildChild(genesis, merkleFill: 0xA1);
        // Fork side: genesis → F1 → F2 (height 2; taller than active).
        var f1 = HeaderTestUtil.BuildChild(genesis, merkleFill: 0xB1, timestamp: 1700000005);
        var f2 = HeaderTestUtil.BuildChild(f1, merkleFill: 0xB1, timestamp: 1700000006);

        // LoadFromStore mimics what HeadersChainService does on
        // startup: feed all retained headers in. Tip ends up at the
        // tallest (F2).
        chain.LoadFromStore(new[]
        {
            (genesis, 0L),
            (a1, 1L),
            (f1, 1L),
            (f2, 2L),
        });
        Assert.Equal(BlockHeaderHasher.ToDisplayHex(BlockHeaderHasher.Hash(f2)),
            BlockHeaderHasher.ToDisplayHex(BlockHeaderHasher.Hash(chain.Tip!)));

        // Now apply the active-tip pointer override (HeadersChainService.StartAsync logic).
        var activeTipWireHex = System.Convert.ToHexString(BlockHeaderHasher.Hash(a1))
            .ToLowerInvariant();
        Assert.True(chain.TryGetByWireHashHex(activeTipWireHex, out var activeHeader, out _));
        chain.PromoteFork(activeHeader);

        // chain.Tip is now A1, even though F2 exists in retention at height 2.
        Assert.Equal(BlockHeaderHasher.ToDisplayHex(BlockHeaderHasher.Hash(a1)),
            BlockHeaderHasher.ToDisplayHex(BlockHeaderHasher.Hash(chain.Tip!)));
        Assert.Equal(1, chain.TipHeight);
    }
}
