using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Observer;
using Dxs.Bsv.P2p.Session;
using Dxs.Common.Journal;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.P2p;
using Dxs.Tests.Shared;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// Wave 2 audit A2 M2: end-to-end runner test driving the production
/// path inv → getdata → tx → match → journal-append. Uses
/// <see cref="MiniBsvServer"/> + fake journal/payload-store/network
/// provider so the test is non-Raven and runs in every environment.
///
/// Also locks the audit-A2 H1 and H2 fixes:
/// - The persisted <c>TxObservation.TxId</c> is in canonical
///   display-order (matches <see cref="BitcoinHelpers.GetTxId"/>),
///   not wire-order.
/// - The runner reaches a P2PKH output match via
///   <see cref="TxScriptParser"/> + <see cref="WatchlistMatcher"/>
///   — the full Wave 2 parsing contract, not the shortcut through
///   <c>Transaction.Outputs[i].Address</c>.
/// </summary>
public class P2pMempoolIngestRunnerTests
{
    private static VersionMessage SampleVersion() => new(
        ProtocolVersion: 70016,
        Services: 0x25,
        TimestampUnixSeconds: 1700000000L,
        AddrRecv: P2pAddress.FromIPv4(0x01, "127.0.0.1", 8333),
        AddrFrom: P2pAddress.Anonymous(0x25),
        Nonce: 1UL,
        UserAgent: "/runner-e2e:0.1/",
        StartHeight: 0,
        Relay: true,
        AssociationId: null);

    private sealed class CapturingAppender : IObservationJournalAppender<ObservationJournalEntry<TxObservation>>
    {
        public readonly ConcurrentBag<ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>> Requests = new();
        public ValueTask<ObservationJournalAppendResult> AppendAsync(
            ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(new ObservationJournalAppendResult(new JournalSequence(Requests.Count), isDuplicate: false));
        }
    }

