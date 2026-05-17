#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Observer;
using Dxs.Bsv.P2p.Session;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 2 S5.3 hosted service. Drives the BSV mempool observer:
///
/// <list type="number">
/// <item>Waits for <see cref="RavenWatchlistLoader.IsLoaded"/> so the
///   matcher is warm before any tx flow.</item>
/// <item>Reconciles every <c>ReconcileIntervalSeconds</c>: attaches
///   the frozen-S0 <see cref="PeerSession.OnInvReceived"/> callback
///   to every Ready peer that isn't already wired. Callbacks are
///   additive (Wave 1 S0 invariant) so this doesn't conflict with
///   <c>IncomingMessages</c> consumers.</item>
/// <item>On each <c>inv(MSG_TX)</c> item:
///   <list type="bullet">
///   <item>Record the inv via <see cref="SourceObservationRecorder"/>.</item>
///   <item>Ask <see cref="MempoolWatcher.DecideFetch"/> whether to issue getdata.</item>
///   <item>On <see cref="MempoolWatcher.FetchDecision.Fetch"/>:
///     subscribe a **one-shot** tx-frame handler via the
///     <see cref="PerSessionDispatcherRegistry"/> for the requested
///     txid, then call
///     <see cref="PeerSession.SendGetDataAsync"/>. The handler
///     parses + matches + persists, then unsubscribes. A timer
///     unsubscribes the handler if the tx never arrives.</item>
///   </list>
/// </item>
/// </list>
///
/// Uses <c>session.OnInvReceived</c> (Wave 1 S0 callback) for inv
/// handling — that path is additive and safe alongside the
/// dispatcher. Uses the dispatcher for tx-frame await so it doesn't
/// race the <see cref="TxRelayCoordinator"/>'s <c>getdata</c> /
/// relay-back handlers on the same channel.
/// </summary>
public sealed class P2pMempoolIngestRunner : IHostedService, IAsyncDisposable
{
    private readonly BsvP2pHealth _health;
    private readonly RavenWatchlistLoader _loader;
    private readonly WatchlistMatcher _matcher;
    private readonly MempoolWatcher _watcher;
    private readonly SourceObservationRecorder _recorder;
    private readonly TxObservationJournalWriter _journal;
    private readonly IRawTransactionPayloadStore _payloadStore;
    private readonly PerSessionDispatcherRegistry _dispatcherRegistry;
    private readonly MempoolWatcherOptions _watcherOptions;
    private readonly BsvP2pConfig _p2pConfig;
    private readonly INetworkProvider _network;
    private readonly ILogger<P2pMempoolIngestRunner> _logger;

    private readonly ConcurrentDictionary<PeerSession, byte> _wiredSessions = new(ReferenceEqualityComparer.Instance);
    private readonly TimeSpan _reconcileInterval = TimeSpan.FromSeconds(5);
    private CancellationTokenSource? _cts;
    private Task? _reconcileLoop;
    private int _disposed;

    public P2pMempoolIngestRunner(
        BsvP2pHealth health,
        RavenWatchlistLoader loader,
        WatchlistMatcher matcher,
        MempoolWatcher watcher,
        SourceObservationRecorder recorder,
        TxObservationJournalWriter journal,
        IRawTransactionPayloadStore payloadStore,
        PerSessionDispatcherRegistry dispatcherRegistry,
        IOptions<MempoolWatcherOptions> watcherOptions,
        IOptions<BsvP2pConfig> p2pOptions,
        INetworkProvider network,
        ILogger<P2pMempoolIngestRunner> logger)
    {
        _health = health;
        _loader = loader;
        _matcher = matcher;
        _watcher = watcher;
        _recorder = recorder;
        _journal = journal;
        _payloadStore = payloadStore;
        _dispatcherRegistry = dispatcherRegistry;
        _watcherOptions = watcherOptions.Value;
        _p2pConfig = p2pOptions.Value;
        _network = network;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_p2pConfig.Enabled)
        {
            _logger.LogInformation("P2pMempoolIngestRunner disabled (P2P off).");
            return;
        }

        // Initialise the watchlist on the way up — the runner is the
        // production owner of the loader's lifecycle.
        await _loader.InitializeAsync(cancellationToken);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _reconcileLoop = Task.Run(() => ReconcileLoopAsync(_cts.Token), CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            try { _cts.Cancel(); } catch { }
        }
        if (_reconcileLoop is not null)
        {
            try { await _reconcileLoop.WaitAsync(cancellationToken); } catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await StopAsync(CancellationToken.None);
        _cts?.Dispose();
    }

