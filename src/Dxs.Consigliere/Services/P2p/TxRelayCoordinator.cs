#nullable enable
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Pool;
using Dxs.Bsv.P2p.Session;
using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;

using Microsoft.Extensions.Logging;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wires <see cref="PeerManager"/> to the tx broadcast lifecycle.
///
/// Responsibilities:
/// - Announce pending tx to all ready peers via <c>inv(MSG_TX, txid)</c>.
/// - Serve the raw tx bytes when a peer sends <c>getdata(MSG_TX, txid)</c>.
/// - Track relay-back: when a peer sends us back an <c>inv(txid)</c> after
///   we announced it, that's proof they're propagating it.
/// - Drive state transitions: Dispatching → PeerAcked → PeerRelayed.
///
/// Wave 2 S5.2 refactor: instead of running one
/// <c>IncomingMessages</c> read loop per (txid, session) pair, the
/// coordinator registers shared per-session handlers via
/// <see cref="PerSessionFrameDispatcher"/> ONCE per session — every
/// pending tx then reuses the same subscription. This eliminates the
/// channel race between concurrent broadcast watchers AND between
/// the coordinator and the W2 mempool runner (which is also a
/// dispatcher consumer). Legacy path with the per-session read loop
/// is preserved for tests that don't pass a dispatcher registry.
/// </summary>
public sealed class TxRelayCoordinator
{
    private readonly PeerManager _peerManager;
    private readonly OutgoingTransactionStore _store;
    private readonly ILogger<TxRelayCoordinator> _logger;
    private readonly PerSessionDispatcherRegistry? _dispatcherRegistry;

    // txid → raw hex bytes (populated during Dispatching)
    private readonly ConcurrentDictionary<string, byte[]> _pendingTx = new(StringComparer.OrdinalIgnoreCase);

    // txid → count of peers that sent relay-back inv
    private readonly ConcurrentDictionary<string, int> _relayBackCount = new(StringComparer.OrdinalIgnoreCase);

    // Sessions for which we've already attached dispatcher handlers.
    private readonly ConcurrentDictionary<PeerSession, byte> _wiredSessions = new(ReferenceEqualityComparer.Instance);

    public TxRelayCoordinator(
        PeerManager peerManager,
        OutgoingTransactionStore store,
        ILogger<TxRelayCoordinator> logger,
        PerSessionDispatcherRegistry? dispatcherRegistry = null)
    {
        _peerManager = peerManager;
        _store = store;
        _logger = logger;
        _dispatcherRegistry = dispatcherRegistry;
    }

    /// <summary>
    /// Announce a tx to all currently-ready peers, start listening for
    /// getdata on all their inbound channels, and update document state.
    /// Returns the number of peers we successfully sent inv to.
    /// </summary>
    public async Task<int> AnnounceAsync(string txId, string rawHex, CancellationToken ct)
    {
        var rawBytes = Convert.FromHexString(rawHex);
        _pendingTx[txId] = rawBytes;

        var sessions = _peerManager.ActiveSessions.Values
            .Where(s => s.State == PeerSessionState.Ready)
            .ToList();

        if (sessions.Count == 0)
        {
            _logger.LogWarning("AnnounceAsync: no ready peers for {TxId}", txId);
            return 0;
        }

        var txidBytes = Convert.FromHexString(txId);
        var inv = InvMessage.ForTx(txidBytes);
        var served = 0;

        foreach (var session in sessions)
        {
            try
            {
                await session.SendInvAsync(inv, ct);
                served++;

                if (_dispatcherRegistry is not null)
                {
                    EnsureWiredViaDispatcher(session);
                }
                else
                {
                    // Legacy path: one read loop per (txid, session) pair.
                    _ = WatchSessionLegacyAsync(session, txId, rawBytes, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to announce {TxId} to {Peer}", txId, session.Remote);
            }
        }

        return served;
    }

    /// <summary>Evict a tx from pending tracking (after terminal state).</summary>
    public void Evict(string txId)
    {
        _pendingTx.TryRemove(txId, out _);
        _relayBackCount.TryRemove(txId, out _);
    }

    private void EnsureWiredViaDispatcher(PeerSession session)
    {
        if (!_wiredSessions.TryAdd(session, 0)) return; // already wired

        var dispatcher = _dispatcherRegistry!.For(session);
        dispatcher.Subscribe(P2pCommands.GetData, "tx-relay.getdata",
            frame => HandleGetDataAsync(session, frame));
        dispatcher.Subscribe(P2pCommands.Inv, "tx-relay.inv",
            frame => HandleInvRelayBackAsync(session, frame));
        dispatcher.Subscribe(P2pCommands.Reject, "tx-relay.reject",
            frame => HandleRejectAsync(session, frame));
    }

    private async Task HandleGetDataAsync(PeerSession session, InboundFrame frame)
    {
        GetDataMessage getdata;
        try { getdata = GetDataMessage.Parse(frame.Payload); }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "tx-relay: malformed getdata from {Peer}", session.Remote);
            return;
        }
        foreach (var item in getdata.Items)
        {
            if (item.Type != InvType.Tx) continue;
            var requestedTxId = Convert.ToHexString(item.Hash).ToLowerInvariant();
            if (!_pendingTx.TryGetValue(requestedTxId, out var rawBytes)) continue;

            session.Telemetry.RecordGetDataRequested(InvType.Tx, item.Hash);
            var requestedAt = DateTime.UtcNow;
            await session.SendTxAsync(rawBytes, CancellationToken.None);
            session.Telemetry.RecordGetDataServed(InvType.Tx, item.Hash, DateTime.UtcNow - requestedAt);
            _logger.LogInformation("Served {TxId} to {Peer}", requestedTxId, session.Remote);

            await UpdatePeerAckedAsync(requestedTxId, session);
        }
    }

