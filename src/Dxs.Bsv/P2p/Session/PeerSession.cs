#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Messages;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dxs.Bsv.P2p.Session;

/// <summary>
/// One outbound BSV P2P connection. Owns the TCP socket, the send + receive
/// loops, and the handshake state machine. Pushes received frames at the
/// caller via an inbound <see cref="Channel"/>, accepts outbound frames via
/// <see cref="SendAsync"/>. Ping/pong, protoconf and feefilter are handled
/// internally and do NOT appear on the inbound channel.
/// </summary>
public sealed class PeerSession : IAsyncDisposable
{
    private readonly P2pNetwork _network;
    private readonly IPEndPoint _remote;
    private readonly PeerSessionConfig _config;
    private readonly ILogger _logger;
    private readonly TaskCompletionSource<DisconnectReason> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Channel<byte[]> _outbound;
    private readonly Channel<InboundFrame> _inbound;
    private readonly CancellationTokenSource _internalCts = new();

    private TcpClient? _client;
    private NetworkStream? _stream;
    private Task? _receiveLoop;
    private Task? _sendLoop;
    private Task? _pingLoop;
    private long _lastInboundUtcTicks;
    private int _disposed;

    /// <summary>
    /// Maximum payload length the peer told us they accept (via their <c>protoconf</c>).
    /// 0 means "no protoconf received yet — assume the basic legacy limit".
    /// </summary>
    public int PeerMaxRecvPayloadLength { get; private set; }

    /// <summary>
    /// Peer's minimum fee per kilobyte for tx relay (from their <c>feefilter</c>).
    /// 0 means "no feefilter received yet — peer accepts any fee".
    /// </summary>
    public long PeerMinFeePerKbSat { get; private set; }

    public PeerSessionState State { get; private set; } = PeerSessionState.Created;
    public VersionMessage? PeerVersion { get; private set; }
    public IPEndPoint Remote => _remote;

    public ChannelReader<InboundFrame> IncomingMessages => _inbound.Reader;

    /// <summary>Completes when the session ends, with the reason.</summary>
    public Task<DisconnectReason> Completion => _completion.Task;

