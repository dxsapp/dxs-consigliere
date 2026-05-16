using System.Collections.Concurrent;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Pool;
using Dxs.Bsv.P2p.Session;

using Microsoft.Extensions.Options;

namespace BsvBroadcastNode;

/// <summary>
/// Core broadcast logic: validate → store → announce to peer pool → track relay-back.
/// No external dependencies — everything in-memory.
/// </summary>
public sealed class BroadcastService(
    TxStore txStore,
    PeerNodeHost peerHost,
    LogRing log,
    IOptions<BroadcastNodeOptions> opts,
    ILogger<BroadcastService> logger)
{
    private readonly BroadcastNodeOptions _opts = opts.Value;

    // txid → raw bytes for serving getdata
    private readonly ConcurrentDictionary<string, byte[]> _pending =
        new(StringComparer.OrdinalIgnoreCase);

    // txid → relay-back count
    private readonly ConcurrentDictionary<string, int> _relayBack =
        new(StringComparer.OrdinalIgnoreCase);

    public (TxRecord record, string? error) Submit(string rawHex)
    {
        // 1. Validate
        if (string.IsNullOrWhiteSpace(rawHex))
            return (null!, "Empty hex");

        var sizeBytes = rawHex.Length / 2;
        if (sizeBytes > _opts.MaxTxSizeBytes)
            return (null!, $"Tx size {sizeBytes:N0} bytes exceeds limit {_opts.MaxTxSizeBytes:N0}");

        byte[] rawBytes;
        try { rawBytes = Convert.FromHexString(rawHex); }
        catch { return (null!, "Invalid hex string"); }

        // 2. Compute txid — double-sha256 of raw bytes, reversed
        var txId = ComputeTxId(rawBytes);

        // 3. Idempotency
        var existing = txStore.Get(txId);
        if (existing is not null)
        {
            log.Add("info", $"Duplicate submit for {txId} — returning existing record", txId);
            return (existing, null);
        }

        // 4. Store
        var record = new TxRecord { TxId = txId, RawHex = rawHex };
        txStore.TryAdd(record);
        log.Add("info", $"Accepted {txId} ({sizeBytes:N0} bytes)", txId);

        // 5. Dispatch asynchronously
        _ = Task.Run(() => DispatchAsync(record, rawBytes));

        return (record, null);
    }

    private async Task DispatchAsync(TxRecord record, byte[] rawBytes)
    {
        _pending[record.TxId] = rawBytes;
        record.Transition(TxState.Dispatching);

        var manager = peerHost.Manager;
        var sessions = manager?.ActiveSessions.Values
            .Where(s => s.State == PeerSessionState.Ready)
            .ToList() ?? new List<PeerSession>();

        if (sessions.Count == 0)
        {
            record.Transition(TxState.Failed, "No ready peers at dispatch time");
            log.Add("warn", $"{record.TxId} failed — no ready peers", record.TxId);
            return;
        }

        var txid = Convert.FromHexString(record.TxId);
        var inv = InvMessage.ForTx(txid);
        var announced = 0;

        foreach (var session in sessions)
        {
            try
            {
                await session.SendInvAsync(inv, default);
                announced++;
                _ = WatchSessionAsync(session, record.TxId, rawBytes);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Announce to {Peer} failed", session.Remote);
            }
        }

        log.Add("info", $"Announced {record.TxId} to {announced}/{sessions.Count} peers", record.TxId);
    }

    private async Task WatchSessionAsync(PeerSession session, string txId, byte[] rawBytes)
    {
        try
        {
            await foreach (var frame in session.IncomingMessages.ReadAllAsync(default))
            {
                if (frame.Command == P2pCommands.GetData)
                {
                    var gd = GetDataMessage.Parse(frame.Payload);
                    foreach (var item in gd.Items)
                    {
                        if (item.Type != InvType.Tx) continue;
                        var id = Convert.ToHexString(item.Hash).ToLowerInvariant();
                        if (!id.Equals(txId, StringComparison.OrdinalIgnoreCase)) continue;

                        await session.SendTxAsync(rawBytes, default);
                        var record = txStore.Get(txId);
                        if (record is not null)
                        {
                            record.PeersServed.Add(session.Remote.ToString());
                            record.Transition(TxState.PeerAcked);
                        }
                        log.Add("info", $"{txId} → PeerAcked via {session.Remote}", txId);
                    }
                }
                else if (frame.Command == P2pCommands.Inv)
                {
                    var inv = InvMessage.Parse(frame.Payload);
                    foreach (var item in inv.Items)
                    {
                        if (item.Type != InvType.Tx) continue;
                        var id = Convert.ToHexString(item.Hash).ToLowerInvariant();
                        if (!_pending.ContainsKey(id)) continue;

                        var count = _relayBack.AddOrUpdate(id, 1, (_, old) => old + 1);
                        if (count >= 2)
                        {
                            var record = txStore.Get(id);
                            if (record is not null && record.State == TxState.PeerAcked)
                            {
                                record.RelayBackCount = count;
                                record.Transition(TxState.PeerRelayed);
                                log.Add("info", $"{id} → PeerRelayed ({count} peers)", id);
                            }
                        }
                    }
                }
                else if (frame.Command == P2pCommands.Reject)
                {
                    var rej = RejectMessage.Parse(frame.Payload);
                    if (rej.Hash is not null)
                    {
                        var id = Convert.ToHexString(rej.Hash).ToLowerInvariant();
                        log.Add("warn", $"Peer {session.Remote} rejected {id}: {rej.Code} {rej.Reason}", id);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "WatchSession error for {TxId}", txId);
        }
    }

    private static string ComputeTxId(byte[] rawTx)
    {
        // txid = SHA256(SHA256(raw)), reversed
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var h1 = sha256.ComputeHash(rawTx);
        var h2 = sha256.ComputeHash(h1);
        Array.Reverse(h2);
        return Convert.ToHexString(h2).ToLowerInvariant();
    }
}
