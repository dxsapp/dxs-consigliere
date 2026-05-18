# Wave 6 — S0 Slice Audit

Audit target: W6 S0 at commit `006c878`
(`feat(p2p): wave6 S0 — PeerScore + IPeerScoringPolicy + default policy`).

Verdict: APPROVE WITH CHANGES
Critical findings: 0
High findings: 0
Medium findings: 1
Low findings: 2
Headline: S0 keeps the scoring surface pure and correctly scoped, but the reject-count arithmetic needs overflow-safe clamping and the tests should pin the remaining edge cases before S1 opens.

## M1

- Severity: MEDIUM
- Slice: S0
- Issue: `DefaultPeerScoringPolicy.Score` sums reject counts into a `long` before widening to `double` (`src/Dxs.Bsv/P2p/Pool/DefaultPeerScoringPolicy.cs:48`). With multiple very large per-class counts, `rejectTotal += count` can overflow before the clamp, potentially turning a huge reject penalty into a low or zero penalty.
- Recommended fix: Saturate while summing, or widen each add before accumulation. For example, accumulate in `double`, clamp after each addition once the value reaches `MaxRejectPenalty / RejectWeight`, or use checked/saturating long arithmetic. Add a test with multiple `long.MaxValue`-ish class counts that still returns a score with the reject penalty capped at 50.

## L1

- Severity: LOW
- Slice: S0
- Issue: The relay-back component cap is implemented (`src/Dxs.Bsv/P2p/Pool/DefaultPeerScoringPolicy.cs:53`), but the current test does not prove the bonus cap because final `PeerScore` ceiling clamp also makes uncapped high relay-back values score 100 (`tests/Dxs.Bsv.Tests/P2p/Pool/PeerScoringTests.cs:50`). A regression removing `MaxRelayBackBonus` could pass the existing relay-back tests.
- Recommended fix: Add a test where penalties leave headroom, e.g. high latency plus `RelayBackInvCount = 200`, so capped bonus yields a distinct score from an uncapped bonus.

## L2

- Severity: LOW
- Slice: S0
- Issue: The policy handles negative `RelayBackInvCount` by treating it as zero (`src/Dxs.Bsv/P2p/Pool/DefaultPeerScoringPolicy.cs:53`), but the negative-input safety case from the prompt is not pinned in `PeerScoringTests`.
- Recommended fix: Add a test with `RelayBackInvCount = -1` (and optionally negative reject/latency inputs) asserting the score remains clamped and does not gain or lose bonus from corrupted negative telemetry.

## Clean Checks

- Formula fidelity: latency, reject, relay-back, and final score match the revised master formula (`docs/stream-tasks/production-ops-wave/master.md:63`).
- Clamp coverage: `PeerScore` clamps constructor input below 0 and above 100 (`src/Dxs.Bsv/P2p/Pool/PeerScore.cs:21`); component clamps are present in the policy.
- `PeerRecord` untouched: no S0 diff to `src/Dxs.Bsv/P2p/Pool/PeerRecord.cs`; score remains per-tick derived.
- Interface shape: `IPeerScoringPolicy` is a single synchronous pure method, `PeerScore Score(PeerTelemetry telemetry)` (`src/Dxs.Bsv/P2p/Pool/IPeerScoringPolicy.cs:19`).
- `PeerTelemetry` untouched: no S0 diff to the frozen record shape.
- No premature wiring: no S0 diff to `PeerManager` or `BsvP2pSetup`; rotation remains S1.

## Verification

- `dotnet test tests/Dxs.Bsv.Tests/Dxs.Bsv.Tests.csproj --filter FullyQualifiedName~PeerScoringTests`
  - Passed: 16
  - Failed: 0
