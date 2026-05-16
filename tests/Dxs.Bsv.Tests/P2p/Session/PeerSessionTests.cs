using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Session;

namespace Dxs.Bsv.Tests.P2p.Session;

public class PeerSessionTests
{
    private static VersionMessage SampleVersion(string ua = "/test:0.1/") =>
        new(
            ProtocolVersion: 70016,
            Services: 0x25,
            TimestampUnixSeconds: 1700000000L,
            AddrRecv: P2pAddress.FromIPv4(0x01, "127.0.0.1", 8333),
            AddrFrom: P2pAddress.Anonymous(0x25),
            Nonce: 1UL,
            UserAgent: ua,
            StartHeight: 0,
            Relay: true,
            AssociationId: null);

    [Fact]
    public async Task ConnectAsync_PerformsHandshake_AndEntersReadyState()
    {
        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();

        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await session.ConnectAsync(SampleVersion("/client:0.1/"), cts.Token);

        Assert.True(result.Success, $"Handshake failed: {result.FailureReason} {result.FailureDetail}");
        Assert.NotNull(result.PeerVersion);
        Assert.Equal("/MiniBsvServer:0.1/", result.PeerVersion!.UserAgent);
        Assert.Equal(PeerSessionState.Ready, session.State);

        // Server should have decoded our version with our UA.
        var peerVersion = await server.PeerVersionTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("/client:0.1/", peerVersion.UserAgent);
    }

    [Fact]
    public async Task IncomingMessage_AfterHandshake_IsDispatchedToChannel()
    {
        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();

        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);

        // Server pushes an inv to us.
        var txid = new byte[32]; Array.Fill(txid, (byte)0xAB);
        await server.ServerSendAsync(P2pCommands.Inv, new InvMessage(new[] { new InvVector(InvType.Tx, txid) }).Serialize());

        // Read from session inbound channel.
        var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var got = await session.IncomingMessages.ReadAsync(readCts.Token);

        Assert.Equal(P2pCommands.Inv, got.Command);
        var parsed = InvMessage.Parse(got.Payload);
        Assert.Single(parsed.Items);
        Assert.Equal(InvType.Tx, parsed.Items[0].Type);
        Assert.Equal(txid, parsed.Items[0].Hash);
    }

    [Fact]
    public async Task PingFromPeer_AutoRepliedWithPong_NotSurfacedToChannel()
    {
        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();

        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);

        // Drain the protoconf the client always sends after verack.
        await ExpectCommand(server, P2pCommands.Protoconf);

        // Server pings us with a specific nonce.
        await server.ServerPingAsync(0xCAFEBABEDEADBEEFUL);

        // We should receive a pong with the same nonce back.
        var srvFrame = await ExpectCommand(server, P2pCommands.Pong);

        var pong = PongMessage.Parse(srvFrame.Payload);
        Assert.Equal(0xCAFEBABEDEADBEEFUL, pong.Nonce);

        // The ping must NOT have surfaced to the inbound channel — we should have
        // no waiting frames there (we wait a tiny moment, then check).
        await Task.Delay(50);
        Assert.False(session.IncomingMessages.TryRead(out _));
    }

    [Fact]
    public async Task SendAsync_DeliversFrameToServer()
    {
        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();

        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);

        // Drain the protoconf the client always sends after verack.
        await ExpectCommand(server, P2pCommands.Protoconf);

        var txid = new byte[32]; Array.Fill(txid, (byte)0x77);
        await session.SendInvAsync(InvMessage.ForTx(txid), CancellationToken.None);

        var srvFrame = await ExpectCommand(server, P2pCommands.Inv);
        var parsed = InvMessage.Parse(srvFrame.Payload);
        Assert.Equal(txid, parsed.Items[0].Hash);
    }

    [Fact]
    public async Task SendProtoconfAfterVerack_DefaultEnabled_PeerReceivesProtoconf()
    {
        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();

        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);

        var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var srvFrame = await server.Received.ReadAsync(readCts.Token);

        Assert.Equal(P2pCommands.Protoconf, srvFrame.Command);
        var pc = ProtoconfMessage.Parse(srvFrame.Payload);
        Assert.Equal(ProtoconfMessage.DefaultMaxRecvPayloadLength, pc.MaxRecvPayloadLength);
    }

    [Fact]
    public async Task PeerProtoconf_UpdatesPeerMaxRecvPayloadLength()
    {
        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();

        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);

        // Server tells us they want at most 4 MiB inbound payload.
        var protoconf = new ProtoconfMessage(4 * 1024 * 1024, "Default");
        await server.ServerSendAsync(P2pCommands.Protoconf, protoconf.Serialize());

        // Wait briefly for the receive loop to consume it.
        await WaitUntil(() => session.PeerMaxRecvPayloadLength == 4 * 1024 * 1024, TimeSpan.FromSeconds(2));
        Assert.Equal(4 * 1024 * 1024, session.PeerMaxRecvPayloadLength);
    }

    [Fact]
    public async Task PeerFeeFilter_UpdatesPeerMinFeePerKb()
    {
        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();

        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);

        await server.ServerSendAsync(P2pCommands.FeeFilter, new FeeFilterMessage(1234L).Serialize());

        await WaitUntil(() => session.PeerMinFeePerKbSat == 1234L, TimeSpan.FromSeconds(2));
        Assert.Equal(1234L, session.PeerMinFeePerKbSat);
    }

    [Fact]
    public async Task ConnectAsync_FailsToUnroutableAddress_WithConnectTimeout()
    {
        // 240.0.0.1 — reserved, unroutable on the public Internet. Forces TCP timeout.
        var unroutable = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("240.0.0.1"), 8333);
        var config = new PeerSessionConfig { ConnectTimeout = TimeSpan.FromMilliseconds(500) };

        await using var session = new PeerSession(P2pNetwork.Mainnet, unroutable, config);
        var result = await session.ConnectAsync(SampleVersion(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(DisconnectReason.ConnectTimeout, result.FailureReason);
        Assert.Equal(PeerSessionState.Closed, session.State);
    }

    [Fact]
    public async Task DisposeAsync_CompletesWithLocalReason()
    {
        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();

        var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);

        await session.DisposeAsync();

        var reason = await session.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(DisconnectReason.Local, reason);
        Assert.Equal(PeerSessionState.Closed, session.State);
    }

    private static async Task<InboundOnServer> ExpectCommand(MiniBsvServer server, string command, TimeSpan? timeout = null)
    {
        var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(3));
        while (true)
        {
            var frame = await server.Received.ReadAsync(cts.Token);
            if (frame.Command == command) return frame;
            // Skip unrelated frames (e.g. protoconf, sendcmpct, sendheaders).
        }
    }

    private static async Task WaitUntil(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(25);
        }
    }
}
