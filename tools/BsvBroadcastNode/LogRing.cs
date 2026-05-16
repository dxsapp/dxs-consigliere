using System.Collections.Concurrent;

namespace BsvBroadcastNode;

/// <summary>Fixed-size in-memory ring buffer for the GET /log endpoint.</summary>
public sealed class LogRing
{
    private readonly ConcurrentQueue<LogEntry> _q = new();
    private readonly int _max;
    private int _count;

    public LogRing(int max = 500) => _max = max;

    public void Add(string level, string message, string? txId = null)
    {
        _q.Enqueue(new LogEntry(DateTimeOffset.UtcNow, level, message, txId));
        if (System.Threading.Interlocked.Increment(ref _count) > _max && _q.TryDequeue(out _))
            System.Threading.Interlocked.Decrement(ref _count);
    }

    public IEnumerable<LogEntry> Recent(int n = 100) => _q.TakeLast(n);

    public sealed record LogEntry(DateTimeOffset At, string Level, string Message, string? TxId);
}
