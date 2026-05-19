namespace Dxs.Consigliere.Configs;

/// <summary>
/// wave-A3 S1 — bind shape for the three named rate-limit
/// policies. Defaults err conservative (per the wave-A3
/// launch-prompt: 5 login/min, 100 me/min, 1 broadcast/sec).
/// Operators can loosen any limit through configuration
/// (env vars: `RateLimiting__Login__PermitsPerMinute=...`).
///
/// Counters live in-process — multi-instance HA is wave-A4
/// territory.
/// </summary>
public sealed class RateLimitingConfig
{
    public RateLimitPolicyConfig Login { get; init; } = new() { PermitsPerMinute = 5 };
    public RateLimitPolicyConfig Me { get; init; } = new() { PermitsPerMinute = 100 };
    public RateLimitPolicyConfig Broadcast { get; init; } = new() { PermitsPerSecond = 1 };
}

public sealed class RateLimitPolicyConfig
{
    /// <summary>
    /// Fixed-window rate, expressed as a per-minute permit count.
    /// Mutually exclusive with <see cref="PermitsPerSecond"/>; if
    /// both are set the per-second value wins.
    /// </summary>
    public int PermitsPerMinute { get; init; }

    /// <summary>
    /// Fixed-window rate expressed as a per-second permit count
    /// — used for the broadcast policy where the threat model
    /// is a runaway client, not brute force.
    /// </summary>
    public int PermitsPerSecond { get; init; }

    /// <summary>
    /// Burst tolerance. When the window resets every request in
    /// the queue is admitted in order. Default 0 = strict
    /// rejection on overflow.
    /// </summary>
    public int QueueLimit { get; init; }
}
