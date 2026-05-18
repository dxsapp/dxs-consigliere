using System.Collections.Generic;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Bsv.P2p.Pool;

namespace Dxs.Bsv.Tests.P2p.Pool;

/// <summary>
/// Wave 6 S0 — pins for <see cref="DefaultPeerScoringPolicy"/>.
/// Covers clamp + each weight component (latency / reject /
/// relay-back) + final formula per master.md A1-followup M2.
/// </summary>
public class PeerScoringTests
{
    [Fact]
    public void EmptyTelemetry_ScoresFullBase()
    {
        var score = new DefaultPeerScoringPolicy().Score(Telemetry());
        Assert.Equal(100, score.Value);
    }

    // Latency component: PingRttP95Ms / 10.0, clamped to [0, 60].
    [Theory]
    [InlineData(0.0, 100)]      // no penalty
    [InlineData(200.0, 80)]     // -20
    [InlineData(600.0, 40)]     // -60 (at the clamp threshold)
    [InlineData(10_000.0, 40)]  // -60 (clamp ceiling)
    public void LatencyPenalty_ScalesAndClamps(double p95Ms, int expectedScore)
    {
        var score = new DefaultPeerScoringPolicy().Score(Telemetry(pingP95Ms: p95Ms));
        Assert.Equal(expectedScore, score.Value);
    }

    // Reject component: 5 * sum(RejectByClass), clamped to [0, 50].
    [Theory]
    [InlineData(0, 100)]    // no penalty
    [InlineData(3, 85)]     // -15
    [InlineData(10, 50)]    // -50 (at the clamp threshold)
    [InlineData(100, 50)]   // -50 (clamp ceiling)
    public void RejectPenalty_ScalesAndClamps(long rejectCount, int expectedScore)
    {
        var score = new DefaultPeerScoringPolicy().Score(Telemetry(rejectCount: rejectCount));
        Assert.Equal(expectedScore, score.Value);
    }

    // Relay-back component: min(RelayBackInvCount, 50). Bonus is
    // added AFTER the base 100, so the result is then ceiling-clamped
    // by the PeerScore constructor.
    [Theory]
    [InlineData(0, 100)]
    [InlineData(30, 100)]    // base + 30 -> clamped to 100
    [InlineData(200, 100)]   // bonus caps at 50 then ceiling clamp
    public void RelayBackBonus_CapsAtFifty_ScoreCeilingClampsToHundred(long relayBack, int expectedScore)
    {
        var score = new DefaultPeerScoringPolicy().Score(Telemetry(relayBack: relayBack));
        Assert.Equal(expectedScore, score.Value);
    }

    [Fact]
    public void RejectByClass_SumsAcrossAllClasses()
    {
        // 2 PolicyRejected + 3 Invalid + 1 Conflicted = 6 -> 30 penalty
        var rejects = new Dictionary<RejectClass, long>
        {
            { RejectClass.PolicyRejected, 2 },
            { RejectClass.Invalid, 3 },
            { RejectClass.Conflicted, 1 },
        };
        var score = new DefaultPeerScoringPolicy().Score(Telemetry(rejectByClass: rejects));
        Assert.Equal(70, score.Value);
    }

    [Fact]
    public void HighPenalties_ScoreFloorClampsToZero()
    {
        // Max latency (-60) + max rejects (-50) = -110 from base 100
        // -> raw -10 -> floor clamps to 0.
        var score = new DefaultPeerScoringPolicy().Score(
            Telemetry(pingP95Ms: 10_000.0, rejectCount: 100));
        Assert.Equal(0, score.Value);
    }

    [Fact]
    public void BonusAndPenalties_Combine_BeforeClamp()
    {
        // latency_penalty = 200/10 = 20
        // reject_penalty  = 5 * 2  = 10
        // relay_back_bonus = min(15, 50) = 15
        // raw = 100 - 20 - 10 + 15 = 85
        var score = new DefaultPeerScoringPolicy().Score(
            Telemetry(pingP95Ms: 200.0, rejectCount: 2, relayBack: 15));
        Assert.Equal(85, score.Value);
    }

