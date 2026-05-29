using System;
using System.Net;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Pool;
using Dxs.Bsv.P2p.Session;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Data.Runtime;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Raven.Client.Documents;
using Raven.Client.Documents.Changes;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Supervisor for the Gate 2 BSV thin-node peer pool. The effective
/// enable state is <c>OperatorRuntimeSettings.P2pEnabled ??
/// BsvP2pConfig.Enabled</c> (DB override seeded from config). The service
/// always starts, brings the pool UP when enabled and tears it DOWN when
/// disabled, and watches the runtime-settings document via the RavenDB
/// Changes API so a wizard completion (or any operator toggle) takes
/// effect LIVE — no restart (wizard-enabled-p2p-runtime-toggle S2).
///
/// The live <see cref="PeerManager"/> is exposed via
/// <see cref="BsvP2pHealth"/>; the mempool observer keys off
/// <see cref="BsvP2pHealth.Bound"/>, so it follows the pool automatically.
/// </summary>
public sealed class BsvP2pHostedService : IHostedService, IAsyncDisposable
{
    internal enum PoolAction { None, Start, Stop }

    private readonly BsvP2pConfig _config;
    private readonly BsvP2pHealth _health;
    private readonly IOperatorRuntimeSettingsService _runtimeSettings;
    private readonly IDocumentStore _documentStore;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<BsvP2pHostedService> _logger;
    private readonly IPeerScoringPolicy? _scoringPolicy;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PeerManager? _manager;
    private InMemoryPeerStore? _store;
    private IDisposable? _settingsSubscription;
    private CancellationTokenSource? _lifetimeCts;
    private bool _poolRunning;

    public BsvP2pHostedService(
        IOptions<BsvP2pConfig> config,
        BsvP2pHealth health,
        IOperatorRuntimeSettingsService runtimeSettings,
        IDocumentStore documentStore,
        ILoggerFactory loggerFactory,
        IPeerScoringPolicy? scoringPolicy = null)
    {
        _config = config.Value;
        _health = health;
        _runtimeSettings = runtimeSettings;
        _documentStore = documentStore;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<BsvP2pHostedService>();
        _scoringPolicy = scoringPolicy;
    }

    /// <summary>Pure decision: what to do given effective-enabled + current pool state.</summary>
    internal static PoolAction DecideAction(bool enabled, bool running)
        => enabled && !running ? PoolAction.Start
            : !enabled && running ? PoolAction.Stop
            : PoolAction.None;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Wave 6 S4 — record the operator's inbound-listener decision
        // on the health surface, regardless of whether the broader
        // P2P subsystem is enabled. If the flag is on, emit a single
        // operator warning: the W6 release ships no listener thread.
        _health.SetInboundEnabled(_config.Inbound.Enabled);
        if (_config.Inbound.Enabled)
        {
            _logger.LogWarning(
                "BsvP2pConfig.Inbound.Enabled = true on port {Port}, but inbound listener is NOT implemented in this release. " +
                "The flag is reserved for a future wave; no accept thread will spin.",
                _config.Inbound.ListenPort);
        }

        _lifetimeCts = new CancellationTokenSource();

        // Watch the runtime-settings doc so a wizard/operator toggle flips
        // the pool live. Subscribe BEFORE the initial reconcile so a change
        // racing startup is not missed (the gate + re-read converge).
        try
        {
            _settingsSubscription = _documentStore
                .Changes()
                .ForDocument(OperatorRuntimeSettingsDocument.DocumentId)
                .Where(c => c.Type is DocumentChangeTypes.Put or DocumentChangeTypes.Delete)
                .Subscribe(change => { _ = ReconcileAsync(); });
        }
        catch (Exception ex)
        {
            // A missing changes connection must not block the host; the
            // initial reconcile below still applies the boot-time state.
            _logger.LogWarning(ex, "BSV P2P: could not subscribe to runtime-settings changes; live toggle disabled until restart.");
        }

