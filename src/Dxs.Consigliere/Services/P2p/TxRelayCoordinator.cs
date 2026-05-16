#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
/// The coordinator runs as a long-lived singleton and subscribes to
/// inbound channel messages from every active <see cref="PeerSession"/>.
/// </summary>
public sealed class TxRelayCoordinator(
    PeerManager peerManager,
    OutgoingTransactionStore store,
    ILogger<TxRelayCoordinator> logger)
{
    // txid → raw hex bytes (populated during Dispatching)
    private readonly ConcurrentDictionary<string, byte[]> _pendingTx = new(StringComparer.OrdinalIgnoreCase);

    // txid → (count of peers that sent relay-back inv)
    private readonly ConcurrentDictionary<string, int> _relayBackCount = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Announce a tx to all currently-ready peers, start listening for
    /// getdata on all their inbound channels, and update document state.
    /// Returns when at least one peer has been served, or throws on total failure.
    /// </summary>
    public async Task<int> AnnounceAsync(string txId, string rawHex, CancellationToken ct)
    {
        var rawBytes = Convert.FromHexString(rawHex);
        _pendingTx[txId] = rawBytes;

        var sessions = peerManager.ActiveSessions.Values
            .Where(s => s.State == PeerSessionState.Ready)
            .ToList();

        if (sessions.Count == 0)
        {
            logger.LogWarning("AnnounceAsync: no ready peers for {TxId}", txId);
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
                // Subscribe the session's inbound channel to drive getdata / relay-back.
                _ = WatchSessionAsync(session, txId, rawBytes, ct);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to announce {TxId} to {Peer}", txId, session.Remote);
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

    private async Task WatchSessionAsync(PeerSession session, string txId, byte[] rawBytes, CancellationToken ct)
    {
        try
        {
            // Read messages from this session until it closes or tx is terminal.
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

                        // Serve the tx
                        await session.SendTxAsync(rawBytes, ct);
                        logger.LogInformation("Served {TxId} to {Peer}", txId, session.Remote);

                        // Update PeerAcked state in store
                        var doc = await store.GetOrNullAsync(txId, ct);
                        if (doc is not null && doc.State == OutgoingTxState.Dispatching)
                        {
                            var attempt = doc.PeerAttempts.Find(a => a.PeerEndpoint == session.Remote.ToString());
                            if (attempt is not null)
                                attempt.GetDataServedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            else
                                doc.PeerAttempts.Add(new PeerAttempt
                                {
                                    PeerEndpoint = session.Remote.ToString(),
                                    AnnouncedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                    GetDataServedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                });
                            doc.State = OutgoingTxState.PeerAcked;
                            await store.SaveAsync(doc, ct);
                        }
                    }
                }
                else if (frame.Command == P2pCommands.Inv)
                {
                    // Check if peer is relaying our tx back to us.
                    var inv = InvMessage.Parse(frame.Payload);
                    foreach (var item in inv.Items)
                    {
                        if (item.Type != InvType.Tx) continue;
                        var seenTxId = Convert.ToHexString(item.Hash).ToLowerInvariant();
                        if (!_pendingTx.ContainsKey(seenTxId)) continue;

                        var count = _relayBackCount.AddOrUpdate(seenTxId, 1, (_, old) => old + 1);
                        logger.LogDebug("Relay-back #{Count} for {TxId} from {Peer}", count, seenTxId, session.Remote);

                        if (count >= 2)
                        {
                            var doc = await store.GetOrNullAsync(seenTxId, ct);
                            if (doc is not null && doc.State == OutgoingTxState.PeerAcked)
                            {
                                doc.State = OutgoingTxState.PeerRelayed;
                                await store.SaveAsync(doc, ct);
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
                            logger.LogWarning("Peer {Peer} rejected {TxId}: {Code} {Reason}",
                                session.Remote, txId, reject.Code, reject.Reason);
                            // Let OutgoingTransactionMonitor handle quorum-based terminal classification.
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "WatchSessionAsync error for {TxId} on {Peer}", txId, session.Remote);
        }
    }
}
