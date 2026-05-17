using System;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Session;
using Dxs.Consigliere.Services.P2p;
using Dxs.Tests.Shared;

using Microsoft.Extensions.Logging.Abstractions;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// Wave 2 S5.1 — dispatcher fan-out + failure-isolation tests.
/// </summary>
public class PerSessionFrameDispatcherTests
{
    private static VersionMessage SampleVersion() => new(
        ProtocolVersion: 70016,
        Services: 0x25,
        TimestampUnixSeconds: 1700000000L,
        AddrRecv: P2pAddress.FromIPv4(0x01, "127.0.0.1", 8333),
        AddrFrom: P2pAddress.Anonymous(0x25),
        Nonce: 1UL,
        UserAgent: "/dispatcher-test:0.1/",
        StartHeight: 0,
        Relay: true,
        AssociationId: null);

    private static async Task<(MiniBsvServer server, PeerSession session, PerSessionFrameDispatcher dispatcher, CancellationTokenSource cts)>
        SetupAsync()
    {
        var server = new MiniBsvServer(P2pNetwork.Mainnet);
        await server.StartAsync();
        var session = new PeerSession(P2pNetwork.Mainnet, server.EndPoint);
        var hs = await session.ConnectAsync(SampleVersion(), new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.True(hs.Success);
        var dispatcher = new PerSessionFrameDispatcher(session, NullLogger<PerSessionFrameDispatcher>.Instance);
        var cts = new CancellationTokenSource();
        _ = Task.Run(() => dispatcher.RunAsync(cts.Token));
        return (server, session, dispatcher, cts);
    }

    [Fact]
    public async Task SingleSubscriber_ReceivesFrame()
    {
        var (server, session, dispatcher, cts) = await SetupAsync();
        await using var _ = session;
        await using var __ = server;

        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        InvMessage? captured = null;
        dispatcher.Subscribe(P2pCommands.Inv, "test.inv", frame =>
        {
            captured = InvMessage.Parse(frame.Payload);
            fired.TrySetResult();
            return Task.CompletedTask;
        });

        var txid = new byte[32]; Array.Fill(txid, (byte)0xAB);
        await server.ServerSendAsync(P2pCommands.Inv, new InvMessage(new[] { new InvVector(InvType.Tx, txid) }).Serialize());

        await fired.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.NotNull(captured);
        Assert.Single(captured!.Items);
        cts.Cancel();
    }

    [Fact]
    public async Task TwoSubscribers_SameCommand_BothReceiveFrame()
    {
        var (server, session, dispatcher, cts) = await SetupAsync();
        await using var _ = session;
        await using var __ = server;

        var firedA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firedB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Subscribe(P2pCommands.Inv, "A", _ => { firedA.TrySetResult(); return Task.CompletedTask; });
        dispatcher.Subscribe(P2pCommands.Inv, "B", _ => { firedB.TrySetResult(); return Task.CompletedTask; });

        var txid = new byte[32];
        await server.ServerSendAsync(P2pCommands.Inv, new InvMessage(new[] { new InvVector(InvType.Tx, txid) }).Serialize());

        await Task.WhenAll(firedA.Task, firedB.Task).WaitAsync(TimeSpan.FromSeconds(3));
        cts.Cancel();
    }

    [Fact]
    public async Task DisposedSubscription_NoLongerReceives()
    {
        var (server, session, dispatcher, cts) = await SetupAsync();
        await using var _ = session;
        await using var __ = server;

        var countA = 0;
        var countB = 0;
        var sub = dispatcher.Subscribe(P2pCommands.Inv, "A", _ => { Interlocked.Increment(ref countA); return Task.CompletedTask; });
        dispatcher.Subscribe(P2pCommands.Inv, "B", _ => { Interlocked.Increment(ref countB); return Task.CompletedTask; });

        var txid = new byte[32];
        var inv = new InvMessage(new[] { new InvVector(InvType.Tx, txid) }).Serialize();
        await server.ServerSendAsync(P2pCommands.Inv, inv);

        // Wait for both to receive frame 1.
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && (countA < 1 || countB < 1)) await Task.Delay(20);
        Assert.Equal(1, countA);
        Assert.Equal(1, countB);

        sub.Dispose();

        await server.ServerSendAsync(P2pCommands.Inv, inv);
        deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline && countB < 2) await Task.Delay(20);

        Assert.Equal(1, countA); // unchanged after dispose
        Assert.Equal(2, countB);
        cts.Cancel();
    }

    [Fact]
    public async Task ThrowingSubscriber_IsIsolated_OthersContinueAndRunAsyncStaysAlive()
    {
        // Audit W2 followup new-M1: a throwing handler must not stop
        // RunAsync nor prevent later subscribers from receiving the
        // frame.
        var (server, session, dispatcher, cts) = await SetupAsync();
        await using var _ = session;
        await using var __ = server;

        var throwsCalled = 0;
        var goodCalled = 0;
        dispatcher.Subscribe(P2pCommands.Inv, "throws", _ =>
        {
            Interlocked.Increment(ref throwsCalled);
            throw new InvalidOperationException("boom");
        });
        dispatcher.Subscribe(P2pCommands.Inv, "good", _ =>
        {
            Interlocked.Increment(ref goodCalled);
            return Task.CompletedTask;
        });

        var txid = new byte[32];
        var inv = new InvMessage(new[] { new InvVector(InvType.Tx, txid) }).Serialize();

        // Send two frames; both subscribers should fire on both, despite
        // the throw.
        await server.ServerSendAsync(P2pCommands.Inv, inv);
        await server.ServerSendAsync(P2pCommands.Inv, inv);

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && goodCalled < 2) await Task.Delay(20);

        Assert.Equal(2, throwsCalled);
        Assert.Equal(2, goodCalled);
        cts.Cancel();
    }
}
