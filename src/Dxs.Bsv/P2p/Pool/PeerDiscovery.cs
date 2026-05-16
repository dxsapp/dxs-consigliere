#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dxs.Bsv.P2p.Pool;

/// <summary>
/// Resolves BSV mainnet peers from DNS seeds and absorbs peers learned via
/// <c>addr</c> gossip. Hands candidate records to <see cref="IPeerStore"/>.
/// </summary>
public sealed class PeerDiscovery
{
    private readonly P2pNetwork _network;
    private readonly IPeerStore _store;
    private readonly ILogger _logger;

    public PeerDiscovery(P2pNetwork network, IPeerStore store, ILogger? logger = null)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Resolve every configured DNS seed, upsert results into the store.
    /// Returns the count of unique new endpoints added.
    /// </summary>
    public async Task<int> RefreshFromDnsSeedsAsync(CancellationToken ct)
    {
        var added = 0;
        foreach (var seed in _network.DnsSeeds)
        {
            IPHostEntry? entry = null;
            try
            {
                entry = await Dns.GetHostEntryAsync(seed, AddressFamily.InterNetwork, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "DNS seed {Seed} resolution failed", seed);
                continue;
            }

            foreach (var addr in entry.AddressList)
            {
                if (addr.AddressFamily != AddressFamily.InterNetwork) continue;
                var ep = new IPEndPoint(addr, _network.DefaultPort);
                var existing = await _store.GetAsync(ep, ct);
                if (existing is null)
                {
                    await _store.UpsertAsync(new PeerRecord(ep) { Source = PeerSource.DnsSeed }, ct);
                    added++;
                }
                else
                {
                    await _store.UpsertAsync(existing with { LastSeenUtc = DateTime.UtcNow }, ct);
                }
            }
        }
        return added;
    }

    /// <summary>
    /// Add a single seed peer (e.g. from operator config) to the store.
    /// </summary>
    public async Task AddSeedAsync(IPEndPoint endpoint, CancellationToken ct)
    {
        var existing = await _store.GetAsync(endpoint, ct);
        if (existing is null)
        {
            await _store.UpsertAsync(new PeerRecord(endpoint) { Source = PeerSource.Config }, ct);
        }
    }

    /// <summary>
    /// Seed the store with hardcoded fallback peers from <see cref="P2pNetwork.FallbackSeeds"/>.
    /// Used during cold-start so we have a bootstrap path even if DNS seeds
    /// only return nodes that ban us.
    /// </summary>
    public async Task<int> SeedFromFallbackAsync(CancellationToken ct)
    {
        var added = 0;
        foreach (var raw in _network.FallbackSeeds)
        {
            if (!TryParse(raw, _network.DefaultPort, out var ep)) continue;
            var existing = await _store.GetAsync(ep, ct);
            if (existing is null)
            {
                await _store.UpsertAsync(new PeerRecord(ep) { Source = PeerSource.Config }, ct);
                added++;
            }
        }
        return added;
    }

    private static bool TryParse(string raw, int defaultPort, out IPEndPoint endpoint)
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

    /// <summary>
    /// Absorb an <c>addr</c> message's contents. Each timed address is upserted
    /// as a low-priority candidate; if we already know about it, only LastSeen
    /// is bumped (we don't overwrite richer fields).
    /// </summary>
    public async Task AbsorbAddrGossipAsync(IEnumerable<IPEndPoint> addresses, CancellationToken ct)
    {
        foreach (var ep in addresses)
        {
            if (ep.Address.AddressFamily != AddressFamily.InterNetwork) continue;
            if (IPAddress.IsLoopback(ep.Address) || ep.Address.Equals(IPAddress.Any)) continue;
            var existing = await _store.GetAsync(ep, ct);
            if (existing is null)
            {
                await _store.UpsertAsync(new PeerRecord(ep) { Source = PeerSource.AddrGossip }, ct);
            }
            else
            {
                await _store.UpsertAsync(existing with { LastSeenUtc = DateTime.UtcNow }, ct);
            }
        }
    }

    /// <summary>
    /// Mark a connect attempt outcome on the store.
    /// </summary>
    public async Task RecordConnectAttemptAsync(IPEndPoint endpoint, bool success, string? failureReason, TimeSpan negativeCooldown, CancellationToken ct)
    {
        var existing = await _store.GetAsync(endpoint, ct)
                       ?? new PeerRecord(endpoint);

        var now = DateTime.UtcNow;
        if (success)
        {
            await _store.UpsertAsync(existing with
            {
                LastSeenUtc = now,
                LastConnectedUtc = now,
                SuccessCount = existing.SuccessCount + 1,
                NegativeUntilUtc = null,
                LastFailureReason = null,
            }, ct);
        }
        else
        {
            await _store.UpsertAsync(existing with
            {
                LastSeenUtc = now,
                FailCount = existing.FailCount + 1,
                NegativeUntilUtc = now + negativeCooldown,
                LastFailureReason = failureReason,
            }, ct);
        }
    }
}
