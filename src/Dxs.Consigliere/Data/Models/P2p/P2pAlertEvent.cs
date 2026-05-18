#nullable enable
using System;
using System.Collections.Generic;

namespace Dxs.Consigliere.Data.Models.P2p;

/// <summary>
/// Wave 6 S2 — append-only Raven document fired by
/// <c>P2pAlertEvaluator</c> when an operator-actionable condition
/// holds. One document per fire; consumers (admin endpoint, future
/// notification sinks) read by id descending or by
/// <see cref="AlertUnixMs"/> window.
///
/// <para>Document IDs follow <c>p2p/alerts/{AlertUnixMs:D14}</c>
/// so a numeric-string ORDER BY hits the natural time order
/// (mirrors the W4 snapshot id format). Retention is enforced by
/// doc-id eviction in the poller, not Raven expiration.</para>
///
/// <para>Snapshots are never edited after creation (master.md
/// Core Rule §3 — append-only).</para>
/// </summary>
public sealed class P2pAlertEvent
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Unix milliseconds at fire time.</summary>
    public long AlertUnixMs { get; set; }

    public P2pAlertType Type { get; set; }

    /// <summary>Human-readable operator detail (threshold + observed
    /// value) baked at fire time so operators don't need to recompute
    /// it from <see cref="Context"/>.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Free-form rule-specific key/value bag (e.g.
    /// <c>"poolSize"="2"</c>, <c>"threshold"="5"</c>). Read-only at the
    /// data layer — the evaluator builds it once per fire.</summary>
    public Dictionary<string, string> Context { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public static string BuildId(long alertUnixMs) =>
        $"p2p/alerts/{alertUnixMs:D14}";
}

/// <summary>
/// Wave 6 S2 — the four rules from master.md §"Critical alert
/// poller". Frozen for the W6 contract — renames require a
/// contract-amendment slice (master.md handoff table).
/// </summary>
public enum P2pAlertType
{
    PoolSizeBelowThreshold = 1,
    RelayBackRateBelowThreshold = 2,
    ReorgDepthExceeded = 3,
    SourceFirstDropout = 4,
}
