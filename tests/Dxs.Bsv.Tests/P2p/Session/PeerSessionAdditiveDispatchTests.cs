using System;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Session;
using Dxs.Tests.Shared;

namespace Dxs.Bsv.Tests.P2p.Session;

/// <summary>
/// Wave 1 S0 additive-dispatch invariant: new typed callbacks
/// (<see cref="PeerSession.OnInvReceived"/> /
/// <see cref="PeerSession.OnRejectReceived"/> /
/// <see cref="PeerSession.OnHeadersReceived"/>) must fire <b>in addition
/// to</b> the existing <see cref="PeerSession.IncomingMessages"/> channel
/// delivering the frame. Regression guard for audit A1 H4.
/// </summary>
public class PeerSessionAdditiveDispatchTests
{
    private static VersionMessage SampleVersion() =>
        new(
            ProtocolVersion: 70016,
            Services: 0x25,
            TimestampUnixSeconds: 1700000000L,
            AddrRecv: P2pAddress.FromIPv4(0x01, "127.0.0.1", 8333),
            AddrFrom: P2pAddress.Anonymous(0x25),
            Nonce: 1UL,
            UserAgent: "/additive-test:0.1/",
            StartHeight: 0,
            Relay: true,
            AssociationId: null);

    [Fact]
    public async Task InvFrame_FiresCallback_AndStillReachesIncomingChannel()
    {
        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();

        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);

        InvMessage? callbackInv = null;
        var callbackFired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.OnInvReceived = msg =>
        {
            callbackInv = msg;
            callbackFired.TrySetResult();
        };

        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success, $"handshake failed: {hs.FailureReason} {hs.FailureDetail}");

        // Server pushes an inv to us.
        var txid = new byte[32]; Array.Fill(txid, (byte)0xAB);
        await server.ServerSendAsync(P2pCommands.Inv, new InvMessage(new[] { new InvVector(InvType.Tx, txid) }).Serialize());

        // Callback must fire.
        await callbackFired.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.NotNull(callbackInv);
        Assert.Single(callbackInv!.Items);
        Assert.Equal(InvType.Tx, callbackInv.Items[0].Type);

        // Channel must ALSO receive it (additive invariant).
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var frame = await session.IncomingMessages.ReadAsync(cts.Token);
        Assert.Equal(P2pCommands.Inv, frame.Command);
    }

    [Fact]
    public async Task RejectFrame_FiresCallback_AndStillReachesIncomingChannel()
    {
        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();

        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);

        RejectMessage? callbackReject = null;
        var callbackFired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.OnRejectReceived = msg =>
        {
            callbackReject = msg;
            callbackFired.TrySetResult();
        };

        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);

        var txid = new byte[32]; Array.Fill(txid, (byte)0xCD);
        var reject = new RejectMessage("tx", RejectCode.Invalid, "bad-txns-foo", txid);
        await server.ServerSendAsync(P2pCommands.Reject, reject.Serialize());

        await callbackFired.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.NotNull(callbackReject);
        Assert.Equal(RejectCode.Invalid, callbackReject!.Code);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var frame = await session.IncomingMessages.ReadAsync(cts.Token);
        Assert.Equal(P2pCommands.Reject, frame.Command);
    }

    [Fact]
    public async Task HeadersFrame_FiresCallback_AndStillReachesIncomingChannel()
    {
        await using var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();

        await using var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);

        System.Collections.Generic.IReadOnlyList<BlockHeader>? callbackHeaders = null;
        var callbackFired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.OnHeadersReceived = hdrs =>
        {
            callbackHeaders = hdrs;
            callbackFired.TrySetResult();
        };

        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);

        // headers payload: 1-byte varint count + 80-byte header + 1-byte tx_count varint (0).
        // BlockHeader.Bytes80 stores only the 80-byte header; HeadersMessage.Serialize
        // appends the transaction-count varint per BSV serialisation rules.
        var headerBytes = new byte[BlockHeader.Size];
        Array.Fill(headerBytes, (byte)0x42);
        var msg = new HeadersMessage(new[] { new BlockHeader(headerBytes) });
        await server.ServerSendAsync(P2pCommands.Headers, msg.Serialize());

        await callbackFired.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.NotNull(callbackHeaders);
        Assert.Single(callbackHeaders!);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var frame = await session.IncomingMessages.ReadAsync(cts.Token);
        Assert.Equal(P2pCommands.Headers, frame.Command);
    }
}
