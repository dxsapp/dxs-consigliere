---
created: 2026-05-18
type: audit-followup
parent: wave6-audit-A1
status: applied to master.md
---

# Wave 6 — A1 audit followup (MAJOR REVISION REQUIRED → revised draft)

Audit verdict cited 1 CRITICAL + 2 HIGH + 3 MEDIUM + 2 LOW
findings. Headline: "W6 is directionally coherent, but two
planned alert rules and the peer-rotation fixture are not
implementable as specified against the current W2/W4/W5
surfaces."

This document lists each finding, the verified evidence in
the current repo, and the concrete revision applied to
`master.md`. All revisions are documentation-only at this
stage; no code has shipped.

---

## C1 — RelayBackRateBelowThreshold has no window/denominator

**Verified:** [PeerTelemetry.cs:30](../../../src/Dxs.Bsv/P2p/Chain/PeerTelemetry.cs#L30)
exposes lifetime-cumulative `RelayBackInvCount` (and lifetime
`GetDataRequestedCount` / `GetDataServedCount`). No
per-window or rate field. Core Rule §4 ("no new counters in
W6") rules out adding a recorder.

**Revision applied:** S2 evaluator carries its own in-memory
previous-tick snapshot of `(RelayBackInvCount,
GetDataRequestedCount)` keyed by peer; on each tick it
computes the per-peer **delta** against the previous tick,
sums across peers, and the rule fires when

```
sum(ΔRelayBackInv) / max(1, sum(ΔGetDataRequested)) < threshold
```

over the poll window. This is pure-logic delta arithmetic
against already-shipped counters — no new recorders, no
PeerTelemetry surface change. The first tick after startup
records the baseline and does NOT fire (no previous tick to
diff against).

Threshold default lifted into `BsvP2pConfig.Alert` so
operators can tune (see M3).

---

## H1 — SourceFirstDropout reads cumulative counters, not deltas

**Verified:** [SourceVisibilityTracker.cs:176](../../../src/Dxs.Consigliere/Services/Metrics/SourceVisibilityTracker.cs#L176)
writes cumulative `FirstSeen` into `SourceMetricsSnapshot`.
The W4 persistence retains a history of snapshots (the
aggregator stores append-only docs with a retention budget).

**Revision applied:** S2 evaluator reads the **last two
snapshots in the configured window** from the W4 persistence
(adds one new query method `GetSnapshotsInWindowAsync(windowMs,
ct)` on `ISnapshotPersistence` — a read-only query, no new
counter surface). For each source, the rule fires when
`FirstSeen(T_now) - FirstSeen(T_window_start) == 0` AND any
other source's delta is > 0 (a dropout that's not a
system-wide quiet period).

Window default 1 h, operator-tunable via
`BsvP2pConfig.Alert.SourceFirstDropoutWindowMs`.

---

## H2 — PeerManager.TickAsync is private + opens real sockets

**Verified:** [PeerManager.cs:124](../../../src/Dxs.Bsv/P2p/Pool/PeerManager.cs#L124)
is `private async Task TickAsync`. [PeerManager.cs:156](../../../src/Dxs.Bsv/P2p/Pool/PeerManager.cs#L156)
`TryConnectAsync` constructs a real `PeerSession` and calls
`session.ConnectAsync` — no test-double seam.

**Revision applied:** S1 ships **`PeerRotationPlanner`** — a
pure-logic type that takes
`(IReadOnlyList<(string Key, PeerScore Score)> active,
PeerRotationPolicy policy)` and returns a
`RotationDecision(IReadOnlyList<string> toEvict)`. The
planner is the unit of test for "evicts lowest-scoring peer";
fixtures drive it without touching sockets.

`PeerManager.TickAsync` keeps its current signature but
delegates the eviction decision to the planner. Integration
test for `PeerManager` end-to-end goes via the planner's
contract (golden inputs → golden eviction set), not by
spinning real sessions.

This also resolves M1's scoring-input-lifecycle concern: the
planner takes scores AS INPUT (computed per-tick from current
`PeerTelemetry` via `IPeerScoringPolicy.Score`), so scores
are **not** persisted on `PeerRecord` — `PeerRecord` stays
storage-only.

---

## M1 — Scoring input lifecycle unspecified

**Verified:** master.md draft mentioned `PeerRecord` carrying
score, but `PeerRecord` is persisted via `IPeerStore` (DNS
seeds + reconnect history). Persisting a score in
`PeerStore` would muddy the persisted shape and create stale
scores across restarts.

**Revision applied:** Score is **derived per tick** from
current `PeerTelemetry`, not stored on `PeerRecord`.
`IPeerScoringPolicy.Score(PeerTelemetry t) -> PeerScore`. The
`PeerRecord` edit in the draft ownership table is dropped.
The planner receives `(Key, Score)` tuples computed at the
tick boundary.

---

## M2 — Score formula weights undocumented

**Revision applied:** `DefaultPeerScoringPolicy` ships with
fixed documented weights:

| Component | Formula | Cap |
|---|---|---|
| `latency_penalty` | `PingRttP95Ms / 10.0` | clamped to `[0, 60]` (i.e. 600ms p95 = max penalty) |
| `reject_penalty` | `5 * sum(RejectByClass)` | clamped to `[0, 50]` (10 rejects = max penalty) |
| `relay_back_bonus` | `min(RelayBackInvCount, 50)` | natural cap at 50 |

Final score: `Clamp(100 - latency_penalty - reject_penalty + relay_back_bonus, 0, 100)`.

A unit test pins each component and the clamp. Weights are
constants in the default policy implementation; a future
config-driven policy can swap them without an interface
change.

---

## M3 — Alert thresholds not operator-tunable

**Revision applied:** All 4 alert thresholds move into a
`BsvP2pConfig.Alert` config section:

```csharp
public sealed class AlertConfig
{
    public int    MinPoolSize                  = 5;
    public double MinRelayBackRate             = 0.30;  // 30%
    public int    ReorgDepthWindowMs           = 5 * 60 * 1000;
    public int    SourceFirstDropoutWindowMs   = 60 * 60 * 1000;
    public int    AlertPollIntervalMs          = 60_000;
    public int    AlertRetentionEvents         = 720;   // ~12h @ 60s
}
```

Defaults match the original draft; operators can override per
`appsettings.json`. Documented in the new runbook (S5).

---

## L1 — Stale Gate 2 runbook cross-reference

**Revision applied:** The new
`docs/platform-api/thin-node-prod-runbook.md` (S5) adds an
explicit "Status of `thin-node-gate2-soak-runbook.md`"
paragraph: that document covered the W2 soak gate and
remains valid for the soak procedure itself; the new prod
runbook is the authoritative ops document for steady-state
operation. No content rewrite of the gate2 doc; cross-ref
explains both roles.

---

## L2 — No explicit final program closeout deliverable

**Revision applied:** New deliverable S9 (program closeout)
added to the slice ledger: writes
`docs/stream-tasks/consigliere-thin-node-observer-program/evidence/closeout.md`
with: final commit hashes per wave (W1-W6); program-level
done-when verification (every program master.md done-when
green); end-state file inventory; handoff notes for the
follow-up programs the master.md cross-references.

S9 is the **last** thing committed; gates the program from
"final wave done" to "program closed".

---

## Summary of changes to master.md

1. **Scope §"Per-peer scoring + rotation":** drops the
   `PeerRecord.Score` edit; adds `PeerRotationPlanner`
   pure-logic type.
2. **Scope §"Critical alert poller":** rewrites
   RelayBackRate rule (delta-based) and SourceFirstDropout
   rule (snapshot-history-delta-based); lifts thresholds
   into `BsvP2pConfig.Alert`.
3. **Scope §"Inbound listener decision":** unchanged.
4. **Scope §"Operator runbook + change notes":** adds the
   Gate-2 cross-ref paragraph + the alert-threshold table.
5. **Core Rules:** Rule 4 amended — alert evaluator MAY hold
   in-process previous-tick state for delta arithmetic; this
   is not a "new counter" because nothing new is recorded
   into a peer's lifetime telemetry.
6. **Ownership zones:** drops `PeerRecord.cs` edit; adds
   `PeerRotationPlanner.cs`; adds the new
   `ISnapshotPersistence.GetSnapshotsInWindowAsync` query.
7. **Slice ledger:** adds S9 (program closeout); S1
   validation pivots to the planner; S2 validation pivots to
   the delta-arithmetic + history-window mechanics.
8. **Definition of Done:** adds the program closeout
   deliverable + the explicit Gate-2 cross-ref check.

Result: every program-stated done-when remains in scope and
is implementable against the surfaces that actually exist in
the W4 + W5 closeout state.
