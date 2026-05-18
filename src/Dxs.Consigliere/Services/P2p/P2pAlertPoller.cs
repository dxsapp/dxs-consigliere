#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Chain;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.P2p;
using Dxs.Consigliere.Services.Metrics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 6 S2 — hosted service that periodically (default 60 s)
/// snapshots P2P + source-metrics state, runs <see cref="P2pAlertEvaluator"/>
/// against it, and writes any fired alerts to
/// <see cref="IAlertEventRepository"/>. Mirrors the W4
/// <c>SourceMetricsAggregator</c> hosted-poller template verbatim.
///
/// <para>Singleton by DI registration so the evaluator's previous-
/// tick state persists across ticks (Core Rule §4 — the in-process
/// delta state is not a new counter, just memoised arithmetic).</para>
///
/// <para>Retention: doc-id eviction past
/// <c>AlertConfig.AlertRetentionEvents</c>, mirroring the W4
/// snapshot retention pattern.</para>
/// </summary>
public sealed class P2pAlertPoller : IHostedService, IAsyncDisposable
{
    private readonly P2pAlertEvaluator _evaluator;
    private readonly IAlertEventRepository _repository;
    private readonly ISnapshotPersistence _snapshotPersistence;
    private readonly BsvP2pHealth _health;
    private readonly AlertConfig _config;
    private readonly ILogger<P2pAlertPoller> _logger;

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private readonly Func<DateTimeOffset, CancellationToken, Task<P2pAlertEvaluatorInput>>? _testInputBuilder;

    public P2pAlertPoller(
        P2pAlertEvaluator evaluator,
        IAlertEventRepository repository,
        ISnapshotPersistence snapshotPersistence,
        BsvP2pHealth health,
        IOptions<BsvP2pConfig> options,
        ILogger<P2pAlertPoller> logger)
        : this(evaluator, repository, snapshotPersistence, health, options, logger, testInputBuilder: null) { }

    /// <summary>
    /// Wave 6 S6 — test-only ctor seam. Lets fixture tests bypass
    /// the production input gather (which reads
    /// <see cref="BsvP2pHealth.ActiveSessions"/> + the snapshot
    /// store) by supplying the <see cref="P2pAlertEvaluatorInput"/>
    /// directly. Production DI uses the public ctor, which passes
    /// <c>null</c> and falls back to the live gather.
    /// </summary>
    internal P2pAlertPoller(
        P2pAlertEvaluator evaluator,
        IAlertEventRepository repository,
        ISnapshotPersistence snapshotPersistence,
        BsvP2pHealth health,
        IOptions<BsvP2pConfig> options,
        ILogger<P2pAlertPoller> logger,
        Func<DateTimeOffset, CancellationToken, Task<P2pAlertEvaluatorInput>>? testInputBuilder)
    {
        _evaluator = evaluator;
        _repository = repository;
        _snapshotPersistence = snapshotPersistence;
        _health = health;
        _config = options.Value.Alert;
        _logger = logger;
        _testInputBuilder = testInputBuilder;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("P2pAlertPoller disabled (config).");
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            try { _cts.Cancel(); } catch { }
        }
        if (_loop is not null)
        {
            try { await _loop.WaitAsync(cancellationToken); } catch { }
        }
    }

    /// <summary>Test seam — drive one tick deterministically without
    /// waiting for the interval to elapse. The optional
    /// <paramref name="nowOverride"/> lets fixture tests pin the
    /// evaluator's <c>Now</c> (and therefore document ids) at a
    /// deterministic timestamp.</summary>
    public async Task<int> TickOnceAsync(
        CancellationToken cancellationToken,
        DateTimeOffset? nowOverride = null)
    {
        var now = nowOverride ?? DateTimeOffset.UtcNow;
        P2pAlertEvaluatorInput input;
        if (_testInputBuilder is not null)
        {
            input = await _testInputBuilder(now, cancellationToken);
        }
        else
        {
            var peerTelemetry = SnapshotPeerTelemetry();
            var windowSnapshots = await LoadWindowSnapshotsAsync(now, cancellationToken);
            input = new P2pAlertEvaluatorInput(
                PoolSize: _health.PoolSize,
                LastDegradedReorgAt: _health.LastDegradedReorgAt,
                PeerTelemetry: peerTelemetry,
                WindowSnapshots: windowSnapshots,
                Config: _config,
                Now: now);
        }

        var events = _evaluator.Evaluate(input);
        foreach (var ev in events)
        {
            await _repository.SaveAsync(ev, cancellationToken);
            _logger.LogWarning("P2P alert fired: {Type} — {Detail}", ev.Type, ev.Detail);
        }

        await EvictExcessAlertsAsync(cancellationToken);
        return events.Count;
    }

    private IReadOnlyDictionary<string, PeerTelemetry> SnapshotPeerTelemetry()
    {
        // Each ActiveSession exposes the same PeerTelemetry shape
        // W4 reads for scoring; the dictionary key is the session's
        // remote endpoint string (mirrors PeerManager._active key).
        var dict = new Dictionary<string, PeerTelemetry>(StringComparer.Ordinal);
        foreach (var session in _health.ActiveSessions)
        {
            try
            {
                dict[session.Remote.ToString()] = session.Telemetry.Snapshot();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Telemetry snapshot failed for peer {Peer}", session.Remote);
            }
        }
        return dict;
    }

    private async Task<IReadOnlyList<Data.Models.Metrics.SourceMetricsSnapshot>> LoadWindowSnapshotsAsync(
        DateTimeOffset now,
        CancellationToken ct)
    {
        var toMs = now.ToUnixTimeMilliseconds();
        var fromMs = toMs - _config.SourceFirstDropoutWindowMs;
        try
        {
            return await _snapshotPersistence.GetSnapshotsInWindowAsync(fromMs, toMs, ct);
        }
        catch (Exception ex)
        {
            // A snapshot-store fault should not stop the poller from
            // evaluating the OTHER three rules. We log and feed the
            // evaluator an empty list — SourceFirstDropout silently
            // no-ops (< 2 snapshots → yield break).
            _logger.LogWarning(ex, "Snapshot persistence read failed; SourceFirstDropout rule will no-op this tick");
            return System.Array.Empty<Data.Models.Metrics.SourceMetricsSnapshot>();
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(1_000, _config.AlertPollIntervalMs));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await TickOnceAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "P2pAlertPoller tick failed; will retry next interval");
            }

            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task EvictExcessAlertsAsync(CancellationToken ct)
    {
        if (_config.AlertRetentionEvents <= 0) return;

        var allIds = await _repository.GetAllIdsOrderedAsync(ct);
        var excess = allIds.Count - _config.AlertRetentionEvents;
        if (excess <= 0) return;

        var toDelete = new List<string>(excess);
        for (var i = 0; i < excess; i++) toDelete.Add(allIds[i]);
        await _repository.DeleteAsync(toDelete, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _cts?.Dispose();
    }
}
