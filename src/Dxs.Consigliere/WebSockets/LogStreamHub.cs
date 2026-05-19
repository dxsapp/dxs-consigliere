using System.Collections.Concurrent;
using Dxs.Consigliere.Logging;
using Dxs.Consigliere.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dxs.Consigliere.WebSockets;

/// <summary>
/// wave-A3 S4 — admin-only live log tail. The hub captures the
/// caller's level + category filter on <see cref="SubscribeToLogs"/>,
/// flushes the in-process ring buffer to seed late joiners, and
/// pumps every subsequent emission until the connection drops
/// or <see cref="UnsubscribeFromLogs"/> is called.
///
/// Per the slice contract the subscription is NOT durable
/// across reconnects: a backend restart wipes the ring, and the
/// client receives a fresh seed on the next subscribe. That
/// matches the in-memory nature of the buffer.
/// </summary>
[Authorize(Policy = AdminAuthDefaults.Policy)]
public sealed class LogStreamHub(LogStreamBuffer buffer, ILogger<LogStreamHub> logger) : Hub
{
    public const string Route = "/ws/logs";
    public const string LogEventMethod = "OnLogEvent";

    private static readonly ConcurrentDictionary<string, IDisposable> Subscriptions = new();

    public async Task SubscribeToLogs(string minLevel = null, string categoryFilter = null)
    {
        var connectionId = Context.ConnectionId;
        var caller = Clients.Caller;
        var levelGate = NormalizeLevel(minLevel);
        var categorySubstring = string.IsNullOrWhiteSpace(categoryFilter) ? null : categoryFilter.Trim();

        // Flush the existing ring before subscribing live so the
        // client can render an instant non-empty list.
        foreach (var seed in buffer.Snapshot())
        {
            if (!Passes(seed, levelGate, categorySubstring)) continue;
            await caller.SendAsync(LogEventMethod, seed);
        }

        var subscription = buffer.Subscribe(entry =>
        {
            if (!Passes(entry, levelGate, categorySubstring)) return;
            // SignalR's SendAsync is fire-and-forget here on
            // purpose — a slow client cannot back-pressure the
            // publisher, dropped frames are acceptable on the
            // log-stream channel.
            _ = caller.SendAsync(LogEventMethod, entry);
        });

        if (Subscriptions.TryRemove(connectionId, out var existing))
            existing.Dispose();
        Subscriptions[connectionId] = subscription;
    }

    public Task UnsubscribeFromLogs()
    {
        if (Subscriptions.TryRemove(Context.ConnectionId, out var existing))
            existing.Dispose();
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        if (Subscriptions.TryRemove(Context.ConnectionId, out var existing))
            existing.Dispose();
        if (exception is not null)
            logger.LogDebug(exception, "[log-stream] connection {Conn} disconnected with error", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private static int NormalizeLevel(string raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "trace" => 0,
        "debug" => 1,
        "information" or "info" => 2,
        "warning" or "warn" => 3,
        "error" => 4,
        "critical" => 5,
        _ => 0,
    };

    private static bool Passes(LogEventDto entry, int levelGate, string categorySubstring)
    {
        if (NormalizeLevel(entry.Level) < levelGate) return false;
        if (categorySubstring is null) return true;
        return entry.Category?.Contains(categorySubstring, StringComparison.OrdinalIgnoreCase) == true;
    }
}
