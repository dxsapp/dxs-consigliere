using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Pool;
using Dxs.Bsv.P2p.Session;
using Dxs.Consigliere.Configs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Hosted-service host for the Gate 2 BSV thin-node peer pool. Disabled by
/// default; enable via <c>Consigliere:Broadcast:P2p:Enabled = true</c>.
/// Exposes the live <see cref="PeerManager"/> via <see cref="BsvP2pHealth"/>
/// for diagnostics/admin endpoints (Gate 4).
/// </summary>
public sealed class BsvP2pHostedService : IHostedService, IAsyncDisposable
{
    private readonly BsvP2pConfig _config;
    private readonly BsvP2pHealth _health;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<BsvP2pHostedService> _logger;
    private PeerManager? _manager;
    private InMemoryPeerStore? _store;

    public BsvP2pHostedService(IOptions<BsvP2pConfig> config, BsvP2pHealth health, ILoggerFactory loggerFactory)
    {
        _config = config.Value;
        _health = health;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<BsvP2pHostedService>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("BSV P2P thin-node disabled (Consigliere:Broadcast:P2p:Enabled = false). Skipping startup.");
            return;
        }

        if (!string.Equals(_config.Network, "mainnet", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Network '{_config.Network}' is not supported in Phase 1. Only 'mainnet'.");

        var network = P2pNetwork.Mainnet;

        _store = new InMemoryPeerStore();
        var discovery = new PeerDiscovery(network, _store, _loggerFactory.CreateLogger<PeerDiscovery>());

        foreach (var raw in _config.InitialPeers)
        {
            if (TryParseEndpoint(raw, network.DefaultPort, out var ep))
                await discovery.AddSeedAsync(ep, cancellationToken);
            else
                _logger.LogWarning("Ignoring malformed InitialPeer '{Raw}'", raw);
        }

        var version = BuildOurVersion();
        var pmConfig = new PeerManagerConfig
        {
            TargetPoolSize = _config.PoolSize,
            BootstrapMaxConcurrency = _config.BootstrapMaxConcurrency,
            BootstrapJitter = TimeSpan.FromMilliseconds(_config.BootstrapJitterMs),
            NegativeCooldown = TimeSpan.FromMinutes(_config.NegativeCooldownMinutes),
            MaintenanceInterval = TimeSpan.FromSeconds(_config.MaintenanceIntervalSeconds),
            DnsRefreshInterval = TimeSpan.FromHours(_config.DnsRefreshIntervalHours),
            VersionFactory = () => version,
            SessionConfig = new PeerSessionConfig
            {
                ConnectTimeout = TimeSpan.FromMilliseconds(_config.ConnectTimeoutMs),
                HandshakeTimeout = TimeSpan.FromMilliseconds(_config.HandshakeTimeoutMs),
                SendProtoconfAfterVerack = _config.SendProtoconfAfterVerack,
            },
        };

        _manager = new PeerManager(network, discovery, _store, pmConfig, _loggerFactory.CreateLogger<PeerManager>());
        _health.Bind(_manager, _store);
        await _manager.StartAsync(cancellationToken);

        _logger.LogInformation("BSV P2P thin-node started: target pool size {Target}, UA {UA}", _config.PoolSize, _config.UserAgent);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_manager is not null)
        {
            await _manager.DisposeAsync();
            _manager = null;
        }
        _health.Unbind();
        _logger.LogInformation("BSV P2P thin-node stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_manager is not null)
        {
            await _manager.DisposeAsync();
        }
    }

    private VersionMessage BuildOurVersion() => new(
        ProtocolVersion: VersionMessage.CurrentProtocolVersion,
        Services: _config.Services,
        TimestampUnixSeconds: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        AddrRecv: P2pAddress.Anonymous(0x01),
        AddrFrom: P2pAddress.Anonymous(_config.Services),
        Nonce: (ulong)Random.Shared.NextInt64(),
        UserAgent: _config.UserAgent,
        StartHeight: 0,
        Relay: true,
        AssociationId: null);

    private static bool TryParseEndpoint(string raw, int defaultPort, out IPEndPoint endpoint)
    {
        endpoint = default!;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var s = raw.Trim();
        var colon = s.LastIndexOf(':');
        string hostPart;
        int port = defaultPort;
        if (colon > 0 && !s.Contains("::"))
        {
            hostPart = s[..colon];
            if (!int.TryParse(s[(colon + 1)..], out port)) return false;
        }
        else
        {
            hostPart = s;
        }
        if (!IPAddress.TryParse(hostPart, out var address)) return false;
        endpoint = new IPEndPoint(address, port);
        return true;
    }
}
