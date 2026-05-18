---
created: 2026-05-18
type: audit-followup
parent: wave6-S0-slice-audit
status: applied
---

# Wave 6 — S0 slice-audit followup (APPROVE WITH CHANGES)

S0 slice-audit verdict on commit `006c878`: APPROVE WITH
CHANGES (0 C / 0 H / 1 M / 2 L). All three findings closed
in this commit; test count 16 → 20.

---

## M1 — Reject-count arithmetic overflow risk

**Verified:** `DefaultPeerScoringPolicy.Score` summed
`RejectByClass` values into a `long` accumulator then
multiplied by 5 before clamping. A corrupted counter near
`long.MaxValue` would overflow the addition (`+=`) before
the clamp ever ran.

**Revision applied:** Saturating sum. Each per-class
contribution is capped at
`rejectSaturate = MaxRejectPenalty/RejectWeight + 1 = 11`
before being added; once the accumulator reaches the
saturation point, the loop breaks. Negative per-class counts
are skipped (treated as 0). The multiplication-then-clamp is
unchanged, but it now operates on a bounded total. New pins:

- `NegativeRejectCount_IsIgnored_NoBonusEffect` — a -100
  count alongside a +3 count yields the +3 penalty only.
- `RejectCount_NearLongMaxValue_DoesNotOverflow` — two
  `long.MaxValue` classes saturate at 50 (score = 50).

---

## L1 — Relay-back bonus cap not surfaced in tests

**Verified:** The existing `RelayBackBonus_CapsAtFifty_…`
test stacked the bonus against the base 100 only, so the
ceiling clamp at 100 absorbed any value above 50 — passing
the test without proving the cap held.

**Revision applied:** New test
`RelayBackBonus_CapsAtFifty_WhenPenaltiesPreventCeilingClamp`
combines max latency (-60) + max rejects (-50) so the bonus
is the only positive contribution. With `relayBack = 70`
and `relayBack = 1_000` the score is 40 in both — without
the cap the raw would be 100-60-50+1000 = 990 → ceiling
clamp to 100, masking the bug.

---

## L2 — Negative `RelayBackInvCount` not pinned

**Verified:** The policy already guarded against negative
relay-back at code level (`telemetry.RelayBackInvCount < 0 ? 0 : …`),
but no test pinned it. A future refactor could remove the
guard without a test failing.

**Revision applied:** New test
`NegativeRelayBackInvCount_IsTreatedAsZeroBonus` — a
telemetry with `RelayBackInvCount = -5` scores 100, not
some sub-100 value (which would be the bug a missing guard
would produce).

---

## Test count

Before: 16 passed.
After: 20 passed.

S0 gate cleared; S1 (`PeerRotationPlanner` + `PeerManager`
integration) opens.
