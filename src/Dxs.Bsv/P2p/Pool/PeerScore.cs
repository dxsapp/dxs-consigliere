#nullable enable
namespace Dxs.Bsv.P2p.Pool;

/// <summary>
/// Wave 6 S0 — integer health score [0, 100] derived per tick from
/// a peer's current <see cref="Chain.PeerTelemetry"/>. Higher is
/// better.
///
/// <para>Score is NOT persisted on <see cref="PeerRecord"/>; it is
/// recomputed each <see cref="PeerManager"/> maintenance tick by
/// the active <see cref="IPeerScoringPolicy"/> and consumed by the
/// rotation planner (W6 A1-followup M1).</para>
/// </summary>
public readonly record struct PeerScore
{
    public const int MinValue = 0;
    public const int MaxValue = 100;

    public int Value { get; }

    public PeerScore(int value)
    {
        Value = value < MinValue ? MinValue : value > MaxValue ? MaxValue : value;
    }

    public override string ToString() => Value.ToString();
}
