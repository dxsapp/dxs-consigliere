using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Data;
using Dxs.Consigliere.Services.P2p;

using Microsoft.Extensions.Logging.Abstractions;

namespace Dxs.Consigliere.Tests.P2p.Reorg;

/// <summary>
/// Wave 3 S4 — pins the orphan-tx-rebroadcast loop's behaviour: raw
/// lookup precedence (outgoing → payload), no-raw skip, announce
/// failure counter, deduplication across orphans.
/// </summary>
public class OrphanedTxRebroadcasterTests
{
    private sealed class FakeOutgoingLookup : IOutgoingRawLookup
    {
        public readonly Dictionary<string, string> Hex = new();
        public Task<string?> GetRawHexAsync(string txId, CancellationToken ct)
            => Task.FromResult(Hex.TryGetValue(txId, out var v) ? v : null);
    }

    private sealed class FakePayloadStore : IRawTransactionPayloadStore
    {
        public readonly Dictionary<string, string> PayloadHex = new();
        public Task<RawTransactionPayloadReference> SaveAsync(
            string txId, string payloadHex, string compressionAlgorithm = RawTransactionPayloadCompressionAlgorithm.None,
            CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<RawTransactionPayloadEnvelope> LoadByTxIdAsync(string txId, CancellationToken ct = default)
        {
            if (PayloadHex.TryGetValue(txId, out var hex))
                return Task.FromResult(new RawTransactionPayloadEnvelope(
                    new RawTransactionPayloadReference($"raw-tx-payloads/{txId}", txId,
                        RawTransactionPayloadCompressionAlgorithm.None), hex));
            return Task.FromResult<RawTransactionPayloadEnvelope>(null!);
        }
        public Task<RawTransactionPayloadEnvelope> LoadAsync(RawTransactionPayloadReference r, CancellationToken ct = default)
            => Task.FromResult<RawTransactionPayloadEnvelope>(null!);
    }

    private sealed class FakeAnnouncer : ITxAnnouncer
    {
        public readonly ConcurrentBag<(string TxId, string RawHex)> Calls = new();
        public int ReadyPeerCount = 1;
        public bool ThrowOnAnnounce;

        public Task<int> AnnounceAsync(string txId, string rawHex, CancellationToken ct)
        {
            Calls.Add((txId, rawHex));
            if (ThrowOnAnnounce) throw new System.InvalidOperationException("simulated failure");
            return Task.FromResult(ReadyPeerCount);
        }
    }

