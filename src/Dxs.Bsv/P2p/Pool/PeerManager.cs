#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Session;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dxs.Bsv.P2p.Pool;

/// <summary>
/// Maintains a pool of <see cref="PeerSession"/> connections to the BSV
/// network. Bootstraps from DNS seeds and config, replenishes when sessions
/// drop, and respects per-/24 diversity + rate-limited fanout (audit H3).
/// </summary>
public sealed class PeerManager : IAsyncDisposable
{
    private readonly P2pNetwork _network;
    private readonly PeerDiscovery _discovery;
    private readonly IPeerStore _store;
    private readonly PeerManagerConfig _config;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, PeerSession> _active = new();
    private readonly SemaphoreSlim _bootstrapGate;
    private Task? _maintenanceLoop;
    private Task? _dnsRefreshLoop;
    private int _disposed;

    public PeerManager(P2pNetwork network, PeerDiscovery discovery, IPeerStore store, PeerManagerConfig config, ILogger? logger = null)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? NullLogger.Instance;
        _bootstrapGate = new SemaphoreSlim(_config.BootstrapMaxConcurrency, _config.BootstrapMaxConcurrency);
    }

    public IReadOnlyDictionary<string, PeerSession> ActiveSessions => _active;
    public int PoolSize => _active.Count;
    public int TargetPoolSize => _config.TargetPoolSize;

    /// <summary>Distinct /24 subnets currently represented in the pool.</summary>
    public int Subnet24Diversity =>
        _active.Keys.Select(k => Subnet24Of(k)).Distinct(StringComparer.Ordinal).Count();

    /// <summary>
    /// Start the maintenance loop. Returns immediately; the loop runs until <see cref="DisposeAsync"/>.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_maintenanceLoop is not null) throw new InvalidOperationException("Already started");

        // Seed hardcoded fallback peers BEFORE DNS — gives us a guaranteed bootstrap
        // path even if DNS seeders return only banned nodes.
        if (_config.EnableFallbackSeeds)
        {
            try
            {
                var seeded = await _discovery.SeedFromFallbackAsync(cancellationToken);
                if (seeded > 0)
                    _logger.LogInformation("Fallback seeds added: {Count}", seeded);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Fallback seed registration failed");
            }
        }

        _maintenanceLoop = Task.Run(() => MaintenanceLoopAsync(_cts.Token));
        _dnsRefreshLoop = Task.Run(() => DnsRefreshLoopAsync(_cts.Token));
    }

    private async Task MaintenanceLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await TickAsync(ct);
                try { await Task.Delay(_config.MaintenanceInterval, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PeerManager maintenance loop crashed");
        }
    }

    private async Task DnsRefreshLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var added = await _discovery.RefreshFromDnsSeedsAsync(ct);
                    if (added > 0)
                        _logger.LogInformation("DNS seeds refreshed: +{Added} new candidates", added);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DNS seed refresh failed");
                }
                try { await Task.Delay(_config.DnsRefreshInterval, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        // Reap closed sessions
        foreach (var (key, session) in _active.ToArray())
        {
            if (session.State == PeerSessionState.Closed)
            {
                _active.TryRemove(key, out _);
                _logger.LogDebug("Reaped closed session {Peer}", key);
            }
        }

        var deficit = _config.TargetPoolSize - _active.Count;
        if (deficit <= 0) return;

        var excludeKeys = new HashSet<string>(_active.Keys, StringComparer.Ordinal);
        var excludeSubnets = new HashSet<string>(_active.Keys.Select(Subnet24Of), StringComparer.Ordinal);

        // Get a few times deficit so we have room to skip /24-overlap candidates.
        var candidates = await _store.SelectCandidatesAsync(deficit * 4, excludeKeys, excludeSubnets, ct);

        var tasks = new List<Task>();
        foreach (var candidate in candidates)
        {
            if (_active.Count + tasks.Count >= _config.TargetPoolSize) break;
            tasks.Add(TryConnectAsync(candidate, ct));
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    private async Task TryConnectAsync(PeerRecord candidate, CancellationToken ct)
    {
        // Rate-limit + jitter (audit H3)
        await _bootstrapGate.WaitAsync(ct);
        try
        {
            if (_config.BootstrapJitter > TimeSpan.Zero)
            {
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next((int)_config.BootstrapJitter.TotalMilliseconds));
                try { await Task.Delay(jitter, ct); } catch (OperationCanceledException) { return; }
            }

            if (_active.ContainsKey(candidate.Key)) return;

            var session = new PeerSession(_network, candidate.EndPoint, _config.SessionConfig, _logger);
            var version = _config.VersionFactory();
            var result = await session.ConnectAsync(version, ct);

            if (!result.Success)
            {
                await _discovery.RecordConnectAttemptAsync(candidate.EndPoint, success: false, failureReason: $"{result.FailureReason}: {result.FailureDetail}", _config.NegativeCooldown, ct);
                _logger.LogDebug("Handshake failed with {Peer}: {Reason}", candidate.EndPoint, result.FailureReason);
                await session.DisposeAsync();
                return;
            }

            // Successful handshake: keep the session, record success.
            if (!_active.TryAdd(candidate.Key, session))
            {
                // Race: someone else opened a session to the same peer. Drop ours.
                await session.DisposeAsync();
                return;
            }

            var enriched = candidate with
            {
                UserAgent = result.PeerVersion?.UserAgent,
                ProtocolVersion = result.PeerVersion?.ProtocolVersion,
                Services = result.PeerVersion?.Services,
            };
            await _discovery.RecordConnectAttemptAsync(candidate.EndPoint, success: true, failureReason: null, TimeSpan.Zero, ct);
            await _store.UpsertAsync(enriched with
            {
                LastSeenUtc = DateTime.UtcNow,
                LastConnectedUtc = DateTime.UtcNow,
                SuccessCount = (await _store.GetAsync(candidate.EndPoint, ct))?.SuccessCount + 1 ?? 1,
            }, ct);

            _logger.LogInformation("Peer connected: {Peer} ua={Ua} ver={Ver}", candidate.EndPoint, result.PeerVersion?.UserAgent, result.PeerVersion?.ProtocolVersion);

            // Watch for disconnect so we can replenish later.
            _ = session.Completion.ContinueWith(t =>
            {
                _active.TryRemove(candidate.Key, out _);
            }, TaskScheduler.Default);

            // Absorb addr-gossip so we discover new peers like a real node.
            session.OnAddrReceived = (timedAddrs) =>
            {
                try
                {
                    var endpoints = timedAddrs
                        .Where(a => a.Address.Port > 0)
                        .Select(a => a.Address.TryGetIPv4())
                        .Where(ip => ip is not null)
                        .Select(ip => new IPEndPoint(IPAddress.Parse(ip!), 8333))
                        .Distinct()
                        .ToList();

                    if (endpoints.Count == 0) return;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _discovery.AbsorbAddrGossipAsync(endpoints, CancellationToken.None);
                            _logger.LogInformation("Absorbed {Count} addr from {Peer}", endpoints.Count, candidate.EndPoint);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "AbsorbAddrGossip failed");
                        }
                    });
                }
                catch { /* swallow */ }
            };

            // Ask the new peer for their address book.
            try
            {
                await session.SendGetAddrAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "getaddr failed to {Peer}", candidate.EndPoint);
            }
        }
        finally
        {
            _bootstrapGate.Release();
        }
    }

    private static string Subnet24Of(string key)
    {
        var hostPart = key.LastIndexOf(':') is int idx && idx > 0 ? key[..idx] : key;
        if (IPAddress.TryParse(hostPart, out var addr))
        {
            var bytes = addr.GetAddressBytes();
            if (bytes.Length == 4)
                return $"{bytes[0]}.{bytes[1]}.{bytes[2]}/24";
        }
        return hostPart;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _cts.Cancel(); } catch { }
        if (_maintenanceLoop is not null)
        {
            try { await _maintenanceLoop; } catch { }
        }
        if (_dnsRefreshLoop is not null)
        {
            try { await _dnsRefreshLoop; } catch { }
        }
        foreach (var session in _active.Values)
        {
            try { await session.DisposeAsync(); } catch { }
        }
        _active.Clear();
        _bootstrapGate.Dispose();
        _cts.Dispose();
    }
}