    private async Task ReconcileLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { Reconcile(); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "P2pMempoolIngestRunner reconcile tick failed");
            }
            try { await Task.Delay(_reconcileInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Test seam mirroring <see cref="HeadersChainService.Reconcile()"/>
    /// — production callers use the no-arg overload.
    /// </summary>
    public void Reconcile()
    {
        if (!_health.Bound) return;
        Reconcile(_health.ActiveSessions);
    }

    /// <summary>
    /// Test seam: same logic against an explicit session collection.
    /// </summary>
    public void Reconcile(IReadOnlyCollection<PeerSession> sessions)
    {
        foreach (var session in sessions)
        {
            if (session.State != PeerSessionState.Ready) continue;
            if (!_wiredSessions.TryAdd(session, 0)) continue;
            AttachInvCallback(session);
            _logger.LogDebug("P2pMempoolIngestRunner wired inv callback for {Peer}", session.Remote);
        }
    }

    private void AttachInvCallback(PeerSession session)
    {
        var prev = session.OnInvReceived;
        Action<InvMessage> ours = inv => _ = HandleInvAsync(session, inv);
        // Additive — preserve any previously-set callback (Wave 1 S0
        // invariant; HeadersChainService also installs one here).
        session.OnInvReceived = prev is null ? ours : prev + ours;
    }

    private async Task HandleInvAsync(PeerSession session, InvMessage inv)
    {
        if (!_matcher.IsLoaded) return; // matcher not warm yet — drop quietly

        foreach (var item in inv.Items)
        {
            if (item.Type != InvType.Tx) continue;

            _recorder.RecordInvObserved(TxObservationSource.P2p);

            var txid = Convert.ToHexString(item.Hash).ToLowerInvariant();
            var decision = _watcher.DecideFetch(txid);
            switch (decision)
            {
                case MempoolWatcher.FetchDecision.Duplicate:
                    continue;
                case MempoolWatcher.FetchDecision.RateLimited:
                    _recorder.RecordRateLimited();
                    continue;
                case MempoolWatcher.FetchDecision.Fetch:
                    try
                    {
                        await IssueGetDataAsync(session, txid, item.Hash);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "IssueGetData failed for {Txid} on {Peer}", txid, session.Remote);
                        _watcher.Forget(txid);
                    }
                    break;
            }
        }
    }

    private async Task IssueGetDataAsync(PeerSession session, string txid, byte[] txidBytes)
    {
        var dispatcher = _dispatcherRegistry.For(session);

        // One-shot tx-frame subscription, txid-scoped.
        var fired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable? sub = null;
        sub = dispatcher.Subscribe(P2pCommands.Tx, $"mempool.tx.{txid[..8]}", async frame =>
        {
            // Each tx frame is the FULL parsed tx bytes; recompute its
            // double-SHA hash to confirm it's the one we asked for.
            // Cheap; happens once per matching frame.
            byte[] gotTxid;
            try { gotTxid = Hash.Sha256Sha256(frame.Payload); }
            catch { return; }
            if (!gotTxid.AsSpan().SequenceEqual(txidBytes)) return;

            try
            {
                await OnTxArrivedAsync(txid, frame.Payload);
            }
            finally
            {
                sub?.Dispose();
                fired.TrySetResult(true);
            }
        });

        // Issue getdata.
        var getData = new GetDataMessage(new[] { new InvVector(InvType.Tx, txidBytes) });
        await session.SendGetDataAsync(getData, CancellationToken.None);

        // Schedule a timeout to unsubscribe if the tx never arrives.
        _ = Task.Delay(_watcherOptions.GetDataTimeoutMs).ContinueWith(_ =>
        {
            if (fired.Task.IsCompleted) return;
            sub?.Dispose();
            // Audit W2 M4-followup classification: distinguish silent
            // timeout from oversize-disconnect by checking session
            // completion + negotiated max payload.
            if (session.Completion.IsCompletedSuccessfully
                && session.Completion.Result == DisconnectReason.ProtocolViolation
                && session.PeerMaxRecvPayloadLength >= _p2pConfig.MempoolMaxFetchedTxBytes)
            {
                _recorder.RecordOversizePayload();
            }
            else
            {
                _recorder.RecordGetDataTimeout();
            }
            _watcher.Forget(txid);
        }, TaskScheduler.Default);
    }

    private async Task OnTxArrivedAsync(string txid, byte[] rawBytes)
    {
        // Parse defensively — Transaction.Parse can throw on malformed.
        Transaction? tx;
        try { tx = Transaction.Parse(rawBytes, _network.Network); }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OnTxArrived: failed to parse {Txid}", txid);
            _recorder.RecordParseError();
            return;
        }
        if (tx is null) { _recorder.RecordParseError(); return; }

        var parsed = ToParsedTx(tx, txid);
        var result = _matcher.Match(parsed);

        if (result is MatchResult.None)
        {
            _recorder.RecordUnmatched();
            return;
        }

        // Hit — persist raw + append journal entry.
        RawTransactionPayloadReference? payloadRef = null;
        try
        {
            payloadRef = await _payloadStore.SaveAsync(
                txid,
                Convert.ToHexString(rawBytes).ToLowerInvariant());
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OnTxArrived: payload save failed for {Txid}", txid);
        }

        var observation = new TxObservation(
            TxObservationEventType.SeenInMempool,
            TxObservationSource.P2p,
            txid,
            DateTimeOffset.UtcNow);
        var ok = await _journal.AppendAsync(observation, payloadRef, TxObservationSource.P2p);
        if (ok) _recorder.RecordMatched();
    }

    private static ParsedTx ToParsedTx(Transaction tx, string txid)
    {
        var outputHashes = new List<byte[]>(tx.Outputs.Count);
        var outputTokens = new List<string>();
        for (var i = 0; i < tx.Outputs.Count; i++)
        {
            var o = tx.Outputs[i];
            if (o.Address?.Hash160 is { Length: 20 } h)
                outputHashes.Add(h);
            if (!string.IsNullOrEmpty(o.TokenId))
                outputTokens.Add(o.TokenId);
        }
        var inputHashes = new List<byte[]>(tx.Inputs.Count);
        for (var i = 0; i < tx.Inputs.Count; i++)
        {
            var inp = tx.Inputs[i];
            if (inp.Address?.Hash160 is { Length: 20 } h)
                inputHashes.Add(h);
        }
        return new ParsedTx(txid, outputHashes, inputHashes, outputTokens);
    }
}