    private sealed class NullPayloadStore : IRawTransactionPayloadStore
    {
        public int SaveCount;
        public Task<RawTransactionPayloadReference> SaveAsync(
            string txId, string payloadHex,
            string compressionAlgorithm = RawTransactionPayloadCompressionAlgorithm.None,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref SaveCount);
            return Task.FromResult<RawTransactionPayloadReference>(
                new RawTransactionPayloadReference($"raw-tx-payloads/{txId}", txId, compressionAlgorithm));
        }
        public Task<RawTransactionPayloadEnvelope> LoadByTxIdAsync(string txId, CancellationToken ct = default)
            => Task.FromResult<RawTransactionPayloadEnvelope>(null);
        public Task<RawTransactionPayloadEnvelope> LoadAsync(RawTransactionPayloadReference r, CancellationToken ct = default)
            => Task.FromResult<RawTransactionPayloadEnvelope>(null);
    }

    private sealed class FakeNetwork : INetworkProvider
    {
        public Network Network => Network.Mainnet;
    }

    /// <summary>
    /// Build a minimal P2PKH-paying transaction that hashes
    /// deterministically. The output goes to a known mainnet address
    /// so we can watchlist it before observation.
    /// </summary>
    private static (byte[] rawBytes, string displayTxid, byte[] watchedHash160) BuildWatchedTx(string address)
    {
        var addr = new Address(address);
        var lockingScript = new byte[]
        {
            0x76,                                       // OP_DUP
            0xa9,                                       // OP_HASH160
            0x14,                                       // push 20 bytes
        };
        // Build raw tx: version=1, 0 inputs, 1 output (1000 sats, P2PKH), locktime=0
        var script = new byte[25];
        script[0] = 0x76; script[1] = 0xa9; script[2] = 0x14;
        Buffer.BlockCopy(addr.Hash160, 0, script, 3, 20);
        script[23] = 0x88; script[24] = 0xac;

        var raw = new System.IO.MemoryStream();
        using (var bw = new System.IO.BinaryWriter(raw, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            bw.Write((uint)1);                  // version
            bw.Write((byte)0);                  // 0 inputs (varint)
            bw.Write((byte)1);                  // 1 output (varint)
            bw.Write((ulong)1000);              // 1000 sats
            bw.Write((byte)script.Length);      // script length (varint)
            bw.Write(script);                   // P2PKH script
            bw.Write((uint)0);                  // locktime
        }
        var rawBytes = raw.ToArray();
        var txid = BitcoinHelpers.GetTxId(rawBytes);
        return (rawBytes, txid, addr.Hash160);
    }

    private static P2pMempoolIngestRunner BuildRunner(
        WatchlistMatcher matcher,
        CapturingAppender appender,
        NullPayloadStore payload,
        out SourceObservationRecorder recorder,
        out RavenWatchlistLoader loader,
        out PerSessionDispatcherRegistry registry)
    {
        var health = new BsvP2pHealth();
        recorder = new SourceObservationRecorder();
        var watcher = new MempoolWatcher(new MempoolWatcherOptions
        {
            // Use a tight timeout so the timeout test doesn't hang the
            // suite if a frame never arrives.
            GetDataTimeoutMs = 1_000,
        });
        var journal = new TxObservationJournalWriter(
            appender,
            payload,
            Options.Create(new ConsigliereStorageConfig()),
            NullLogger<TxObservationJournalWriter>.Instance);
        registry = new PerSessionDispatcherRegistry(new LoggerFactory());
        // RavenWatchlistLoader needs an IDocumentStore but we don't
        // exercise it directly in this test — just feed the matcher
        // pre-loaded (MarkLoaded) and pass a Mock store.
        loader = new RavenWatchlistLoader(
            Moq.Mock.Of<Raven.Client.Documents.IDocumentStore>(),
            matcher,
            NullLogger<RavenWatchlistLoader>.Instance);
        matcher.MarkLoaded();

        return new P2pMempoolIngestRunner(
            health,
            loader,
            matcher,
            watcher,
            recorder,
            journal,
            payload,
            registry,
            Options.Create(new MempoolWatcherOptions { GetDataTimeoutMs = 1_000 }),
            Options.Create(new BsvP2pConfig { Enabled = true, MempoolMaxFetchedTxBytes = 32 * 1024 * 1024 }),
            new FakeNetwork(),
            NullLogger<P2pMempoolIngestRunner>.Instance);
    }

    [Fact]
    public async Task InvToJournal_PersistsObservation_WithDisplayOrderTxid()
    {
        const string watchedAddress = "12UScFvnuA9FjeoapbRQ5YS963mgzrQkZo";
        var (raw, displayTxid, watchedHash) = BuildWatchedTx(watchedAddress);

        var matcher = new WatchlistMatcher();
        matcher.AddAddress(watchedHash);

        var appender = new CapturingAppender();
        var payload = new NullPayloadStore();
        var runner = BuildRunner(matcher, appender, payload,
            out var recorder, out var _, out var _);

        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();
        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);

        runner.Reconcile(new[] { session });

        // Server sends inv(MSG_TX, txid). The runner should issue
        // getdata, after which the server replies with the raw tx.
        var wireTxid = TxHashOrder.DisplayHexToWire(displayTxid);
        var inv = new InvMessage(new[] { new InvVector(InvType.Tx, wireTxid) });
        await server.ServerSendAsync(P2pCommands.Inv, inv.Serialize());

        // Wait for the getdata frame.
        var deadline = DateTime.UtcNow.AddSeconds(3);
        var sawGetData = false;
        while (DateTime.UtcNow < deadline && !sawGetData)
        {
            using var pollCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            try
            {
                var frame = await server.Received.ReadAsync(pollCts.Token);
                if (frame.Command == P2pCommands.GetData) sawGetData = true;
            }
            catch (OperationCanceledException) { }
        }
        Assert.True(sawGetData, "runner did not issue getdata after inv(MSG_TX)");

        // Now the server replies with the actual tx payload.
        await server.ServerSendAsync(P2pCommands.Tx, raw);

        // Wait for the journal append.
        deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && appender.Requests.IsEmpty) await Task.Delay(20);
        Assert.False(appender.Requests.IsEmpty,
            "runner did not append a journal observation after the tx reply");

        var req = appender.Requests.First();
        var observation = req.Observation.Observation;
        Assert.Equal(TxObservationSource.P2p, observation.Source);
        // Audit W2 A2 H1: txid must be display-order, matching
        // BitcoinHelpers.GetTxId — not Convert.ToHexString(item.Hash).
        Assert.Equal(displayTxid, observation.TxId);
        Assert.Equal(TxObservationEventType.SeenInMempool, observation.EventType);
        Assert.NotNull(req.Observation.PayloadReference);
        Assert.Equal(1, payload.SaveCount);
        Assert.Equal(1, recorder.GetMatchedCount());
    }

    [Fact]
    public async Task UnmatchedTx_IncrementsUnmatchedCount_NotJournal()
    {
        // Build a tx that pays an address NOT on the watchlist; only
        // a different address is watched.
        var (raw, _, _) = BuildWatchedTx("12UScFvnuA9FjeoapbRQ5YS963mgzrQkZo");
        var otherHash = new Address("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa").Hash160;

        var matcher = new WatchlistMatcher();
        matcher.AddAddress(otherHash);  // watch a DIFFERENT address

        var appender = new CapturingAppender();
        var payload = new NullPayloadStore();
        var runner = BuildRunner(matcher, appender, payload,
            out var recorder, out var _, out var _);

        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();
        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);
        runner.Reconcile(new[] { session });

        var txid = BitcoinHelpers.GetTxId(raw);
        var wireTxid = TxHashOrder.DisplayHexToWire(txid);
        await server.ServerSendAsync(P2pCommands.Inv,
            new InvMessage(new[] { new InvVector(InvType.Tx, wireTxid) }).Serialize());

        // Drain getdata
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
        {
            try { _ = await server.Received.ReadAsync(cts.Token); } catch { }
        }

        await server.ServerSendAsync(P2pCommands.Tx, raw);

        // Wait until recorder either matches or unmatches.
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline
               && recorder.GetMatchedCount() == 0 && recorder.GetUnmatchedCount() == 0)
            await Task.Delay(20);

        Assert.Equal(0, recorder.GetMatchedCount());
        Assert.Equal(1, recorder.GetUnmatchedCount());
        Assert.True(appender.Requests.IsEmpty);
        Assert.Equal(0, payload.SaveCount);
    }

    [Fact]
    public async Task RateLimited_Inv_ForgetsTxid_NotRetainedInDedupe()
    {
        // Audit W2 A2 M1 first half: runner calls watcher.Forget(txid)
        // on RateLimited so the dedupe cache doesn't permanently
        // de-duplicate a transient rate-limit miss. Pin THIS specific
        // behaviour — the retry-after-window-slides part lives in its
        // own test below to keep the assertion crisp (audit
        // A2-followup new-L1 test-name precision fix).
        var (_, displayTxid, watchedHash) = BuildWatchedTx("12UScFvnuA9FjeoapbRQ5YS963mgzrQkZo");

        var (runner, watcher, recorder, session, server) =
            await BuildRunnerForRateLimitTest(watchedHash);
        try
        {
            // Pre-consume the budget so the next inv hits RateLimited.
            Assert.Equal(MempoolWatcher.FetchDecision.Fetch, watcher.DecideFetch("burner"));

            var dedupeBefore = watcher.DedupeSize;

            // Inv arrives → runner sees RateLimited → Forget.
            var wireTxid = TxHashOrder.DisplayHexToWire(displayTxid);
            await server.ServerSendAsync(P2pCommands.Inv,
                new InvMessage(new[] { new InvVector(InvType.Tx, wireTxid) }).Serialize());

            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline && recorder.GetRateLimitedCount() == 0)
                await Task.Delay(20);
            Assert.True(recorder.GetRateLimitedCount() >= 1,
                "expected RateLimited counter to increment on first inv");

            // The rate-limited txid was inserted into the dedupe cache
            // by DecideFetch (TryAdd succeeded before the rate-budget
            // check rejected it). If the runner's Forget(txid) call ran
            // on the RateLimited branch the dedupe size returns to its
            // pre-inv value; without Forget it would be one larger.
            // This is the precise observable we want to pin — and it
            // doesn't depend on the rate window state.
            Assert.Equal(dedupeBefore, watcher.DedupeSize);
        }
        finally
        {
            await session.DisposeAsync();
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task RateLimited_Inv_RetriesAfterWindowSlides()
    {
        // Audit W2 A2 M1 second half: once the 1-second rate-limit
        // window slides, a re-issued inv for the same txid succeeds
        // (because the runner called Forget on the rate-limit branch,
        // AND the window has now made room in the rate budget).
        // Pinned by sleeping past the window — slower but the only
        // proof of the actual behaviour the audit cared about.
        var (raw, displayTxid, watchedHash) = BuildWatchedTx("12UScFvnuA9FjeoapbRQ5YS963mgzrQkZo");

        var (runner, watcher, recorder, session, server) =
            await BuildRunnerForRateLimitTest(watchedHash);
        try
        {
            // Burn the single rate slot.
            Assert.Equal(MempoolWatcher.FetchDecision.Fetch, watcher.DecideFetch("burner"));

            // First inv: rate-limited.
            var wireTxid = TxHashOrder.DisplayHexToWire(displayTxid);
            await server.ServerSendAsync(P2pCommands.Inv,
                new InvMessage(new[] { new InvVector(InvType.Tx, wireTxid) }).Serialize());
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline && recorder.GetRateLimitedCount() == 0)
                await Task.Delay(20);
            Assert.True(recorder.GetRateLimitedCount() >= 1);

            // Wait for the 1-second sliding window to clear.
            await Task.Delay(1_100);

            // Second inv for the SAME txid. Now Fetch is allowed
            // (window slot freed) and dedupe doesn't block (Forget
            // cleared the prior entry).
            await server.ServerSendAsync(P2pCommands.Inv,
                new InvMessage(new[] { new InvVector(InvType.Tx, wireTxid) }).Serialize());

            // Drain server's received-frame channel until we see a
            // getdata (proves the retry actually issued the request).
            deadline = DateTime.UtcNow.AddSeconds(3);
            var sawGetData = false;
            while (DateTime.UtcNow < deadline && !sawGetData)
            {
                using var pollCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                try
                {
                    var frame = await server.Received.ReadAsync(pollCts.Token);
                    if (frame.Command == P2pCommands.GetData) sawGetData = true;
                }
                catch (OperationCanceledException) { }
            }
            Assert.True(sawGetData,
                "after the 1 s rate-limit window slid, the runner did not retry getdata");
        }
        finally
        {
            await session.DisposeAsync();
            await server.DisposeAsync();
        }
    }

    /// <summary>
    /// Build a runner + a connected MiniBsvServer + session set up
    /// for the rate-limit tests (MaxGetDataPerSec=1). Returns the
    /// owned-by-caller server + session so the test can dispose them
    /// in a finally block.
    /// </summary>
    private async Task<(P2pMempoolIngestRunner runner, MempoolWatcher watcher, SourceObservationRecorder recorder, PeerSession session, MiniBsvServer server)>
        BuildRunnerForRateLimitTest(byte[] watchedHash)
    {
        var matcher = new WatchlistMatcher();
        matcher.AddAddress(watchedHash);

        var health = new BsvP2pHealth();
        var recorder = new SourceObservationRecorder();
        var watcher = new MempoolWatcher(new MempoolWatcherOptions
        {
            MaxGetDataPerSec = 1,
            GetDataTimeoutMs = 1_000,
        });
        var appender = new CapturingAppender();
        var payload = new NullPayloadStore();
        var journal = new TxObservationJournalWriter(
            appender, payload, Options.Create(new ConsigliereStorageConfig()),
            NullLogger<TxObservationJournalWriter>.Instance);
        var registry = new PerSessionDispatcherRegistry(new LoggerFactory());
        var loader = new RavenWatchlistLoader(
            Moq.Mock.Of<Raven.Client.Documents.IDocumentStore>(), matcher,
            NullLogger<RavenWatchlistLoader>.Instance);
        matcher.MarkLoaded();
        var runner = new P2pMempoolIngestRunner(
            health, loader, matcher, watcher, recorder, journal, payload, registry,
            Options.Create(new MempoolWatcherOptions { MaxGetDataPerSec = 1, GetDataTimeoutMs = 1_000 }),
            Options.Create(new BsvP2pConfig { Enabled = true, MempoolMaxFetchedTxBytes = 32 * 1024 * 1024 }),
            new FakeNetwork(),
            NullLogger<P2pMempoolIngestRunner>.Instance);

        var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();
        var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);
        runner.Reconcile(new[] { session });

        return (runner, watcher, recorder, session, server);
    }
}
