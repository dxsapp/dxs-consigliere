#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv;
using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Observer;
using Dxs.Bsv.P2p.Session;
using Dxs.Consigliere.Configs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// thin-node-primary-source S2 — on-demand <c>getdata(MSG_TX)</c> rawTx
/// fetch over the live peer pool. Mirrors the
/// <see cref="P2pMempoolIngestRunner"/> getdata→await-tx mechanism but
/// keyed on an arbitrary requested txid rather than an inbound inv.
///
/// <para>
/// Behaviour (see <see cref="IP2pRawTransactionClient"/>): broadcasts the
/// getdata to every Ready peer, subscribes a one-shot <c>tx</c>-frame
/// handler on each via the <see cref="PerSessionDispatcherRegistry"/>,
/// and returns the first frame whose double-SHA256 hash matches the
/// requested txid. Returns <c>null</c> on timeout / no ready peers / P2P
/// off — never throws on a miss, so the fetch service's fallback loop
/// reaches the external providers transparently.
/// </para>
/// </summary>
public sealed class P2pRawTransactionClient : IP2pRawTransactionClient
{
    private readonly BsvP2pHealth _health;
    private readonly PerSessionDispatcherRegistry _dispatcherRegistry;
    private readonly BsvP2pConfig _config;
    private readonly ILogger<P2pRawTransactionClient> _logger;

    public P2pRawTransactionClient(
        BsvP2pHealth health,
        PerSessionDispatcherRegistry dispatcherRegistry,
        IOptions<BsvP2pConfig> config,
        ILogger<P2pRawTransactionClient> logger)
    {
        _health = health;
        _dispatcherRegistry = dispatcherRegistry;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<byte[]?> TryGetRawAsync(string txId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(txId)) return null;

        // P2P off / not bound → nothing to ask. Null lets the fetch
        // service move to the next provider.
        if (!_config.Enabled || !_health.Bound) return null;

        // Normalise the requested txid into the wire-order bytes packed
        // into the getdata vector, and keep them to verify the inbound
        // tx frame. A malformed id is a clean miss, not a throw.
        byte[] wireTxid;
        try
        {
            wireTxid = TxHashOrder.DisplayHexToWire(txId);
        }
        catch (ArgumentException)
        {
            _logger.LogDebug("P2p rawTx fetch: malformed txid {TxId}", txId);
            return null;
        }

        var sessions = _health.ActiveSessions
            .Where(s => s.State == PeerSessionState.Ready)
            .ToArray();
        if (sessions.Length == 0) return null;

        var fired = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriptions = new List<IDisposable>(sessions.Length);
        var tag = $"rawtx.fetch.{txId[..Math.Min(8, txId.Length)]}";

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1, _config.RawTxFetchTimeoutMs)));

        try
        {
            foreach (var session in sessions)
            {
                PerSessionFrameDispatcher dispatcher;
                try
                {
                    dispatcher = _dispatcherRegistry.For(session);
                }
                catch (Exception ex)
                {
                    // Dispatcher wiring failure for one peer must not sink
                    // the whole fetch — log + skip, try the others.
                    _logger.LogDebug(ex, "P2p rawTx fetch: dispatcher unavailable for {Peer}", session.Remote);
                    continue;
                }

                var sub = dispatcher.Subscribe(P2pCommands.Tx, tag, frame =>
                {
                    if (fired.Task.IsCompleted) return Task.CompletedTask;

                    // Each tx frame is the full raw tx; recompute its
                    // double-SHA256 and compare against the requested
                    // wire-order txid before accepting (reject mismatches).
                    byte[] gotTxid;
                    try { gotTxid = Hash.Sha256Sha256(frame.Payload); }
                    catch { return Task.CompletedTask; }
                    if (!gotTxid.AsSpan().SequenceEqual(wireTxid)) return Task.CompletedTask;

                    fired.TrySetResult(frame.Payload);
                    return Task.CompletedTask;
                });
                subscriptions.Add(sub);

                // Issue getdata to this peer. A send failure on one peer
                // is non-fatal — keep asking the rest.
                try
                {
                    var getData = new GetDataMessage(new[] { new InvVector(InvType.Tx, wireTxid) });
                    await session.SendGetDataAsync(getData, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "P2p rawTx fetch: getdata send failed for {Peer}", session.Remote);
                }
            }

            // No peer accepted a getdata send → nothing in flight.
            if (subscriptions.Count == 0) return null;

            var completed = await Task.WhenAny(
                fired.Task,
                Task.Delay(Timeout.Infinite, timeoutCts.Token));

            if (completed == fired.Task)
                return fired.Task.Result;

            // Timeout or caller cancellation → clean miss for fallback.
            return null;
        }
        catch (OperationCanceledException)
        {
            // Linked-token cancellation (timeout / caller). Treat as miss.
            return null;
        }
        finally
        {
            foreach (var sub in subscriptions)
            {
                try { sub.Dispose(); } catch { /* best-effort */ }
            }
        }
    }
}
