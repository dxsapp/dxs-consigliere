#nullable enable
using System;

using Dxs.Bsv.P2p.Chain;

namespace Dxs.Bsv.P2p.Pool;

/// <summary>
/// Wave 6 S0 — default <see cref="IPeerScoringPolicy"/> with the fixed
/// weights documented in <c>docs/stream-tasks/production-ops-wave/master.md</c>
/// (A1-followup M2). The base score is 100; penalties subtract,
/// bonuses add, the result is clamped to <c>[0, 100]</c> by
/// <see cref="PeerScore"/>'s constructor.
///
/// <list type="table">
///   <listheader><term>Component</term><description>Formula + cap</description></listheader>
///   <item>
///     <term><c>latency_penalty</c></term>
///     <description><c>PingRttP95Ms / 10.0</c>, clamped to
///     <c>[0, 60]</c> — 600 ms p95 caps the penalty.</description>
///   </item>
///   <item>
///     <term><c>reject_penalty</c></term>
///     <description><c>5 * sum(RejectByClass)</c>, clamped to
///     <c>[0, 50]</c> — 10 rejects cap the penalty.</description>
///   </item>
///   <item>
///     <term><c>relay_back_bonus</c></term>
///     <description><c>min(RelayBackInvCount, 50)</c> — caps at 50.</description>
///   </item>
/// </list>
/// </summary>
public sealed class DefaultPeerScoringPolicy : IPeerScoringPolicy
{
    public const double LatencyDivisorMs = 10.0;
    public const double MaxLatencyPenalty = 60.0;
    public const int RejectWeight = 5;
    public const int MaxRejectPenalty = 50;
    public const int MaxRelayBackBonus = 50;
    public const int BaseScore = 100;

    public PeerScore Score(PeerTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var latencyPenalty = ClampDouble(telemetry.PingRttP95Ms / LatencyDivisorMs, 0.0, MaxLatencyPenalty);

        long rejectTotal = 0;
        foreach (var (_, count) in telemetry.RejectByClass)
            rejectTotal += count;
        var rejectPenalty = ClampDouble(RejectWeight * (double)rejectTotal, 0.0, MaxRejectPenalty);

        var relayBackBonus = telemetry.RelayBackInvCount < 0
            ? 0.0
            : Math.Min(telemetry.RelayBackInvCount, MaxRelayBackBonus);

        var raw = BaseScore - latencyPenalty - rejectPenalty + relayBackBonus;
        return new PeerScore((int)Math.Round(raw, MidpointRounding.AwayFromZero));
    }

    private static double ClampDouble(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;
}
