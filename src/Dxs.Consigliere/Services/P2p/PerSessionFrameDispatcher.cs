#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Session;

using Microsoft.Extensions.Logging;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 2 S5.1 — single-consumer reader for one
/// <see cref="PeerSession"/>'s
/// <see cref="PeerSession.IncomingMessages"/> channel.
/// <see cref="System.Threading.Channels.ChannelReader{T}"/> is not a
/// broadcast primitive, so allowing multiple consumers to read the
/// same channel produces frame starvation. This dispatcher owns the
/// single reader and fans each frame out to all registered subscribers.
///
/// <para>
/// Subscriber failure isolation (audit W2 followup new-M1):
/// <list type="bullet">
/// <item>Every handler invocation runs inside a try/catch; thrown
///   exceptions are logged with the subscriber's stable tag and do
///   NOT terminate <see cref="RunAsync"/>.</item>
/// <item>A failed handler does not block later subscribers from
///   receiving the same frame — each handler is invoked
///   independently.</item>
/// <item>Fan-out is sequential: handlers for the same frame run
///   one after another. Slow handlers DO apply per-session
///   backpressure to other subscribers; W2 handlers are expected
///   to return quickly (schedule async work and return).</item>
/// </list>
/// </para>
/// </summary>
public sealed class PerSessionFrameDispatcher
{
    private readonly PeerSession _session;
    private readonly ILogger _logger;
    private readonly object _subsLock = new();
    private readonly Dictionary<string, List<Subscription>> _subsByCommand = new(StringComparer.Ordinal);

    public PerSessionFrameDispatcher(PeerSession session, ILogger logger)
    {
        _session = session;
        _logger = logger;
    }

    /// <summary>Endpoint of the underlying session. Useful for tests and logs.</summary>
    public PeerSession Session => _session;

    /// <summary>
    /// Register a handler for inbound frames whose command equals
    /// <paramref name="command"/>. Returns an
    /// <see cref="IDisposable"/> handle; disposing un-subscribes
    /// the handler. <paramref name="subscriberTag"/> is used in
    /// error logs and tests to attribute behaviour.
    /// </summary>
    public IDisposable Subscribe(string command, string subscriberTag, Func<InboundFrame, Task> handler)
    {
        if (string.IsNullOrEmpty(command)) throw new ArgumentException("command required", nameof(command));
        if (string.IsNullOrEmpty(subscriberTag)) throw new ArgumentException("subscriberTag required", nameof(subscriberTag));
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        var sub = new Subscription(command, subscriberTag, handler, this);
        lock (_subsLock)
        {
            if (!_subsByCommand.TryGetValue(command, out var list))
            {
                list = new List<Subscription>();
                _subsByCommand[command] = list;
            }
            list.Add(sub);
        }
        return sub;
    }

    /// <summary>
    /// Drive the read loop. Returns when the channel completes
    /// (session disconnected) or <paramref name="ct"/> is cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var frame in _session.IncomingMessages.ReadAllAsync(ct))
            {
                await DispatchAsync(frame);
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PerSessionFrameDispatcher reader loop ended for {Peer}", _session.Remote);
        }
    }

    private async Task DispatchAsync(InboundFrame frame)
    {
        // Snapshot the subscriber list under the lock so a concurrent
        // un-subscription doesn't mutate the iteration target.
        Subscription[] snapshot;
        lock (_subsLock)
        {
            if (!_subsByCommand.TryGetValue(frame.Command, out var list) || list.Count == 0)
                return;
            snapshot = list.ToArray();
        }

        foreach (var sub in snapshot)
        {
            try
            {
                await sub.Handler(frame);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "PerSessionFrameDispatcher: subscriber {Tag} threw on {Command} frame for {Peer}",
                    sub.Tag, frame.Command, _session.Remote);
            }
        }
    }

    private void Remove(Subscription sub)
    {
        lock (_subsLock)
        {
            if (_subsByCommand.TryGetValue(sub.Command, out var list))
            {
                list.Remove(sub);
                if (list.Count == 0) _subsByCommand.Remove(sub.Command);
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        public readonly string Command;
        public readonly string Tag;
        public readonly Func<InboundFrame, Task> Handler;
        private readonly PerSessionFrameDispatcher _owner;
        private int _disposed;

        public Subscription(string command, string tag, Func<InboundFrame, Task> handler, PerSessionFrameDispatcher owner)
        {
            Command = command;
            Tag = tag;
            Handler = handler;
            _owner = owner;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _owner.Remove(this);
        }
    }
}
