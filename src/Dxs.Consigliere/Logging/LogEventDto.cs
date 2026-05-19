namespace Dxs.Consigliere.Logging;

/// <summary>
/// wave-A3 S4 — frozen wire shape for the SignalR log stream
/// payload. The hub emits these one-by-one as the framework
/// produces log events. The shape is documented here so a
/// future wave-A4 UI surface can deserialise it without
/// coupling to MEL internals.
///
/// Values flow through <see cref="LogSanitizer"/> before they
/// reach this DTO; the wire is the sanitized tier.
/// </summary>
public sealed class LogEventDto
{
    /// <summary>Monotonic-ish unix-ms timestamp at the moment of emit.</summary>
    public long UnixMs { get; init; }

    /// <summary>One of <c>trace</c>, <c>debug</c>, <c>information</c>, <c>warning</c>, <c>error</c>, <c>critical</c>.</summary>
    public string Level { get; init; }

    /// <summary>Source category — the typed <c>ILogger&lt;T&gt;</c> generic argument's full name.</summary>
    public string Category { get; init; }

    /// <summary>Render-once formatted message (sanitized).</summary>
    public string Message { get; init; }

    /// <summary>Exception's <c>ToString()</c> when present (sanitized); null otherwise.</summary>
    public string Exception { get; init; }
}