    public PeerSession(P2pNetwork network, IPEndPoint remote, PeerSessionConfig? config = null, ILogger? logger = null)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _remote = remote ?? throw new ArgumentNullException(nameof(remote));
        _config = config ?? new PeerSessionConfig();
        _logger = logger ?? NullLogger.Instance;
        _outbound = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        _inbound = Channel.CreateBounded<InboundFrame>(new BoundedChannelOptions(_config.InboundChannelCapacity) { SingleReader = false, FullMode = BoundedChannelFullMode.Wait });
    }

    /// <summary>
    /// Open the TCP connection, perform the version/verack handshake, then
    /// start the steady-state I/O loops. On failure the session is closed
    /// and <see cref="Completion"/> resolves with the reason.
    /// </summary>
    public async Task<HandshakeResult> ConnectAsync(VersionMessage ourVersion, CancellationToken cancellationToken)
    {
        if (ourVersion is null) throw new ArgumentNullException(nameof(ourVersion));
        if (State != PeerSessionState.Created)
            throw new InvalidOperationException($"Session is in state {State}, cannot connect again");

        State = PeerSessionState.Connecting;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _internalCts.Token);

        try
        {
            // 1. TCP connect with timeout
            _client = new TcpClient { NoDelay = true };
            using (var connectCts = new CancellationTokenSource(_config.ConnectTimeout))
            using (var connectLinked = CancellationTokenSource.CreateLinkedTokenSource(linked.Token, connectCts.Token))
            {
                try
                {
                    await _client.ConnectAsync(_remote.Address, _remote.Port, connectLinked.Token);
                }
                catch (OperationCanceledException) when (connectCts.IsCancellationRequested)
                {
                    return await EndWith(DisconnectReason.ConnectTimeout, "TCP connect timed out");
                }
            }
            _stream = _client.GetStream();
            _lastInboundUtcTicks = DateTime.UtcNow.Ticks;
            State = PeerSessionState.Handshaking;

            // 2. Send our version
            var versionFrame = FrameCodec.Encode(_network, P2pCommands.Version, ourVersion.Serialize());
            await _stream.WriteAsync(versionFrame, linked.Token);

            // 3. Drive the handshake inline (peer may interleave protoconf/authch/sendcmpct).
            using var handshakeCts = new CancellationTokenSource(_config.HandshakeTimeout);
            using var handshakeLinked = CancellationTokenSource.CreateLinkedTokenSource(linked.Token, handshakeCts.Token);

            VersionMessage? peerVersion = null;
            var gotPeerVerack = false;
            var sentOurVerack = false;

            try
            {
                while (!(peerVersion is not null && gotPeerVerack && sentOurVerack))
                {
                    var frame = await ReadNextFrameAsync(handshakeLinked.Token);
                    switch (frame.Command)
                    {
                        case P2pCommands.Version:
                            peerVersion = VersionMessage.Parse(frame.Payload);
                            PeerVersion = peerVersion;
                            // Reply verack as soon as we have peer's version.
                            await _stream.WriteAsync(FrameCodec.Encode(_network, P2pCommands.Verack, ReadOnlyMemory<byte>.Empty.Span), linked.Token);
                            if (_config.SendProtoconfAfterVerack)
                            {
                                await _stream.WriteAsync(
                                    FrameCodec.Encode(_network, P2pCommands.Protoconf, ProtoconfMessage.PhaseOneDefault.Serialize()),
                                    linked.Token);
                            }
                            sentOurVerack = true;
                            break;
                        case P2pCommands.Verack:
                            gotPeerVerack = true;
                            break;
                        case P2pCommands.Ping:
                            // Some peers ping during handshake. Always reply.
                            await _stream.WriteAsync(FrameCodec.Encode(_network, P2pCommands.Pong, frame.Payload), linked.Token);
                            break;
                        case P2pCommands.Protoconf:
                            ApplyProtoconf(frame.Payload);
                            break;
                        case P2pCommands.FeeFilter:
                            ApplyFeeFilter(frame.Payload);
                            break;
                        case P2pCommands.Reject:
                            // Peer rejected our version. Surface and bail.
                            var rej = RejectMessage.Parse(frame.Payload);
                            return await EndWith(DisconnectReason.HandshakeRejected, $"peer reject during handshake: {rej.Code} {rej.Reason}");
                        // authch/sendheaders/sendcmpct/sendhdrsen/addr/etc — drop, but still count toward "inbound traffic".
                    }
                }
            }
            catch (OperationCanceledException) when (handshakeCts.IsCancellationRequested)
            {
                return await EndWith(DisconnectReason.HandshakeTimeout, "Handshake did not complete in time");
            }
            catch (P2pDecodeException ex)
            {
                return await EndWith(DisconnectReason.ProtocolViolation, ex.Message);
            }
            catch (Exception ex) when (IsConnectionLost(ex))
            {
                return await EndWith(ClassifyConnectionLost(ex), ex.Message);
            }

            // 4. Steady state — spin up background loops.
            State = PeerSessionState.Ready;
            _receiveLoop = Task.Run(() => ReceiveLoopAsync(_internalCts.Token));
            _sendLoop    = Task.Run(() => SendLoopAsync(_internalCts.Token));
            _pingLoop    = Task.Run(() => PingLoopAsync(_internalCts.Token));

            return HandshakeResult.Ok(peerVersion!);
        }
        catch (Exception ex)
        {
            return await EndWith(DisconnectReason.InternalError, ex.Message);
        }
    }

    /// <summary>Enqueue an outbound frame. Awaits on backpressure.</summary>
    public ValueTask SendAsync(string command, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (State != PeerSessionState.Ready)
            throw new InvalidOperationException($"Cannot send while session is in state {State}");
        var frame = FrameCodec.Encode(_network, command, payload.Span);
        return _outbound.Writer.WriteAsync(frame, cancellationToken);
    }

    /// <summary>Convenience: send a typed BSV message.</summary>
    public ValueTask SendVersionAsync(VersionMessage msg, CancellationToken ct) => SendAsync(P2pCommands.Version, msg.Serialize(), ct);
    public ValueTask SendInvAsync(InvMessage msg, CancellationToken ct) => SendAsync(P2pCommands.Inv, msg.Serialize(), ct);
    public ValueTask SendGetDataAsync(GetDataMessage msg, CancellationToken ct) => SendAsync(P2pCommands.GetData, msg.Serialize(), ct);
    public ValueTask SendNotFoundAsync(NotFoundMessage msg, CancellationToken ct) => SendAsync(P2pCommands.NotFound, msg.Serialize(), ct);
    public ValueTask SendHeadersAsync(HeadersMessage msg, CancellationToken ct) => SendAsync(P2pCommands.Headers, msg.Serialize(), ct);
    public ValueTask SendAddrAsync(AddrMessage msg, CancellationToken ct) => SendAsync(P2pCommands.Addr, msg.Serialize(), ct);
    public ValueTask SendTxAsync(ReadOnlyMemory<byte> rawTx, CancellationToken ct) => SendAsync(P2pCommands.Tx, rawTx, ct);
    public ValueTask SendGetAddrAsync(CancellationToken ct) => SendAsync(P2pCommands.GetAddr, ReadOnlyMemory<byte>.Empty, ct);

    /// <summary>
    /// Fires when peer sends an <c>addr</c> message. The handler is invoked
    /// inline from the receive loop, so it should be fast and non-blocking
    /// (or fire-and-forget Task.Run for async work).
    /// </summary>
    public Action<IReadOnlyList<TimedAddress>>? OnAddrReceived { get; set; }

    // ----- internal loops -----

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && State == PeerSessionState.Ready)
            {
                var frame = await ReadNextFrameAsync(ct);
                Volatile.Write(ref _lastInboundUtcTicks, DateTime.UtcNow.Ticks);

                switch (frame.Command)
                {
                    case P2pCommands.Ping:
                        // Reply pong immediately, do not bubble up.
                        await _stream!.WriteAsync(FrameCodec.Encode(_network, P2pCommands.Pong, frame.Payload), ct);
                        continue;
                    case P2pCommands.Pong:
                        // Peer responded to our ping; nothing to do.
                        continue;
                    case P2pCommands.Protoconf:
                        ApplyProtoconf(frame.Payload);
                        continue;
                    case P2pCommands.FeeFilter:
                        ApplyFeeFilter(frame.Payload);
                        continue;
                    case P2pCommands.Verack:
                        // Spurious / late verack — ignore.
                        continue;
                    case P2pCommands.Addr:
                        // addr-gossip is consumed by PeerManager via the OnAddrReceived
                        // callback. Don't push to inbound channel — multiple consumers race.
                        if (OnAddrReceived is not null)
                        {
                            try
                            {
                                var addrMsg = AddrMessage.Parse(frame.Payload);
                                OnAddrReceived(addrMsg.Addresses);
                            }
                            catch { /* ignore malformed gossip */ }
                        }
                        continue;
                }

                // Surface everything else to the caller via the channel.
                await _inbound.Writer.WriteAsync(new InboundFrame(frame.Command, frame.Payload), ct);
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (P2pDecodeException ex)
        {
            await EndWith(DisconnectReason.ProtocolViolation, ex.Message);
        }
        catch (Exception ex) when (IsConnectionLost(ex))
        {
            await EndWith(ClassifyConnectionLost(ex), ex.Message);
        }
        catch (Exception ex)
        {
            await EndWith(DisconnectReason.InternalError, ex.Message);
        }
        finally
        {
            _inbound.Writer.TryComplete();
        }
    }

    private async Task SendLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _outbound.Reader.WaitToReadAsync(ct))
            {
                while (_outbound.Reader.TryRead(out var frame))
                {
                    await _stream!.WriteAsync(frame, ct);
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex) when (IsConnectionLost(ex))
        {
            await EndWith(ClassifyConnectionLost(ex), ex.Message);
        }
        catch (Exception ex)
        {
            await EndWith(DisconnectReason.InternalError, ex.Message);
        }
    }

    private async Task PingLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && State == PeerSessionState.Ready)
            {
                await Task.Delay(_config.PingInterval, ct);

                // Idle watchdog: if peer has been silent for too long, declare dead.
                var lastInbound = new DateTime(Volatile.Read(ref _lastInboundUtcTicks), DateTimeKind.Utc);
                if (DateTime.UtcNow - lastInbound > _config.IdleTimeout)
                {
                    await EndWith(DisconnectReason.IdleTimeout, $"No inbound traffic for {_config.IdleTimeout}");
                    return;
                }

                // Send our own ping. Nonce-correlation isn't required at this level
                // (we don't drop the connection if pong is missing; pong tracking
                // is implicit via inbound activity).
                var nonce = (ulong)Random.Shared.NextInt64();
                await SendAsync(P2pCommands.Ping, new PingMessage(nonce).Serialize(), ct);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ping loop error on {Peer}", _remote);
        }
    }

    // ----- frame read helper -----

    private async Task<Frame> ReadNextFrameAsync(CancellationToken ct)
    {
        // Read 24-byte header, then payload-length bytes, decode, validate.
        if (_stream is null) throw new InvalidOperationException("Stream not initialised");

        var header = new byte[Frame.HeaderSize];
        await ReadExactAsync(_stream, header, ct);

        // Quick decode of length without going through FrameCodec twice; we still
        // run TryDecode for full validation once we have the full bytes.
        var declaredLength = (uint)(header[16] | (header[17] << 8) | (header[18] << 16) | (header[19] << 24));
        if (declaredLength > (uint)_config.InitialMaxRecvPayloadLength)
            throw new P2pDecodeException($"Inbound payload {declaredLength}B exceeds limit {_config.InitialMaxRecvPayloadLength}B");

        var fullBuffer = new byte[Frame.HeaderSize + (int)declaredLength];
        Buffer.BlockCopy(header, 0, fullBuffer, 0, Frame.HeaderSize);
        if (declaredLength > 0)
        {
            var payloadSegment = new ArraySegment<byte>(fullBuffer, Frame.HeaderSize, (int)declaredLength);
            await ReadExactAsync(_stream, payloadSegment, ct);
        }

        var result = FrameCodec.TryDecode(_network, fullBuffer, _config.InitialMaxRecvPayloadLength, out var frame, out _);
        switch (result)
        {
            case DecodeResult.Ok:
                return frame!;
            case DecodeResult.BadMagic:
                throw new P2pDecodeException("Bad magic on inbound frame");
            case DecodeResult.BadCommand:
                throw new P2pDecodeException("Malformed command field on inbound frame");
            case DecodeResult.BadChecksum:
                throw new P2pDecodeException("Bad checksum on inbound frame");
            case DecodeResult.OversizedPayload:
                throw new P2pDecodeException("Oversized payload on inbound frame");
            case DecodeResult.NeedMore:
                throw new P2pDecodeException("Truncated frame after header indicated full payload — should not happen");
            default:
                throw new P2pDecodeException($"Unknown frame decode result {result}");
        }
    }

    private static async Task ReadExactAsync(NetworkStream stream, ArraySegment<byte> destination, CancellationToken ct)
    {
        var offset = 0;
        while (offset < destination.Count)
        {
            var read = await stream.ReadAsync(destination.Slice(offset), ct);
            if (read == 0)
                throw new EndOfStreamException("Peer closed connection");
            offset += read;
        }
    }

    private static Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct) =>
        ReadExactAsync(stream, new ArraySegment<byte>(buffer), ct);

    // ----- helpers -----

    private void ApplyProtoconf(byte[] payload)
    {
        try
        {
            var pc = ProtoconfMessage.Parse(payload);
            PeerMaxRecvPayloadLength = (int)Math.Min(pc.MaxRecvPayloadLength, int.MaxValue);
            _logger.LogDebug("protoconf from {Peer}: maxRecv={MaxRecv} streamPolicies={Streams}",
                _remote, PeerMaxRecvPayloadLength, pc.StreamPolicies);
        }
        catch (P2pDecodeException ex)
        {
            _logger.LogDebug(ex, "Ignoring malformed protoconf from {Peer}", _remote);
        }
    }

    private void ApplyFeeFilter(byte[] payload)
    {
        try
        {
            var ff = FeeFilterMessage.Parse(payload);
            PeerMinFeePerKbSat = ff.FeePerKbSat;
            _logger.LogDebug("feefilter from {Peer}: {Fee} sat/kb", _remote, PeerMinFeePerKbSat);
        }
        catch (P2pDecodeException ex)
        {
            _logger.LogDebug(ex, "Ignoring malformed feefilter from {Peer}", _remote);
        }
    }

    private static bool IsConnectionLost(Exception ex) =>
        ex is EndOfStreamException
        || ex is SocketException
        || ex is IOException
        || (ex is AggregateException ae && ae.InnerException is SocketException or IOException);

    private static DisconnectReason ClassifyConnectionLost(Exception ex)
    {
        if (ex is EndOfStreamException) return DisconnectReason.PeerClosed;
        if (ex is SocketException sx && sx.SocketErrorCode == SocketError.ConnectionReset) return DisconnectReason.PeerReset;
        return DisconnectReason.PeerClosed;
    }

    private async Task<HandshakeResult> EndWith(DisconnectReason reason, string? detail = null)
    {
        await EndAsync(reason);
        return HandshakeResult.Failed(reason, detail);
    }

    private async Task EndAsync(DisconnectReason reason)
    {
        if (State == PeerSessionState.Closed) return;
        State = PeerSessionState.Closing;
        try
        {
            _internalCts.Cancel();
            _inbound.Writer.TryComplete();
            _outbound.Writer.TryComplete();
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            // Wait for background loops to settle (best-effort).
            var loops = new List<Task>();
            if (_receiveLoop is not null) loops.Add(_receiveLoop);
            if (_sendLoop is not null) loops.Add(_sendLoop);
            if (_pingLoop is not null) loops.Add(_pingLoop);
            if (loops.Count > 0)
            {
                // Bounded wait — don't get stuck in Closing if a loop hangs.
                try
                {
                    var allLoops = Task.WhenAll(loops);
                    var done = await Task.WhenAny(allLoops, Task.Delay(TimeSpan.FromSeconds(5)));
                    // either allLoops or delay finished — we proceed regardless
                    _ = done; // suppress unused
                }
                catch { /* swallow */ }
            }
        }
        finally
        {
            State = PeerSessionState.Closed;
            _completion.TrySetResult(reason);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await EndAsync(DisconnectReason.Local);
        _internalCts.Dispose();
    }
}