    private async Task HandleInvRelayBackAsync(PeerSession session, InboundFrame frame)
    {
        InvMessage inv;
        try { inv = InvMessage.Parse(frame.Payload); }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "tx-relay: malformed inv from {Peer}", session.Remote);
            return;
        }
        foreach (var item in inv.Items)
        {
            if (item.Type != InvType.Tx) continue;
            var seenTxId = Convert.ToHexString(item.Hash).ToLowerInvariant();
            if (!_pendingTx.ContainsKey(seenTxId)) continue;

            session.Telemetry.RecordRelayBackInv(item.Hash);

            var count = _relayBackCount.AddOrUpdate(seenTxId, 1, (_, old) => old + 1);
            _logger.LogDebug("Relay-back #{Count} for {TxId} from {Peer}", count, seenTxId, session.Remote);

            if (count >= 2)
            {
                var doc = await _store.GetOrNullAsync(seenTxId);
                if (doc is not null && doc.State == OutgoingTxState.PeerAcked)
                {
                    doc.State = OutgoingTxState.PeerRelayed;
                    await _store.SaveAsync(doc);
                }
            }
        }
    }

    private Task HandleRejectAsync(PeerSession session, InboundFrame frame)
    {
        try
        {
            var reject = RejectMessage.Parse(frame.Payload);
            if (reject.Hash is not null)
            {
                var rejectedTxId = Convert.ToHexString(reject.Hash).ToLowerInvariant();
                if (_pendingTx.ContainsKey(rejectedTxId))
                {
                    _logger.LogWarning("Peer {Peer} rejected {TxId}: {Code} {Reason}",
                        session.Remote, rejectedTxId, reject.Code, reject.Reason);
                    // OutgoingTransactionMonitor handles quorum-based terminal
                    // classification.
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "tx-relay: malformed reject from {Peer}", session.Remote);
        }
        return Task.CompletedTask;
    }

    private async Task UpdatePeerAckedAsync(string txId, PeerSession session)
    {
        var doc = await _store.GetOrNullAsync(txId);
        if (doc is null || doc.State != OutgoingTxState.Dispatching) return;
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var attempt = doc.PeerAttempts.Find(a => a.PeerEndpoint == session.Remote.ToString());
        if (attempt is not null)
        {
            attempt.GetDataServedAtMs = nowMs;
        }
        else
        {
            doc.PeerAttempts.Add(new PeerAttempt
            {
                PeerEndpoint = session.Remote.ToString(),
                AnnouncedAtMs = nowMs,
                GetDataServedAtMs = nowMs,
            });
        }
        doc.State = OutgoingTxState.PeerAcked;
        await _store.SaveAsync(doc);
    }

    // --- Legacy per-session read loop, preserved for tests that
    // instantiate the coordinator without a dispatcher registry.

    private async Task WatchSessionLegacyAsync(PeerSession session, string txId, byte[] rawBytes, CancellationToken ct)
    {
        try
        {
            await foreach (var frame in session.IncomingMessages.ReadAllAsync(ct))
            {
                if (frame.Command == P2pCommands.GetData)
                {
                    var getdata = GetDataMessage.Parse(frame.Payload);
                    foreach (var item in getdata.Items)
                    {
                        if (item.Type != InvType.Tx) continue;
                        var requestedTxId = Convert.ToHexString(item.Hash).ToLowerInvariant();
                        if (!requestedTxId.Equals(txId, StringComparison.OrdinalIgnoreCase)) continue;

                        session.Telemetry.RecordGetDataRequested(InvType.Tx, item.Hash);
                        var requestedAt = DateTime.UtcNow;
                        await session.SendTxAsync(rawBytes, ct);
                        session.Telemetry.RecordGetDataServed(InvType.Tx, item.Hash, DateTime.UtcNow - requestedAt);
                        _logger.LogInformation("Served {TxId} to {Peer}", txId, session.Remote);

                        await UpdatePeerAckedAsync(txId, session);
                    }
                }
                else if (frame.Command == P2pCommands.Inv)
                {
                    var inv = InvMessage.Parse(frame.Payload);
                    foreach (var item in inv.Items)
                    {
                        if (item.Type != InvType.Tx) continue;
                        var seenTxId = Convert.ToHexString(item.Hash).ToLowerInvariant();
                        if (!_pendingTx.ContainsKey(seenTxId)) continue;

                        session.Telemetry.RecordRelayBackInv(item.Hash);

                        var count = _relayBackCount.AddOrUpdate(seenTxId, 1, (_, old) => old + 1);
                        _logger.LogDebug("Relay-back #{Count} for {TxId} from {Peer}", count, seenTxId, session.Remote);

                        if (count >= 2)
                        {
                            var doc = await _store.GetOrNullAsync(seenTxId, ct);
                            if (doc is not null && doc.State == OutgoingTxState.PeerAcked)
                            {
                                doc.State = OutgoingTxState.PeerRelayed;
                                await _store.SaveAsync(doc, ct);
                            }
                        }
                    }
                }
                else if (frame.Command == P2pCommands.Reject)
                {
                    var reject = RejectMessage.Parse(frame.Payload);
                    if (reject.Hash is not null)
                    {
                        var rejectedTxId = Convert.ToHexString(reject.Hash).ToLowerInvariant();
                        if (rejectedTxId.Equals(txId, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("Peer {Peer} rejected {TxId}: {Code} {Reason}",
                                session.Remote, txId, reject.Code, reject.Reason);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "WatchSessionAsync error for {TxId} on {Peer}", txId, session.Remote);
        }
    }
}
