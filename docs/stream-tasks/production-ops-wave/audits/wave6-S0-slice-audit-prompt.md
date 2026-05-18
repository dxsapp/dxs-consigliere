# Wave 6 — S0 slice-audit prompt

Audit target: W6 S0 (the prereq slice) at the post-S0 commit
on `codex/consigliere-vnext`. Run sync; this gates S1+ open.

---

You are auditing the **first slice** of Wave 6
(`production-ops-wave`). S0 is the data-model + interface +
default policy for peer scoring — the foundation S1
(rotation) and S2 (alert poller) depend on.

Read:

- `docs/stream-tasks/production-ops-wave/master.md` (the
  post-A1 revised package; pass-2 verdict + this slice's row
  in the slice ledger)
- `docs/stream-tasks/production-ops-wave/audits/wave6-audit-A1-followup.md`
  (M2 — the documented weights this slice implements)
- `docs/stream-tasks/production-ops-wave/audits/wave6-audit-A1-followup-2.md`
  (M1 — score is per-tick derived, NOT persisted)

Cross-validate against the actual repo state:

- `src/Dxs.Bsv/P2p/Pool/PeerScore.cs` (new, S0)
- `src/Dxs.Bsv/P2p/Pool/IPeerScoringPolicy.cs` (new, S0)
- `src/Dxs.Bsv/P2p/Pool/DefaultPeerScoringPolicy.cs` (new, S0)
- `tests/Dxs.Bsv.Tests/P2p/Pool/PeerScoringTests.cs` (new, S0)
- `src/Dxs.Bsv/P2p/Chain/PeerTelemetry.cs` (the input shape)
- `src/Dxs.Bsv/P2p/Pool/PeerRecord.cs` (must be unchanged —
  A1-followup M1 forbids persisting the score)

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

1. **Formula fidelity.** Does `DefaultPeerScoringPolicy.Score`
   implement the formula from master.md §"Per-peer scoring +
   rotation" verbatim? (`latency_penalty = clamp(P95/10, 0, 60)`,
   `reject_penalty = clamp(5 * sum(RejectByClass), 0, 50)`,
   `relay_back_bonus = min(RelayBackInvCount, 50)`, final
   `clamp(100 - latency - reject + bonus, 0, 100)`.)

2. **Clamp coverage.** Does `PeerScore` clamp the constructor
   input both ways (`< 0 → 0`, `> 100 → 100`)? Are the
   component-level clamps also in place inside the policy?

3. **PeerRecord untouched.** Does `PeerRecord.cs` show any S0
   edit? It MUST NOT — score is per-tick derived (A1-followup
   M1).

4. **Test pin completeness.** Does the test suite pin: empty
   telemetry → 100; each component scaling independently;
   each component's clamp ceiling; multi-class reject sum;
   combined penalties hitting the floor clamp; combined
   penalty+bonus before clamp; `PeerScore` constructor clamp
   directly?

5. **Interface shape.** Is `IPeerScoringPolicy` a single-method
   pure-logic interface taking `PeerTelemetry` and returning
   `PeerScore`? Any I/O / async / extra parameters?

6. **PeerTelemetry contract.** Does S0 read `PeerTelemetry`
   without changing its shape? (W1 contract-freeze — any
   touch needs a contract-amendment slice.)

7. **No premature wiring.** S0 must NOT touch `PeerManager` /
   register anything in DI / change `BsvP2pSetup`. Rotation
   is S1.

8. **Negative-input safety.** If a `PeerTelemetry` has a
   negative `RelayBackInvCount` (corrupted / underflow), does
   the policy still return a clamped score?

9. **Numeric overflow.** `5 * sum(RejectByClass)` where the
   sum is `long.MaxValue`-ish — is the intermediate widened
   to a type that won't overflow before clamping?

## Verdict format

End with:

```
Verdict: APPROVE | APPROVE WITH CHANGES | MAJOR REVISION REQUIRED
Critical findings: <count>
High findings: <count>
Medium findings: <count>
Low findings: <count>
Headline: <one sentence>
```

Then per-finding detail (`C1`, `H1`, `M1`, `L1` etc.):

- Severity
- Slice (S0 or wave-level)
- Issue (1-2 sentences with file:line where applicable)
- Recommended fix (concrete)
