---
created: 2026-05-18
type: audit-followup
parent: wave6-audit-A1 (pass-2)
status: applied to master.md
---

# Wave 6 — A1 pass-2 followup (APPROVE WITH CHANGES)

Pass-2 verdict on commit `4eeefd1`: APPROVE WITH CHANGES
(0 C / 1 H / 1 M / 1 L). The pass-1 blockers (C1, H1, H2)
materially closed. Two enumerated changes + one un-enumerated
low remained.

---

## H1 — RelayBackRate false-fires in zero-sample windows

**Verified problem:** The pass-1 formula
`sum(ΔRelayBackInv) / max(1, sum(ΔGetDataRequested))` yields
0 when `sum(ΔGetDataRequested) == 0` (quiet window with no
broadcast / getdata activity), which is then < `MinRelayBackRate`
(0.30) and fires the rule despite there being no signal to
evaluate.

**Revision applied:** Master.md §Scope (S2 rule) now requires
two conditions for the rule to fire:

1. `sum(ΔGetDataRequested) > 0` — the window had at least one
   inv-request, i.e. there is signal to evaluate.
2. `sum(ΔRelayBackInv) / sum(ΔGetDataRequested) <
   AlertConfig.MinRelayBackRate`.

A window with `sum(ΔGetDataRequested) == 0` is **no-signal**
and the rule is a no-op for that tick. The `max(1, …)`
denominator clamp is removed from the spec because it was
the source of the false-fire.

**Fixture pin:** a new fixture test
`P2pAlertPoller_RelayBackRate_ZeroSampleWindow_DoesNotFire`
is added to S6 + the Definition of Done's named test list:
asserts NO `RelayBackRateBelowThreshold` event is written
when `sum(ΔGetDataRequested) == 0` over the poll window. A
paired test continues to assert the rule DOES fire when
`sum(ΔGetDataRequested) > 0 && sum(ΔRelayBackInv) == 0`.

---

## M1 — S6 still names `PeerManager.TickAsync` as the seam

**Verified problem:** S6 §"Fixture validation suite" still
read "drives `PeerManager.TickAsync` with seeded peer
records at varying scores", although the revised plan
already moved the eviction decision into the pure-logic
`PeerRotationPlanner`. The DoD named test
`PeerManager_Rotation_EvictsLowestScoringPeer` carried the
old framing.

**Revision applied:**
- S6 §"Fixture validation suite" now reads "drives
  `PeerRotationPlanner.Plan` with seeded `(Key, Score)`
  tuples at varying scores (no sockets, no real
  `PeerManager`); asserts the lowest-scoring key is in
  `RotationDecision.EvictKeys`".
- DoD test rename:
  `PeerManager_Rotation_EvictsLowestScoringPeer` →
  `PeerRotationPlanner_EvictsLowestScoringPeer`.

The handoff table's "Future operator-action waves" row
still cites `PeerManager.TickAsync eviction hook` —
deliberate, because external waves swap policy via the
planner's input contract, which is reached through
`PeerManager.TickAsync` at the integration boundary.

---

## L1 — un-enumerated in pass-2 summary

The pass-2 verdict reports `Low findings: 1` but the
summary text did not enumerate the specific issue. Action:
no spec change at this stage. If the next pass surfaces the
specific L, it gets a regular followup. Recording the
intentional no-op here so an auditor reading this trail
doesn't think the L was silently dropped.

---

## Summary of changes to master.md

1. **Scope §"Critical alert poller" — RelayBackRate rule:**
   rewritten to suppress zero-sample windows.
2. **Scope §"Fixture validation suite":** S6 rotation
   fixture pivots to `PeerRotationPlanner`; new
   zero-sample-window fixture added.
3. **Definition of Done:** rename
   `PeerManager_Rotation_EvictsLowestScoringPeer` to
   `PeerRotationPlanner_EvictsLowestScoringPeer`; add
   `P2pAlertPoller_RelayBackRate_ZeroSampleWindow_DoesNotFire`
   to the green-fixtures list.
4. **Frontmatter status + Delivery Notes:** updated to record
   pass-2 verdict + commit pointers.
