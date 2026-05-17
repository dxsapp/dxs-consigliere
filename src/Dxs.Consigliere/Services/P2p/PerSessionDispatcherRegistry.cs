#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Session;

using Microsoft.Extensions.Logging;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 2 S5.1 — owns one <see cref="PerSessionFrameDispatcher"/>
/// per <see cref="PeerSession"/>, spins up its
/// <see cref="PerSessionFrameDispatcher.RunAsync"/> task on demand,
/// and tears down on session completion. Singleton — bind in DI.
/// </summary>
public sealed class PerSessionDispatcherRegistry : IAsyncDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PerSessionDispatcherRegistry> _logger;
    private readonly ConcurrentDictionary<PeerSession, Entry> _entries = new(ReferenceEqualityComparer.Instance);
    private readonly CancellationTokenSource _cts = new();
    private int _disposed;

    public PerSessionDispatcherRegistry(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PerSessionDispatcherRegistry>();
    }

    /// <summary>
    /// Get-or-create the dispatcher for <paramref name="session"/>.
    /// Idempotent — repeated calls return the same dispatcher. The
    /// reader loop starts on first creation; it ends when the
    /// session's channel completes (disconnect) or registry shutdown.
    /// </summary>
    public PerSessionFrameDispatcher For(PeerSession session)
    {
        if (session is null) throw new ArgumentNullException(nameof(session));

        if (_entries.TryGetValue(session, out var existing)) return existing.Dispatcher;

        var dispatcher = new PerSessionFrameDispatcher(
            session, _loggerFactory.CreateLogger<PerSessionFrameDispatcher>());
        var entry = new Entry(dispatcher);
        if (!_entries.TryAdd(session, entry))
        {
            // race — someone else created one between TryGetValue and TryAdd
            return _entries[session].Dispatcher;
        }

        entry.RunTask = Task.Run(async () =>
        {
            try
            {
                await dispatcher.RunAsync(_cts.Token);
            }
            finally
            {
                _entries.TryRemove(session, out _);
            }
        }, CancellationToken.None);

        return dispatcher;
    }

    /// <summary>Number of live dispatchers. Diagnostic only.</summary>
    public int Count => _entries.Count;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _cts.Cancel(); } catch { }
        var tasks = new List<Task>();
        foreach (var (_, entry) in _entries)
            if (entry.RunTask is not null) tasks.Add(entry.RunTask);
        try { await Task.WhenAll(tasks); } catch { /* best-effort */ }
        _cts.Dispose();
    }

    private sealed class Entry
    {
        public Entry(PerSessionFrameDispatcher dispatcher) => Dispatcher = dispatcher;
        public PerSessionFrameDispatcher Dispatcher { get; }
        public Task? RunTask { get; set; }
    }
}
