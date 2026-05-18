#nullable enable
using Dxs.Bsv.P2p.Chain;

namespace Dxs.Bsv.P2p.Pool;

/// <summary>
/// Wave 6 S0 — pure-logic policy that maps a <see cref="PeerTelemetry"/>
/// snapshot to a <see cref="PeerScore"/>.
///
/// <para>Implementations MUST be deterministic, side-effect free, and
/// O(1) in the size of the telemetry shape. The peer manager calls
/// <see cref="Score"/> on every active peer once per maintenance tick,
/// so the implementation is on the hot path of the rotation decision.</para>
///
/// <para>The default implementation is <see cref="DefaultPeerScoringPolicy"/>.
/// A future wave may ship operator-tunable weights via a config-driven
/// policy without changing this interface (W6 master.md handoff table).</para>
/// </summary>
public interface IPeerScoringPolicy
{
    PeerScore Score(PeerTelemetry telemetry);
}