    [Fact]
    public void RelayBackBonus_CapsAtFifty_WhenPenaltiesPreventCeilingClamp()
    {
        // S0-audit L1 fix — surface the bonus cap without the ceiling
        // clamp absorbing it. With max latency (-60) + max rejects
        // (-50) the bonus is the ONLY positive contribution, so the
        // cap shows in the final score.
        // bonus capped at 50: raw = 100 - 60 - 50 + 50 = 40.
        var capped = new DefaultPeerScoringPolicy().Score(
            Telemetry(pingP95Ms: 600.0, rejectCount: 10, relayBack: 70));
        Assert.Equal(40, capped.Value);

        // Higher input → same score (cap holds). Without the cap the
        // raw would be 100 - 60 - 50 + 1_000 = 990 → ceiling-clamp to
        // 100, masking the bug.
        var farAboveCap = new DefaultPeerScoringPolicy().Score(
            Telemetry(pingP95Ms: 600.0, rejectCount: 10, relayBack: 1_000));
        Assert.Equal(40, farAboveCap.Value);
    }

    [Fact]
    public void NegativeRelayBackInvCount_IsTreatedAsZeroBonus()
    {
        // S0-audit L2 fix — corrupted / underflowed telemetry must
        // not yield a negative bonus that subtracts from the score.
        var score = new DefaultPeerScoringPolicy().Score(Telemetry(relayBack: -5));
        Assert.Equal(100, score.Value);
    }

    [Fact]
    public void NegativeRejectCount_IsIgnored_NoBonusEffect()
    {
        // S0-audit M1 fix — negative reject counts (corrupted data)
        // must not subtract from the positive total.
        var rejects = new Dictionary<RejectClass, long>
        {
            { RejectClass.PolicyRejected, 3 },
            { RejectClass.Invalid, -100 },
        };
        // 3 positive rejects → 15 penalty → 85.
        var score = new DefaultPeerScoringPolicy().Score(Telemetry(rejectByClass: rejects));
        Assert.Equal(85, score.Value);
    }

    [Fact]
    public void RejectCount_NearLongMaxValue_DoesNotOverflow()
    {
        // S0-audit M1 pin — saturating sum must keep the policy
        // total-bounded even if a single class is long.MaxValue.
        var rejects = new Dictionary<RejectClass, long>
        {
            { RejectClass.PolicyRejected, long.MaxValue },
            { RejectClass.Invalid, long.MaxValue },
        };
        var score = new DefaultPeerScoringPolicy().Score(Telemetry(rejectByClass: rejects));
        // Saturates at MaxRejectPenalty (50) → score = 100 - 50 = 50.
        Assert.Equal(50, score.Value);
    }

    [Fact]
    public void PeerScore_Constructor_ClampsValues()
    {
        Assert.Equal(0, new PeerScore(-50).Value);
        Assert.Equal(0, new PeerScore(0).Value);
        Assert.Equal(50, new PeerScore(50).Value);
        Assert.Equal(100, new PeerScore(100).Value);
        Assert.Equal(100, new PeerScore(150).Value);
    }

    private static PeerTelemetry Telemetry(
        double pingP95Ms = 0.0,
        long rejectCount = 0,
        long relayBack = 0,
        IReadOnlyDictionary<RejectClass, long>? rejectByClass = null)
    {
        var rejects = rejectByClass
            ?? (rejectCount == 0
                ? (IReadOnlyDictionary<RejectClass, long>)new Dictionary<RejectClass, long>()
                : new Dictionary<RejectClass, long> { { RejectClass.PolicyRejected, rejectCount } });

        return new PeerTelemetry(
            BytesIn: 0,
            BytesOut: 0,
            LastRecvUtc: null,
            LastSendUtc: null,
            PingRttP50Ms: 0.0,
            PingRttP95Ms: pingP95Ms,
            PingSampleCount: 0,
            GetDataRequestedCount: 0,
            GetDataServedCount: 0,
            GetDataServeP50Ms: 0.0,
            GetDataServeP95Ms: 0.0,
            RelayBackInvCount: relayBack,
            RejectByClass: rejects,
            ProtocolViolationCount: 0,
            LastDisconnectReason: null);
    }
}