    private static (OrphanedTxRebroadcaster broadcaster,
                    FakeOutgoingLookup outgoing,
                    FakePayloadStore payload,
                    FakeAnnouncer announcer,
                    OrphanedTxRebroadcastRecorder recorder) Build()
    {
        var outgoing = new FakeOutgoingLookup();
        var payload = new FakePayloadStore();
        var announcer = new FakeAnnouncer();
        var recorder = new OrphanedTxRebroadcastRecorder();
        var broadcaster = new OrphanedTxRebroadcaster(
            outgoing, payload, announcer, recorder,
            NullLogger<OrphanedTxRebroadcaster>.Instance);
        return (broadcaster, outgoing, payload, announcer, recorder);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> OrphansFor(
        string blockHash, params string[] txIds)
        => new Dictionary<string, IReadOnlyList<string>> { [blockHash] = txIds };

    [Fact]
    public async Task EmptyInput_DoesNothing()
    {
        var (broadcaster, _, _, announcer, recorder) = Build();
        await broadcaster.RebroadcastAsync(
            new Dictionary<string, IReadOnlyList<string>>(), CancellationToken.None);

        Assert.Empty(announcer.Calls);
        Assert.Equal(0, recorder.GetAnnouncedCount());
    }

    [Fact]
    public async Task TxInOutgoingStore_IsAnnounced_FromOutgoingRawHex()
    {
        var (broadcaster, outgoing, _, announcer, recorder) = Build();
        outgoing.Hex["tx1"] = "rawhex1";

        await broadcaster.RebroadcastAsync(
            OrphansFor("block1", "tx1"), CancellationToken.None);

        var call = Assert.Single(announcer.Calls);
        Assert.Equal("tx1", call.TxId);
        Assert.Equal("rawhex1", call.RawHex);
        Assert.Equal(1, recorder.GetAnnouncedCount());
    }

    [Fact]
    public async Task TxInPayloadStore_IsAnnounced_FromPayloadHex()
    {
        var (broadcaster, _, payload, announcer, recorder) = Build();
        payload.PayloadHex["tx1"] = "rawhex_payload";

        await broadcaster.RebroadcastAsync(
            OrphansFor("block1", "tx1"), CancellationToken.None);

        var call = Assert.Single(announcer.Calls);
        Assert.Equal("rawhex_payload", call.RawHex);
        Assert.Equal(1, recorder.GetAnnouncedCount());
    }

    [Fact]
    public async Task OutgoingStoreTakesPrecedenceOverPayloadStore()
    {
        var (broadcaster, outgoing, payload, announcer, _) = Build();
        outgoing.Hex["tx1"] = "outgoing-raw";
        payload.PayloadHex["tx1"] = "payload-raw";

        await broadcaster.RebroadcastAsync(
            OrphansFor("block1", "tx1"), CancellationToken.None);

        var call = Assert.Single(announcer.Calls);
        Assert.Equal("outgoing-raw", call.RawHex);
    }

    [Fact]
    public async Task TxWithNoRaw_IsSkipped_NoAnnounce_CounterIncrements()
    {
        var (broadcaster, _, _, announcer, recorder) = Build();

        await broadcaster.RebroadcastAsync(
            OrphansFor("block1", "tx1"), CancellationToken.None);

        Assert.Empty(announcer.Calls);
        Assert.Equal(1, recorder.GetSkippedNoRawCount());
        Assert.Equal(0, recorder.GetAnnouncedCount());
    }

    [Fact]
    public async Task AnnounceThrows_CounterIncrements_LoopContinues()
    {
        var (broadcaster, outgoing, _, announcer, recorder) = Build();
        outgoing.Hex["tx1"] = "raw1";
        outgoing.Hex["tx2"] = "raw2";
        announcer.ThrowOnAnnounce = true;

        await broadcaster.RebroadcastAsync(
            OrphansFor("block1", "tx1", "tx2"), CancellationToken.None);

        Assert.Equal(2, announcer.Calls.Count); // both attempted
        Assert.Equal(2, recorder.GetAnnounceFailedCount());
        Assert.Equal(0, recorder.GetAnnouncedCount());
    }

    [Fact]
    public async Task NoReadyPeer_CounterIncrements_NotCountedAsAnnounced()
    {
        var (broadcaster, outgoing, _, announcer, recorder) = Build();
        outgoing.Hex["tx1"] = "raw1";
        announcer.ReadyPeerCount = 0;

        await broadcaster.RebroadcastAsync(
            OrphansFor("block1", "tx1"), CancellationToken.None);

        Assert.Single(announcer.Calls);
        Assert.Equal(0, recorder.GetAnnouncedCount());
        Assert.Equal(1, recorder.GetAnnounceNoReadyPeerCount());
    }

    [Fact]
    public async Task SameTxInTwoOrphans_AnnouncedOnce()
    {
        var (broadcaster, outgoing, _, announcer, recorder) = Build();
        outgoing.Hex["tx1"] = "raw1";

        var orphans = new Dictionary<string, IReadOnlyList<string>>
        {
            ["block1"] = new[] { "tx1" },
            ["block2"] = new[] { "tx1" }, // duplicate across orphans
        };

        await broadcaster.RebroadcastAsync(orphans, CancellationToken.None);

        Assert.Single(announcer.Calls);
        Assert.Equal(1, recorder.GetAnnouncedCount());
    }

    [Fact]
    public async Task MultipleOrphans_EachUniqueTxIdAnnouncedOnce()
    {
        var (broadcaster, outgoing, _, announcer, recorder) = Build();
        outgoing.Hex["tx1"] = "raw1";
        outgoing.Hex["tx2"] = "raw2";
        outgoing.Hex["tx3"] = "raw3";

        var orphans = new Dictionary<string, IReadOnlyList<string>>
        {
            ["block1"] = new[] { "tx1", "tx2" },
            ["block2"] = new[] { "tx3" },
        };

        await broadcaster.RebroadcastAsync(orphans, CancellationToken.None);

        Assert.Equal(3, announcer.Calls.Count);
        Assert.Equal(3, recorder.GetAnnouncedCount());
    }
}
