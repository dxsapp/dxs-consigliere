# Wave 6 — Pre-Execution Audit A1 Second Pass

Audit target: `docs/stream-tasks/production-ops-wave/` at commit `4eeefd1`
(`docs(ops): wave6 A1 audit MAJOR REVISION applied`).

This pass re-ran the A1 prompt after the `master.md` revision documented in
`audits/wave6-audit-A1-followup.md`.

Verdict: APPROVE WITH CHANGES
Critical findings: 0
High findings: 1
Medium findings: 1
Low findings: 1
Headline: The A1 blockers are materially closed, but the revised relay-back rule must suppress zero-sample windows and the S6 fixture text still needs to align with the new planner seam.

## Prior A1 Closure Check

| Prior finding | Status | Evidence | Residual concern |
|---|---|---|---|
| C1 — relay-back window/denominator missing | CLOSED WITH NOTE | `master.md:85-94` now defines previous-tick deltas from lifetime `PeerTelemetry` counters. | See H1: zero-sample windows still false-fire because the denominator uses `max(1, sum(ΔGetDataRequested))`. |
| H1 — SourceFirstDropout cumulative counters | CLOSED WITH NOTE | `master.md:98-107` switches to W4 snapshot-history deltas via `ISnapshotPersistence.GetSnapshotsInWindowAsync`. | Implementation should no-op when insufficient snapshot history exists. |
| H2 — `PeerManager.TickAsync` private + sockets | CLOSED WITH NOTE | `master.md:70-79` and `master.md:242-244` add `PeerRotationPlanner` as the pure fixture seam. | See M1: the older S6 prose still names `PeerManager.TickAsync`. |
| M1 — scoring input lifecycle | CLOSED | `master.md:58-60`, `master.md:220` drop persisted `PeerRecord.Score`; score is derived per tick from telemetry. | |
| M2 — score weights undocumented | CLOSED WITH NOTE | `master.md:63-69` locks latency/reject/relay-back formula and caps. | See L1: `MinimumScoreToRetain = 30` still lacks a short rationale. |
| M3 — thresholds not tunable | CLOSED | `master.md:111-123` adds `BsvP2pConfig.Alert` defaults for all alert thresholds and retention. | |
| L1 — stale Gate 2 runbook cross-reference | CLOSED | `master.md:286-288` requires a status paragraph in the prod runbook. | |
| L2 — final program closeout missing | CLOSED | `master.md:251`, `master.md:283-285` add S9 and a program closeout DoD item. | |

## H1

- Severity: HIGH
- Slice: S2
- Issue: The revised relay-back rule computes `sum(ΔRelayBackInv) / max(1, sum(ΔGetDataRequested)) < AlertConfig.MinRelayBackRate` (`docs/stream-tasks/production-ops-wave/master.md:85`). After the first baseline tick, a quiet window with no outgoing broadcast/getdata activity has `ΔGetDataRequested = 0` and `ΔRelayBackInv = 0`, so it evaluates as `0 / 1 < 0.30` and fires even though there was no sample to evaluate. `TxRelayCoordinator` only increments these telemetry counters when a pending outgoing tx is involved (`src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs:159`, `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs:186`), so quiet windows are expected.
- Recommended fix: Amend S2 to treat `sum(ΔGetDataRequested) == 0` as `InsufficientSample` / no alert. Pin with a fixture: first tick baselines, second tick with zero deltas does not fire, third tick with `ΔGetDataRequested > 0` and low relay-back does fire.

## M1

- Severity: MEDIUM
- Slice: S6
- Issue: The revised S1 plan correctly pivots to `PeerRotationPlanner`, but the fixture bullet still says "`peer rotation evicts low-scoring peer in fixture` — drives `PeerManager.TickAsync` with seeded peer records" (`docs/stream-tasks/production-ops-wave/master.md:147`). That conflicts with the H2 fix and can steer implementation back toward private-method/socket-coupled tests.
- Recommended fix: Replace the S6 bullet with planner-first wording, e.g. "`PeerRotationPlanner_Rotation_EvictsLowestScoringPeer` drives scored active-peer fixtures; `PeerManager` integration only asserts the planner is invoked / one eviction per tick without opening sockets."

## L1

- Severity: LOW
- Slice: S0 / S1
- Issue: The score weights are now documented, and `MinimumScoreToRetain` is configurable, but the default `30` still lacks a one-sentence operator rationale (`docs/stream-tasks/production-ops-wave/master.md:75`). The A1 prompt explicitly asked whether 30/100 is justified.
- Recommended fix: Add a short rationale near the default: e.g. "30 keeps peers with either moderate latency or a few rejects, but evicts peers that combine high p95 latency with repeated rejects and no relay-back evidence." Keep the exact wording aligned to the final scoring formula.

## Accepted Decisions

- Inbound listener OFF + config-warning stub remains acceptable.
- One-peer-per-tick eviction remains the right safety rule for this pool size.
- Append-only Raven alerts plus REST polling remain acceptable for W6.
- SPA defer remains acceptable under the W2/W3/W4/W5 precedent.
- `IPeerScoringPolicy` belongs in `Dxs.Bsv` as long as it stays pure and consumes only BSV-layer telemetry/scores; the revised plan satisfies that.
- The platform-doc split is correct: production runbook and W5 broadcast changeout should remain separate, with S9 handling final program closeout.