        await ReconcileAsync();
    }

    /// <summary>
    /// Converge the pool to the effective enable state. Serialized by
    /// <see cref="_gate"/> so the Changes-API callback and the boot call
    /// never start/stop concurrently; the state is re-read inside the lock.
    /// </summary>
    private async Task ReconcileAsync()
    {
        var lifetime = _lifetimeCts;
        if (lifetime is null || lifetime.IsCancellationRequested) return;

        var acquired = false;
        try
        {
            await _gate.WaitAsync(lifetime.Token).ConfigureAwait(false);
            acquired = true;

            var enabled = await _runtimeSettings.GetP2pEnabledAsync(lifetime.Token);
            switch (DecideAction(enabled, _poolRunning))
            {
                case PoolAction.Start:
                    await StartPoolAsync(lifetime.Token);
                    _poolRunning = true;
                    break;
                case PoolAction.Stop:
                    await StopPoolAsync();
                    _poolRunning = false;
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down — nothing to do.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BSV P2P reconcile failed; pool left in its previous state.");
        }
        finally
        {
            if (acquired) _gate.Release();
        }
    }

    private async Task StartPoolAsync(CancellationToken cancellationToken)
    {
        if (!string.Equals(_config.Network, "mainnet", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("BSV P2P enabled but network '{Network}' is not supported (mainnet only). Pool not started.", _config.Network);
            return;
        }

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
                // Audit W2 M4: raise the inbound payload cap so mempool
                // tx beyond the legacy 2 MiB default still get accepted.
                InitialMaxRecvPayloadLength = _config.MempoolMaxFetchedTxBytes,
            },
            // Wave 6 S7 — score-aware rotation activates when both a
            // scoring policy AND a non-null RotationPolicy are present.
            // Defaults match master.md §"Per-peer scoring + rotation".
            RotationPolicy = _scoringPolicy is null
                ? null
                : new PeerRotationPolicy(),
        };

        if (_config.MempoolMaxFetchedTxBytes < 4 * 1024 * 1024)
        {
            // Audit W2 M4 operator-warning rule: most modern BSV
            // mempools see legitimate tx beyond the legacy 2 MiB cap.
            _logger.LogWarning(
                "BsvP2pConfig.MempoolMaxFetchedTxBytes is set to {Bytes}B (<4 MiB). " +
                "BSV mainnet mempool tx routinely exceed this; observation will silently drop oversize payloads.",
                _config.MempoolMaxFetchedTxBytes);
        }

        _manager = new PeerManager(
            network, discovery, _store, pmConfig,
            _loggerFactory.CreateLogger<PeerManager>(),
            scoringPolicy: _scoringPolicy);
        _health.Bind(_manager, _store);
        await _manager.StartAsync(cancellationToken);

        _logger.LogInformation("BSV P2P thin-node started: target pool size {Target}, UA {UA}", _config.PoolSize, _config.UserAgent);
    }

    private async Task StopPoolAsync()
    {
        if (_manager is not null)
        {
            await _manager.DisposeAsync();
            _manager = null;
        }
        _store = null;
        _health.Unbind();
        _logger.LogInformation("BSV P2P thin-node stopped.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _settingsSubscription?.Dispose();
        _settingsSubscription = null;

        if (_lifetimeCts is not null)
        {
            await _lifetimeCts.CancelAsync();
        }

        // Acquire the gate (CancellationToken.None) so we don't race an
        // in-flight reconcile mutating the pool — it bails fast now that
        // the lifetime token is cancelled. Then tear the pool down.
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            if (_poolRunning)
            {
                await StopPoolAsync();
                _poolRunning = false;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _settingsSubscription?.Dispose();
        if (_manager is not null)
        {
            await _manager.DisposeAsync();
            _manager = null;
        }
        _lifetimeCts?.Dispose();
        _gate.Dispose();
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
