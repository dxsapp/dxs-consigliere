using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;

namespace Dxs.Bsv.Tests.P2p.Session;

/// <summary>
/// Minimal BSV-protocol-compatible test peer. Binds to a loopback ephemeral
/// port; accepts ONE inbound connection; performs the server-side handshake;
/// then exposes channels to drive arbitrary traffic in either direction.
/// </summary>
internal sealed class MiniBsvServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly P2pNetwork _network;
    private readonly Channel<InboundOnServer> _received = Channel.CreateUnbounded<InboundOnServer>();
    private TcpClient? _clientSocket;
    private NetworkStream? _stream;
    private Task? _receiveLoop;
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource<VersionMessage> _peerVersion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IPEndPoint EndPoint => (IPEndPoint)_listener.LocalEndpoint;
    public ChannelReader<InboundOnServer> Received => _received.Reader;
    public Task<VersionMessage> PeerVersionTask => _peerVersion.Task;

    public MiniBsvServer(P2pNetwork network)
    {
        _network = network;
        _listener = new TcpListener(IPAddress.Loopback, port: 0);
    }

    public Task StartAsync()
    {
        _listener.Start();
        // Background accept on first connection
        _receiveLoop = Task.Run(AcceptAndServeAsync);
        return Task.CompletedTask;
    }

    private async Task AcceptAndServeAsync()
    {
        try
        {
            _clientSocket = await _listener.AcceptTcpClientAsync(_cts.Token);
            _stream = _clientSocket.GetStream();

            // Server-side handshake: wait for peer's version, send our version + verack,
            // then wait for peer's verack.
            var clientVersion = await ExpectFrame(P2pCommands.Version);
            var parsed = VersionMessage.Parse(clientVersion);
            _peerVersion.TrySetResult(parsed);

            // Our (server) version + verack
            var ourVersion = new VersionMessage(
                ProtocolVersion: 70016,
                Services: 0x25,
                TimestampUnixSeconds: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                AddrRecv: P2pAddress.Anonymous(0x25),
                AddrFrom: P2pAddress.Anonymous(0x25),
                Nonce: 1UL,
                UserAgent: "/MiniBsvServer:0.1/",
                StartHeight: 0,
                Relay: true,
                AssociationId: null);
            await WriteFrameAsync(P2pCommands.Version, ourVersion.Serialize());
            await WriteFrameAsync(P2pCommands.Verack, Array.Empty<byte>());

            await ExpectFrame(P2pCommands.Verack);

            // Steady state — surface everything else for the test to inspect.
            while (!_cts.IsCancellationRequested)
            {
                var (command, payload) = await ReadFrame();
                if (command == P2pCommands.Ping)
                {
                    await WriteFrameAsync(P2pCommands.Pong, payload);
                    continue;
                }
                await _received.Writer.WriteAsync(new InboundOnServer(command, payload), _cts.Token);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _received.Writer.TryComplete(ex);
            _peerVersion.TrySetException(ex);
        }
        finally
        {
            _received.Writer.TryComplete();
        }
    }

    /// <summary>Push an arbitrary frame to the client (e.g. drive an inv or reject).</summary>
    public async Task ServerSendAsync(string command, byte[] payload)
    {
        if (_stream is null) throw new InvalidOperationException("Server stream not yet connected");
        await WriteFrameAsync(command, payload);
    }

    public async Task ServerPingAsync(ulong nonce)
    {
        await WriteFrameAsync(P2pCommands.Ping, new PingMessage(nonce).Serialize());
    }

    private async Task<byte[]> ExpectFrame(string command)
    {
        var (gotCommand, payload) = await ReadFrame();
        if (gotCommand != command)
            throw new InvalidOperationException($"Expected command '{command}', got '{gotCommand}'");
        return payload;
    }

    private async Task<(string command, byte[] payload)> ReadFrame()
    {
        if (_stream is null) throw new InvalidOperationException("Stream is null");
        var header = new byte[Frame.HeaderSize];
        await ReadExactAsync(header);
        var length = (uint)(header[16] | (header[17] << 8) | (header[18] << 16) | (header[19] << 24));
        var full = new byte[Frame.HeaderSize + (int)length];
        Buffer.BlockCopy(header, 0, full, 0, Frame.HeaderSize);
        if (length > 0)
            await ReadExactAsync(new ArraySegment<byte>(full, Frame.HeaderSize, (int)length));
        var result = FrameCodec.TryDecode(_network, full, Frame.LegacyMaxPayloadLength, out var frame, out _);
        if (result != DecodeResult.Ok)
            throw new InvalidOperationException($"Server failed to decode frame: {result}");
        return (frame!.Command, frame.Payload);
    }

    private async Task ReadExactAsync(byte[] buffer) =>
        await ReadExactAsync(new ArraySegment<byte>(buffer));

    private async Task ReadExactAsync(ArraySegment<byte> destination)
    {
        if (_stream is null) throw new InvalidOperationException("Stream is null");
        var offset = 0;
        while (offset < destination.Count)
        {
            var n = await _stream.ReadAsync(destination.Slice(offset), _cts.Token);
            if (n == 0) throw new EndOfStreamException("Client closed");
            offset += n;
        }
    }

    private async Task WriteFrameAsync(string command, byte[] payload)
    {
        if (_stream is null) throw new InvalidOperationException("Stream is null");
        var frame = FrameCodec.Encode(_network, command, payload);
        await _stream.WriteAsync(frame, _cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        try { _cts.Cancel(); } catch { }
        try { _clientSocket?.Close(); } catch { }
        try { _listener.Stop(); } catch { }
        if (_receiveLoop is not null)
        {
            try { await _receiveLoop; } catch { }
        }
        _cts.Dispose();
    }
}

internal sealed record InboundOnServer(string Command, byte[] Payload);
