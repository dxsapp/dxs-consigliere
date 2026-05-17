#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Metrics;
using Dxs.Consigliere.Extensions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Services.Metrics;

/// <summary>
/// Wave 4 S4 — hosted service that periodically invokes
/// <see cref="ISourceMetricsCollector.Collect"/>, persists the
/// snapshot to Raven, and evicts old snapshots beyond
/// <see cref="SourceMetricsConfig.SnapshotRetentionCount"/>.
///
/// <para>Singleton by DI registration so retention eviction is
/// race-free across the snapshot loop. Eviction reads the current
/// snapshot count and deletes the oldest excess via lexicographic
/// id ordering (see <see cref="SourceMetricsBuckets.BuildId"/> for
/// the padded format that makes string ordering equal numeric
/// ordering).</para>
///
/// <para>Master §Core Rule §6: snapshots are eventually-consistent
/// across counters (each <c>Interlocked.Read</c> is atomic but the
/// read set is not transactional). At 30 s sampling intervals the
/// inconsistency window is well below operational resolution.</para>
/// </summary>
public sealed class SourceMetricsAggregator : IHostedService, IAsyncDisposable
{
    private readonly ISourceMetricsCollector _collector;
    private readonly SourceVisibilityTracker _visibilityTracker;
    private readonly IDocumentStore _documentStore;
    private readonly SourceMetricsConfig _config;
    private readonly ILogger<SourceMetricsAggregator> _logger;

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public SourceMetricsAggregator(
        ISourceMetricsCollector collector,
        SourceVisibilityTracker visibilityTracker,
        IDocumentStore documentStore,
        IOptions<SourceMetricsConfig> config,
        ILogger<SourceMetricsAggregator> logger)
    {
        _collector = collector;
        _visibilityTracker = visibilityTracker;
        _documentStore = documentStore;
        _config = config.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("SourceMetricsAggregator disabled (config).");
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

    /// <summary>Single-tick entrypoint exposed for the S6 fixture
    /// suite: drive the snapshot loop deterministically without
    /// waiting for the configured interval to elapse.</summary>
    public async Task<SourceMetricsSnapshot> TickOnceAsync(CancellationToken cancellationToken)
    {
        _visibilityTracker.EvictStaleEntries(DateTimeOffset.UtcNow);
        var snapshot = _collector.Collect();
        await PersistAsync(snapshot, cancellationToken);
        await EvictExcessSnapshotsAsync(cancellationToken);
        return snapshot;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(1_000, _config.SnapshotIntervalMs));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await TickOnceAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SourceMetricsAggregator tick failed; will retry next interval");
            }

            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PersistAsync(SourceMetricsSnapshot snapshot, CancellationToken ct)
    {
        using var session = _documentStore.OpenAsyncSession();
        await session.StoreAsync(snapshot, snapshot.Id, ct);
        await session.SaveChangesAsync(ct);
    }

    private async Task EvictExcessSnapshotsAsync(CancellationToken ct)
    {
        if (_config.SnapshotRetentionCount <= 0) return;

        using var session = _documentStore.OpenAsyncSession();
        // Take ascending order (oldest first) past the retention
        // count; delete them. The id format
        // (SourceMetricsBuckets.BuildId) is zero-padded D14 so
        // lex-order equals numeric-order on the snapshot timestamp.
        var allIds = await session
            .Query<SourceMetricsSnapshot>()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(token: ct);

        var excess = allIds.Count - _config.SnapshotRetentionCount;
        if (excess <= 0) return;

        for (var i = 0; i < excess; i++)
            session.Delete(allIds[i]);

        await session.SaveChangesAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _cts?.Dispose();
    }
}
