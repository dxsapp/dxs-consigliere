#nullable enable
using System.Collections.Generic;

namespace Dxs.Bsv.P2p.Pool;

/// <summary>
/// Wave 6 S1 — pure-logic planner that decides which active peer (if
/// any) <see cref="PeerManager"/> should evict on the current
/// maintenance tick.
///
/// <para>The planner consumes only the scored snapshot of currently
/// active peers + the rotation policy; it touches no sockets, no
/// telemetry sinks, no Raven store. This is the W6 test seam for the
/// "rotation evicts low-scoring peer in fixture" done-when (A1
/// pass-2 M1 fix).</para>
///
/// <para>Single-peer-per-tick by construction (Core Rule §5): the
/// returned <see cref="RotationDecision.EvictKeys"/> contains at most
/// one key. Bounded eviction prevents flap loops where a tick evicts
/// half the pool then waits for replenishment.</para>
/// </summary>
public sealed class PeerRotationPlanner
{
    /// <summary>
    /// Compute the rotation decision for one tick.
    /// </summary>
    /// <param name="active">Currently active peers with their freshly-
    /// computed <see cref="PeerScore"/>. Order does not matter; ties
    /// are broken by insertion order.</param>
    /// <param name="policy">Rotation thresholds + pool sizing.</param>
    /// <returns>A <see cref="RotationDecision"/> whose
    /// <see cref="RotationDecision.EvictKeys"/> is empty (no-op) or a
    /// single-key list.</returns>
    public RotationDecision Plan(IReadOnlyList<ScoredPeer> active, PeerRotationPolicy policy)
    {
        if (active is null || active.Count == 0)
            return RotationDecision.NoOp;

        ScoredPeer? lowest = null;
        for (var i = 0; i < active.Count; i++)
        {
            var candidate = active[i];
            if (lowest is null || candidate.Score.Value < lowest.Value.Score.Value)
                lowest = candidate;
        }

        // The lowest-scoring peer is only evicted when it falls below
        // the retain floor. This satisfies both behaviours documented
        // in master.md:
        //   - "proactively evict peers below MinimumScoreToRetain" —
        //     direct match;
        //   - "rotation evicts the lowest-scoring active peer ...
        //     instead of an arbitrary one" — when the floor catches
        //     a peer, that peer is the one that gets the slot.
        // Crucially, we do NOT churn a pool whose worst peer is still
        // healthy (≥ floor) — that would drive flap loops.
        if (lowest is { } pick && pick.Score.Value < policy.MinimumScoreToRetain)
            return new RotationDecision(new[] { pick.Key });

        return RotationDecision.NoOp;
    }
}

/// <summary>
/// Wave 6 S1 — input row for <see cref="PeerRotationPlanner.Plan"/>:
/// an active peer's stable key (host:port) + the score computed for it
/// THIS tick via the configured <see cref="IPeerScoringPolicy"/>.
/// </summary>
public readonly record struct ScoredPeer(string Key, PeerScore Score);

/// <summary>
/// Wave 6 S1 — thresholds + sizing the planner uses. Default values
/// match <c>BsvP2pConfig.Alert.MinimumScoreToRetain</c> at the
/// consigliere layer; the planner itself is host-agnostic.
/// </summary>
public sealed record PeerRotationPolicy
{
    /// <summary>
    /// Master.md §"Per-peer scoring + rotation": peers below this
    /// floor are evicted at most one-per-tick. Default 30 out of 100.
    /// </summary>
    public int MinimumScoreToRetain { get; init; } = 30;
}

/// <summary>
/// Wave 6 S1 — output of the planner. <see cref="EvictKeys"/> contains
/// at most one key (Core Rule §5).
/// </summary>
public sealed record RotationDecision(IReadOnlyList<string> EvictKeys)
{
    /// <summary>Sentinel for "no eviction this tick".</summary>
    public static readonly RotationDecision NoOp = new(System.Array.Empty<string>());
}
