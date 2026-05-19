using Microsoft.Extensions.Logging;

namespace Dxs.Consigliere.Logging;

/// <summary>
/// wave-A3 S4 — `Microsoft.Extensions.Logging` provider that
/// hands every framework log emission to <see cref="LogStreamBuffer"/>.
/// The project doesn't currently wire `UseSerilog(...)` into
/// the host (Serilog stays a static bootstrap logger), so a
/// MEL provider captures the broadest surface — every
/// `ILogger&lt;T&gt;` injected into a controller, background
/// task, etc. — without depending on a Serilog hook.
///
/// Sanitization runs at emit time so the buffer holds only
/// already-clean payloads.
/// </summary>
public sealed class LogStreamProvider(LogStreamBuffer buffer) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new LogStreamLogger(buffer, categoryName);

    public void Dispose() { /* buffer is DI-owned */ }

    private sealed class LogStreamLogger(LogStreamBuffer buffer, string category) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var message = formatter is null ? state?.ToString() ?? string.Empty : formatter(state, exception);
            buffer.Publish(new LogEventDto
            {
                UnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Level = logLevel.ToString().ToLowerInvariant(),
                Category = category,
                Message = LogSanitizer.Apply(message),
                Exception = exception is null ? null : LogSanitizer.Apply(exception.ToString()),
            });
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
