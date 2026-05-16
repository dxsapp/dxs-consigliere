using System.Net;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Pool;
using Dxs.Bsv.P2p.Session;

using Microsoft.Extensions.Options;

namespace BsvBroadcastNode;

/// <summary>
/// Hosted service that owns <see cref="PeerManager"/> + <see cref="PeerDiscovery"/>.
/// Starts on application startup; stopped on shutdown.
/// </summary>
public sealed class PeerNodeHost(
    LogRing log,
    IOptions<BroadcastNodeOptions> opts,
    ILogger<PeerNodeHost> logger) : IHostedService, IAsyncDisposable
{
    private readonly BroadcastNodeOptions _opts = opts.Value;
    private PeerManager? _manager;
    private InMemoryPeerStore? _store;
    private Task? _inboundLoop;
    private CancellationTokenSource? _inboundCts;

    public PeerManager? Manager => _manager;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var network = P2pNetwork.Mainnet;
        _store = new InMemoryPeerStore();
        var discovery = new PeerDiscovery(network, _store, logger);

        // Add operator-supplied extra peers
        foreach (var raw in _opts.ParseExtraPeers())
        {
            if (TryParseEndpoint(raw, network.DefaultPort, out var ep))
            {
                await discovery.AddSeedAsync(ep, cancellationToken);
                log.Add("info", $"Added seed peer {ep}");
            }
        }

        var version = BuildVersion();

        var config = new PeerManagerConfig
        {
            TargetPoolSize = _opts.PoolSize,
            BootstrapMaxConcurrency = 4,
            BootstrapJitter = TimeSpan.FromMilliseconds(200),
            NegativeCooldown = TimeSpan.FromMinutes(15),
            MaintenanceInterval = TimeSpan.FromSeconds(15),
            DnsRefreshInterval = TimeSpan.FromHours(6),
            VersionFactory = () => version,
            SessionConfig = new PeerSessionConfig
            {
                ConnectTimeout = TimeSpan.FromSeconds(5),
                HandshakeTimeout = TimeSpan.FromSeconds(30),
                SendProtoconfAfterVerack = true,
            },
        };

        _manager = new PeerManager(network, discovery, _store, config, logger);
        await _manager.StartAsync(cancellationToken);

        // Optional: inbound BSV P2P listener on port 8333
        if (_opts.ListenPort > 0)
        {
            _inboundCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _inboundLoop = Task.Run(() => InboundListenerAsync(network, _inboundCts.Token));
            log.Add("info", $"BSV P2P listener started on :{_opts.ListenPort}");
        }

        log.Add("info", $"BSV BroadcastNode started — target pool {_opts.PoolSize}, UA: {_opts.UserAgent}");
        logger.LogInformation("BSV BroadcastNode started. Pool target: {Target}, UA: {UA}",
            _opts.PoolSize, _opts.UserAgent);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _inboundCts?.Cancel();
        if (_inboundLoop is not null)
            try { await _inboundLoop; } catch { }
        if (_manager is not null)
            await _manager.DisposeAsync();
        log.Add("info", "BSV BroadcastNode stopped");
    }

    public async ValueTask DisposeAsync()
    {
        _inboundCts?.Cancel();
        if (_manager is not null)
            await _manager.DisposeAsync();
    }

    private async Task InboundListenerAsync(P2pNetwork network, CancellationToken ct)
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Any, _opts.ListenPort);
        listener.Start();
        log.Add("info", $"Inbound listener accepting on :{_opts.ListenPort}");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleInboundAsync(client, network, ct), ct);
            }
        }
        catch (OperationCanceledException) { }
        finally { listener.Stop(); }
    }

    private async Task HandleInboundAsync(
        System.Net.Sockets.TcpClient client,
        P2pNetwork network,
        CancellationToken ct)
    {
        var remote = client.Client.RemoteEndPoint?.ToString() ?? "?";
        log.Add("info", $"Inbound TCP from {remote}");
        try
        {
            using var stream = client.GetStream();
            var deadline = DateTime.UtcNow.AddSeconds(60);

            // Server-side BSV handshake: wait for client's version, reply version+verack+protoconf.
            var headerBuf = new byte[Dxs.Bsv.P2p.Frame.HeaderSize];
            await ReadExactAsync(stream, headerBuf, ct);
            var magic = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(headerBuf.AsSpan(0, 4));
            if (magic != network.Magic) { log.Add("warn", $"Bad magic from {remote}: 0x{magic:x8}"); return; }

            var cmd = System.Text.Encoding.ASCII.GetString(headerBuf, 4, 12).TrimEnd('\0');
            var len = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(headerBuf.AsSpan(16, 4));
            var payload = new byte[len];
            if (len > 0) await ReadExactAsync(stream, payload, ct);

            if (cmd != "version") { log.Add("warn", $"Expected 'version', got '{cmd}' from {remote}"); return; }

            // Parse peer UA for logging
            var peerUa = "";
            try { if (payload.Length > 84) { var n = payload[84]; peerUa = System.Text.Encoding.ASCII.GetString(payload, 85, n); } } catch { }
            log.Add("info", $"INBOUND version from {remote} ua='{peerUa}'");

            // Send our version + verack + protoconf
            var ourVer = BuildVersion();
            var verFrame = Dxs.Bsv.P2p.FrameCodec.Encode(network, "version", ourVer.Serialize());
            var vaFrame = Dxs.Bsv.P2p.FrameCodec.Encode(network, "verack", Array.Empty<byte>());
            var pcFrame = Dxs.Bsv.P2p.FrameCodec.Encode(network, "protoconf",
                new Dxs.Bsv.P2p.Messages.ProtoconfMessage(2097152, "Default").Serialize());
            await stream.WriteAsync(verFrame, ct);
            await stream.WriteAsync(vaFrame, ct);
            await stream.WriteAsync(pcFrame, ct);

            // Wait for peer's verack
            await ReadExactAsync(stream, headerBuf, ct);
            var cmd2 = System.Text.Encoding.ASCII.GetString(headerBuf, 4, 12).TrimEnd('\0');
            var len2 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(headerBuf.AsSpan(16, 4));
            if (len2 > 0) await ReadExactAsync(stream, new byte[len2], ct);
            log.Add("info", $"INBOUND HANDSHAKE COMPLETE with {remote} ua='{peerUa}'");

            // Keep pinging
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(120_000, ct);
                var ping = Dxs.Bsv.P2p.FrameCodec.Encode(network, "ping",
                    BitConverter.GetBytes((ulong)Random.Shared.NextInt64()));
                await stream.WriteAsync(ping, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { log.Add("warn", $"Inbound {remote} error: {ex.Message}"); }
        finally { client.Close(); log.Add("info", $"Inbound {remote} disconnected"); }
    }

    private static async Task ReadExactAsync(System.Net.Sockets.NetworkStream s, byte[] buf, CancellationToken ct)
    {
        var off = 0;
        while (off < buf.Length)
        {
            var n = await s.ReadAsync(buf.AsMemory(off), ct);
            if (n == 0) throw new System.IO.EndOfStreamException("Peer closed");
            off += n;
        }
    }

    private VersionMessage BuildVersion() => new(
        ProtocolVersion: VersionMessage.CurrentProtocolVersion,
        Services: _opts.Services,
        TimestampUnixSeconds: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        AddrRecv: P2pAddress.Anonymous(0x01),
        AddrFrom: P2pAddress.Anonymous(_opts.Services),
        Nonce: (ulong)Random.Shared.NextInt64(),
        UserAgent: _opts.UserAgent,
        StartHeight: 0,
        Relay: true,
        AssociationId: null);

    private static bool TryParseEndpoint(string raw, int defaultPort, out IPEndPoint endpoint)
    {
        endpoint = default!;
        var colon = raw.LastIndexOf(':');
        string host; int port = defaultPort;
        if (colon > 0 && !raw.Contains("::"))
        {
            host = raw[..colon];
            if (!int.TryParse(raw[(colon + 1)..], out port)) return false;
        }
        else host = raw;
        if (!IPAddress.TryParse(host, out var addr)) return false;
        endpoint = new IPEndPoint(addr, port);
        return true;
    }
}
