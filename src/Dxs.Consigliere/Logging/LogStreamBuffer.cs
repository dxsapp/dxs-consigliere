using System.Collections.Concurrent;

namespace Dxs.Consigliere.Logging;

/// <summary>
/// wave-A3 S4 — in-process ring buffer + fan-out for the live
/// log tail. Capacity is 1000 entries; on overflow the OLDEST
/// entry is evicted (drop-front-on-write, NOT block) so an
/// overwhelmed Serilog hot path cannot back-pressure callers.
///
/// Subscribers register an <see cref="Action{LogEventDto}"/>
/// callback via <see cref="Subscribe"/> and dispose the returned
/// handle when done. Newly-subscribed consumers can pull the
/// current ring contents via <see cref="Snapshot"/> — used by
/// the SignalR hub to seed late-joiners with whatever's already
/// in memory before the live stream starts.
/// </summary>
public sealed class LogStreamBuffer
{
    public const int Capacity = 1000;

    private readonly object _gate = new();
    private readonly Queue<LogEventDto> _ring = new(Capacity);
    private readonly ConcurrentDictionary<Guid, Action<LogEventDto>> _subscribers = new();

    public void Publish(LogEventDto entry)
    {
        if (entry is null) return;
        lock (_gate)
        {
            if (_ring.Count == Capacity) _ring.Dequeue();
            _ring.Enqueue(entry);
        }
        foreach (var (_, fn) in _subscribers)
        {
            try { fn(entry); }
            catch { /* a misbehaving subscriber MUST NOT take down the publisher loop */ }
        }
    }

    public LogEventDto[] Snapshot()
    {
        lock (_gate) return _ring.ToArray();
    }

    public IDisposable Subscribe(Action<LogEventDto> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var id = Guid.NewGuid();
        _subscribers[id] = handler;
        return new Subscription(this, id);
    }

    private sealed class Subscription(LogStreamBuffer owner, Guid id) : IDisposable
    {
        public void Dispose() => owner._subscribers.TryRemove(id, out _);
    }
}
