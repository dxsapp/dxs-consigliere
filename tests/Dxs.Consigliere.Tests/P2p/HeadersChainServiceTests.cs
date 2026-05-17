using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Session;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.P2p;
using Dxs.Consigliere.WebSockets;
using Dxs.Tests.Shared;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Raven.Embedded;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// Wave 1 S3 integration test for <see cref="HeadersChainService"/>.
/// Builds a real <see cref="PeerSession"/> handshaked against
/// <see cref="MiniBsvServer"/>, wires it via the service's test-seam
/// <see cref="HeadersChainService.Reconcile(IReadOnlyCollection{PeerSession})"/>,
/// drives inv / headers from the server, and asserts:
/// - on inv(MSG_BLOCK) the service sends a getheaders to the peer
/// - on headers the service persists via <see cref="BlockHeaderStore"/>
///   and invokes the <see cref="INewBlockNotifier"/> with the right tip.
///
/// Skipped on machines lacking the embedded-Raven runtime — same gate as
/// other integration tests in this suite.
/// </summary>
public class HeadersChainServiceTests : RavenTestDriver
{
    static HeadersChainServiceTests()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false,
            },
        });
    }

    private sealed class CapturingNotifier : INewBlockNotifier
    {
        public readonly List<BlockTipDto> Notifications = new();
        public Task NotifyAsync(BlockTipDto tip, CancellationToken ct)
        {
            Notifications.Add(tip);
            return Task.CompletedTask;
        }
    }

    private static VersionMessage SampleVersion() => new(
        ProtocolVersion: 70016,
        Services: 0x25,
        TimestampUnixSeconds: 1700000000L,
        AddrRecv: P2pAddress.FromIPv4(0x01, "127.0.0.1", 8333),
        AddrFrom: P2pAddress.Anonymous(0x25),
        Nonce: 1UL,
        UserAgent: "/headers-test:0.1/",
        StartHeight: 0,
        Relay: true,
        AssociationId: null);

    private static HeadersChainService BuildService(
        HeadersChain chain,
        BlockHeaderStore store,
        INewBlockNotifier notifier,
        HeadersChainOptions? options = null)
    {
        var health = new BsvP2pHealth();
        return new HeadersChainService(
            health,
            chain,
            Options.Create(options ?? new HeadersChainOptions()),
            store,
            notifier,
            Options.Create(new BsvP2pConfig { Enabled = true }),
            NullLogger<HeadersChainService>.Instance);
    }

    /// <summary>
    /// Builds an 80-byte PoW-valid header with bits=0x207fffff (regtest
    /// min target). Iterates nonce until the hash satisfies the target;
    /// the high byte of regtest target tops out at 0x7f so ~50% of
    /// hashes pass per attempt.
    /// </summary>
    private static BlockHeader BuildRegtestHeader(byte[] prev32, byte merkleFill, uint timestamp = 1700000000)
    {
        var bytes = new byte[BlockHeader.Size];
        bytes[0] = 1; // version
        Buffer.BlockCopy(prev32, 0, bytes, 4, 32);
        for (var i = 36; i < 68; i++) bytes[i] = merkleFill;
        bytes[68] = (byte)(timestamp & 0xff);
        bytes[69] = (byte)((timestamp >> 8) & 0xff);
        bytes[70] = (byte)((timestamp >> 16) & 0xff);
        bytes[71] = (byte)((timestamp >> 24) & 0xff);
        // bits = 0x207fffff (regtest min, LE)
        bytes[72] = 0xff; bytes[73] = 0xff; bytes[74] = 0x7f; bytes[75] = 0x20;
        for (uint n = 0; n < uint.MaxValue; n++)
        {
            bytes[76] = (byte)(n & 0xff);
            bytes[77] = (byte)((n >> 8) & 0xff);
            bytes[78] = (byte)((n >> 16) & 0xff);
            bytes[79] = (byte)((n >> 24) & 0xff);
            var hdr = new BlockHeader(bytes);
            if (BlockHeaderHasher.MeetsTarget(hdr)) return hdr;
        }
        throw new InvalidOperationException("no PoW nonce found in 2^32");
    }

    [Fact]
    public async Task Headers_FromPeer_AreStoredAndNotifierFires()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        var chain = new HeadersChain(new HeadersChainOptions { RetainedHeaderCount = 200 });
        var notifier = new CapturingNotifier();
        var service = BuildService(chain, store, notifier);

        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();
        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success, $"handshake failed: {hs.FailureReason} {hs.FailureDetail}");

        // Wire callbacks via the test seam.
        service.Reconcile(new[] { session });

        // Push a valid headers message (single header at height 0).
        var h0 = BuildRegtestHeader(prev32: new byte[32], merkleFill: 0x11);
        await server.ServerSendAsync(P2pCommands.Headers, new HeadersMessage(new[] { h0 }).Serialize());

        // Wait for notification.
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (notifier.Notifications.Count == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.Single(notifier.Notifications);
        Assert.Equal(0, notifier.Notifications[0].Height);
        Assert.Equal(BlockHeader.Size, notifier.Notifications[0].HeaderSize);

        // Persisted to store.
        WaitForIndexing(docStore);
        var tipDoc = await store.GetTipAsync();
        Assert.NotNull(tipDoc);
        Assert.Equal(0, tipDoc.Height);
        Assert.Equal(notifier.Notifications[0].Hash, tipDoc.Hash);
    }

    [Fact]
    public async Task Inv_MsgBlock_TriggersGetHeadersToAnnouncingPeer()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        var chain = new HeadersChain();
        var notifier = new CapturingNotifier();
        var service = BuildService(chain, store, notifier);

        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();
        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);

        service.Reconcile(new[] { session });

        // Push inv(MSG_BLOCK) — service should respond with getheaders.
        var blockId = new byte[32]; for (var i = 0; i < 32; i++) blockId[i] = (byte)i;
        var inv = new InvMessage(new[] { new InvVector(InvType.Block, blockId) });
        await server.ServerSendAsync(P2pCommands.Inv, inv.Serialize());

        // Drain server-received frames; expect a getheaders within a short window.
        var deadline = DateTime.UtcNow.AddSeconds(3);
        var sawGetHeaders = false;
        while (DateTime.UtcNow < deadline && !sawGetHeaders)
        {
            using var pollCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            try
            {
                var frame = await server.Received.ReadAsync(pollCts.Token);
                if (frame.Command == P2pCommands.GetHeaders) sawGetHeaders = true;
            }
            catch (OperationCanceledException) { /* keep polling */ }
        }
        Assert.True(sawGetHeaders, "Service did not send getheaders after inv(MSG_BLOCK)");
    }

    [Fact]
    public async Task Fork_HeaderIsPersisted_TipUnchanged()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;
        using var docStore = GetDocumentStore();
        var store = new BlockHeaderStore(docStore);
        var chain = new HeadersChain();
        var notifier = new CapturingNotifier();
        var service = BuildService(chain, store, notifier);

        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();
        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        service.Reconcile(new[] { session });

        // Build a chain: h0 → h1; then a fork h1' at h0.
        var h0 = BuildRegtestHeader(new byte[32], 0x11);
        var h1 = BuildRegtestHeader(BlockHeaderHasher.Hash(h0), 0x22);
        var h1Fork = BuildRegtestHeader(BlockHeaderHasher.Hash(h0), 0x33);

        await server.ServerSendAsync(P2pCommands.Headers, new HeadersMessage(new[] { h0, h1 }).Serialize());
        // Wait for both notifications.
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (notifier.Notifications.Count < 2 && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Assert.Equal(2, notifier.Notifications.Count);

        // Send fork — should be persisted but NOT notified (tip unchanged).
        var notifiedBefore = notifier.Notifications.Count;
        await server.ServerSendAsync(P2pCommands.Headers, new HeadersMessage(new[] { h1Fork }).Serialize());

        // Give the fork header time to process.
        await Task.Delay(300);
        Assert.Equal(notifiedBefore, notifier.Notifications.Count); // tip unchanged, no new notification

        WaitForIndexing(docStore);
        var allDocs = await store.RecentAsync(10);
        Assert.Equal(3, allDocs.Count); // h0, h1, h1Fork all persisted
    }
}
