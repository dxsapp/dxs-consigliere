---
created: 2026-05-18
type: wave-closeout
parent: consigliere-thin-node-observer-program (Wave 6)
status: closed
---

# Wave 6 — Production Ops Wave Closeout

Wave 6 of `consigliere-thin-node-observer-program`. The FINAL
wave of the program. Per-wave evidence; the program-level
record is at
`../consigliere-thin-node-observer-program/evidence/closeout.md`
(W6 S9 deliverable).

## Slice delivery

| slice | commit | summary |
|---|---|---|
| package + A1 prompt | `3daf4ce` | Initial wave master + audit prompt |
| A1 revision (MAJOR) | `4eeefd1` | C1 + 2H + 3M + 2L applied to master |
| A1 pass-2 (CHANGES) | `550a262` | H1 zero-sample-window suppression + M1 S6 wording aligned to planner |
| S0 | `006c878` | `PeerScore` + `IPeerScoringPolicy` + `DefaultPeerScoringPolicy` + 16 unit tests |
| S0 slice-audit | `07fdac6` | Overflow-safe reject sum + edge pins (M1+L1+L2). 20 green |
| S1 | `895f799` | `PeerRotationPlanner` + `PeerManager` integration. 29 green |
| S2 | `8fcfeb6` | `P2pAlertPoller` + evaluator + Raven repo + 19 new tests |
| S1+S2 slice-audit prompt | `a70f26e` | 23 audit dimensions |
| S1+S2 slice-audit fix | `1832742` | Window-span guard + append-only + telemetry fault pin |
| S3 | `4dce3ea` | `GET /api/admin/p2p/alerts` + 17 controller tests |
| S4 | `518229e` | `Inbound` config stub + `InboundEnabled` health flag + 4 tests |
| S5 | `0854e28` | `thin-node-prod-runbook.md` + `broadcast-w5-changeout.md` |
| S6 | `dfcc06d` | 6-test done-when fixture suite + poller test seam |
| S7 | `701f7c8` | DI wiring + `W6_SingletonGraph_Resolves` + 2 tests |
| S8 | (deferred) | SPA AlertsPage — operator-deferred per W2/W3/W4/W5 pattern |
| S9 + W6 close | THIS COMMIT | Program closeout + W6 evidence + program-ledger flip |

## Test count

- `Dxs.Bsv.Tests`: 250/250 green (W6 added: 20 scoring +
  10 planner = 30 P2p/Pool tests).
- `Dxs.Consigliere.Tests` P2p + Metrics + Controllers + Setup
  (W6 scope): 6 done-when fixtures + 17 evaluator + 4 poller
  + 4 inbound + 17 controller alerts + 2 W6 DI = 50+ green
  tests added by this wave.
- Raven embedded integration:
  `RavenAlertEventRepositoryTests` (2 tests) skipped locally
  per the existing pattern; CI runs.

## Audit trail

Pre-execution wave audit A1 (Codex):
- Pass 1: MAJOR REVISION REQUIRED (1 C / 2 H / 3 M / 2 L) —
  `audits/wave6-audit-A1-followup.md`.
- Pass 2: APPROVE WITH CHANGES (0 C / 1 H / 1 M / 1 L) —
  `audits/wave6-audit-A1-followup-2.md`.
- Pass 3: APPROVE.

Slice-level audits:
- S0 slice-audit: APPROVE WITH CHANGES (0 C / 0 H / 1 M / 2 L) —
  `audits/wave6-S0-slice-audit-followup.md`.
- S1+S2 slice-audit: APPROVE WITH CHANGES (0 C / 1 H / 1 M /
  1 L) — `audits/wave6-S1-S2-slice-audit-followup.md`.

All audits + findings folded in-wave; no carry-over residuals.

## Operator-facing changes (W6 only)

### New REST endpoint

- `GET /api/admin/p2p/alerts?lastN=N&since=unixMs` — alert log
  (default page 20, retention-clamped, hard ceiling 1440).

### Extended REST endpoint

- `GET /api/admin/p2p/health` — added `InboundEnabled` to the
  `P2pHealthDto`.

### New configuration sections

- `Consigliere:Broadcast:P2p:Alert` — 8 fields covering
  enable, poll cadence, retention, 4 thresholds.
- `Consigliere:Broadcast:P2p:Inbound` — config-only stub
  (`Enabled` + `ListenPort`).

### New documentation

- `docs/platform-api/thin-node-prod-runbook.md` (226 lines) —
  steady-state production runbook.
- `docs/platform-api/broadcast-w5-changeout.md` (153 lines) —
  W5 broadcast contract migration notes for wallet teams.

### New behavior (production)

- Score-aware peer rotation in `PeerManager.TickAsync`:
  evicts at most one peer per tick whose score is strictly
  below `MinimumScoreToRetain` (default 30).
- Periodic alert poller (default 60 s) writes append-only
  `P2pAlertEvent` documents (`p2p/alerts/{unixMs:D14}` id
  format) for 4 rules: pool size, relay-back rate (delta-based,
  zero-sample-window suppressed), reorg depth, source first-
  seen dropout (window-span guarded).

## Frozen contracts (W6 handoff facts)

- `P2pAlertEvent` document shape + `P2pAlertType` enum —
  read by future Grafana / Prometheus exporters without
  amendment.
- `P2pAlertResponse` / `P2pAlertEventDto` — read by SPA +
  external consumers.
- `BsvP2pConfig.Inbound` skeleton — used by a future inbound-
  listener wave.
- `IPeerScoringPolicy` interface — swap in operator-tuned
  weights without changing the rotation planner.
- `PeerRotationPlanner.Plan` + `RotationDecision` shape —
  manual-evict admin endpoints (future) can call the planner
  directly.

## Residuals carried to the program closeout

S8 (SPA AlertsPage) operator-deferred. All other residuals are
post-program follow-ups recorded in
`../consigliere-thin-node-observer-program/evidence/closeout.md`
§"Residuals".

## Status: CLOSED

Wave 6 closes. Program closes with this same commit (S9
program-closeout doc landed in the same change).
