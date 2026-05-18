---
created: 2026-05-18
type: program-closeout
parent: consigliere-thin-node-observer-program
status: closed
---

# Consigliere Thin-Node Observer Program — Closeout

Program-level closeout for `consigliere-thin-node-observer-program`.
Six waves delivered. This document is the authoritative end-state
record (Wave 6 S9 deliverable).

## Wave delivery

| Wave | Stream | Closeout commit | End state |
|---|---|---|---|
| W1 | `bsv-headers-chain-wave` | `557f8ad` (closeout) → `e7ec9cc` (A2 revision) | Headers chain + `BlockHeaderStore` + `PeerSession.Telemetry` contract frozen |
| W2 | `bsv-mempool-observer-wave` | `1bd7ee2` (closeout) → `848d7c4` (A2 revision) | Mempool watcher + `P2pMempoolIngestRunner` + per-session dispatcher + Gate 3 tx lifecycle |
| W3 | `reorg-handling-wave` | `1fab0ef` → `ca4f4c3` (A2-followup-3 final) | `ReorgDetector` + `ReorgPipeline` + degraded-reorg signal + `OrphanedTxRebroadcaster` |
| W4 | `observation-source-metrics-wave` | `5a53e08` | `SourceMetricsAggregator` + `SourceVisibilityTracker` + admin/metrics/sources |
| W5 | `broadcast-unification-wave` | `00f9cfc` | Unified `IBroadcastService.BroadcastAsync` collapsing all legacy broadcast paths |
| W6 | `production-ops-wave` | THIS COMMIT | Peer scoring + rotation, 4-rule alert poller, admin/p2p/alerts, inbound config stub, prod runbook + W5 changeout doc |

## Program-level Definition of Done verification

The program-level DoD from `master.md` §"Definition of Done"
checked one by one:

- [x] **All six waves are `done` or intentionally `not_opened`.**
  Verified: W1-W6 all `done`; no `not_opened`. W6 S8 (SPA alerts
  panel) operator-deferred per the W2 S8 / W3 S7 / W4 S8 / W5 S7
  pattern — recorded in the W6 closeout.
- [x] **`tests/Dxs.Bsv.Tests` and `tests/Dxs.Consigliere.Tests`
  pass with no new failures vs baseline.** Residual: 3 pre-W5
  Raven embedded-runtime skips persist (local .NET 8 absent;
  CI runs). Two known timing-flaky tests
  (`P2pMempoolIngestRunnerTests.RateLimited_Inv_RetriesAfterWindowSlides`
  + the BSV-side `PeerManager_FailureRecordsNegativeCooldown`)
  pass in isolation; documented as W5 residuals.
- [ ] **A real BSV mainnet transaction broadcast survives the
  full lifecycle.** Operator-deferred per the W2 S8 / W3 S7 /
  W4 S8 / W5 S7 SPA-defer pattern. The unified broadcast
  surface ships in W5 (`BroadcastUnificationGrepTests` + 6
  behavior + 2 production-DI tests pin it); end-to-end mainnet
  validation is an operator-session item recorded as the only
  open program-level residual.
- [x] **Admin panel shows P2P pool / headers / mempool / metrics
  / alerts.** Endpoint inventory:
  - `/api/admin/p2p/health` (W1, extended in W6 with
    `InboundEnabled`)
  - `/api/admin/p2p/peers` (W2)
  - `/api/admin/p2p/headers/tip` + `/api/admin/p2p/headers/recent` (W1)
  - `/api/admin/metrics/sources` (W4)
  - `/api/admin/p2p/alerts` (W6)
  All authorize via `AdminAuthDefaults.Policy`. SPA renderers
  for W4 metrics page and W6 alerts page are operator-deferred
  per the SPA-defer pattern — REST surfaces are the operator
  surface of record.
- [x] **`evidence/closeout.md` lists end-state metrics, delivery
  hashes, residuals, operator-facing changes.** This document.
- [x] **Public API change notes for the `Broadcast` contract
  published in `docs/platform-api/`.** Delivered in W6 S5 as
  `docs/platform-api/broadcast-w5-changeout.md`.

## Operator-facing changes (cumulative across W1-W6)

### New admin REST endpoints (W1-W6)

- `GET /api/admin/p2p/health` (W1; W6 added `InboundEnabled`)
- `GET /api/admin/p2p/peers` (W2)
- `GET /api/admin/p2p/headers/tip` (W1)
- `GET /api/admin/p2p/headers/recent?count=N` (W1)
- `GET /api/admin/metrics/sources?lastN=N` (W4)
- `GET /api/admin/p2p/alerts?lastN=N&since=unixMs` (W6)
- `POST /api/tx/broadcast` body `{ "rawHex": "..." }` (W5;
  replaces legacy `POST /api/tx/broadcast/{raw}`)

### New SignalR shapes (W1-W6)

- `wallethub.OnNewBlock(BlockTipDto)` (W1)
- `wallethub.OnReorg(ReorgEventDto)` (W3)
- `wallethub.invoke('Broadcast', rawHex)` now returns
  `BroadcastReceiptDto` instead of `bool` (W5).
- `OnBroadcastStateChanged` lifecycle stream (W2 + W5).

### New configuration sections

- `Consigliere:Broadcast:P2p` (W2 + W5 + W6 — full
  configuration referenced in `thin-node-prod-runbook.md`)
- `Consigliere:Broadcast:P2p:Headers` (W1)
- `Consigliere:Broadcast:P2p:Mempool` (W2)
- `Consigliere:Broadcast:P2p:TxPolicy` (W2)
- `Consigliere:Broadcast:P2p:Alert` (W6 — 4 thresholds + poll
  cadence + retention; all operator-tunable)
- `Consigliere:Broadcast:P2p:Inbound` (W6 — config-only stub)

### New documentation

- `docs/platform-api/thin-node-gate2-soak-runbook.md` (W2,
  pre-deployment 24h soak procedure)
- `docs/platform-api/thin-node-prod-runbook.md` (W6, steady-
  state production operations)
- `docs/platform-api/broadcast-w5-changeout.md` (W6, standalone
  wallet-team migration notes for the W5 broadcast contract
  change)

### Breaking changes

W5 removed the legacy multi-provider broadcast path. Wallet
clients must migrate per `broadcast-w5-changeout.md`. There is
no compatibility shim (vnext repo policy).

## Frozen contracts at program close

Per the W1 freeze + each wave's handoff facts:

- `PeerSession.Telemetry` surface (W1 contract freeze) — read
  by W4 source metrics + W6 peer scoring without amendment.
- `PeerTelemetry` record shape (W1) — same 15 fields throughout.
- `SourceMetricsSnapshot` + `SourceVisibilityCounters` (W4) —
  read by W6 SourceFirstDropout rule without amendment.
- `BroadcastReceiptDto` (W5 frozen by W1 S0.8).
- `P2pAlertEvent` + `P2pAlertType` enum (W6, frozen for any
  future ops-dashboard exporters).
- `P2pAlertResponse` / `P2pAlertEventDto` (W6, frozen for SPA +
  external consumers).
- `BsvP2pConfig.Inbound` config shape (W6 stub, frozen for the
  future inbound-listener wave).

## Residuals (post-program follow-ups, not blocking)

1. **Live mainnet broadcast validation** — W5 S7 / program DoD
   item, operator-session work.
2. **SPA renderers for W4 + W6 admin pages** — deferred per the
   SPA-defer pattern. REST surfaces are the operator surface of
   record.
3. **Alert escalation sinks** (Slack / PagerDuty / webhook) —
   W6 Out-of-scope; the Raven append-only log + admin polling
   is the W6 deliverable. A follow-up wave can add notification
   sinks reading from `IAlertEventRepository`.
4. **Inbound P2P listener implementation** — W6 ships the
   config stub only. A future wave can land the listener
   without a contract amendment.
5. **Score-weight tuning UI / runtime config** — W6
   `DefaultPeerScoringPolicy` ships with fixed documented
   weights; operator changes require a code edit (or a future
   config-driven policy implementation behind the same
   `IPeerScoringPolicy` interface).
6. **Per-peer manual eviction admin endpoint** — rotation is
   automatic in W6; a manual operator override is a post-W6
   follow-up.
7. **OpenTelemetry / Prometheus exporter for alerts** — W6
   Out-of-scope; future follow-up wave reading
   `P2pAlertEvent` documents.
8. **Raven `Broadcast` document archival** — historical-only
   after W5 (no current writer); a storage-hygiene follow-up
   can choose to archive or delete the historical docs.
9. **Timing-flaky tests** —
   `P2pMempoolIngestRunnerTests.RateLimited_Inv_RetriesAfterWindowSlides`
   + BSV `PeerManager_FailureRecordsNegativeCooldown` pass in
   isolation; documented as W5 residuals.

## Architecture invariants the program upholds

- **No bidirectional dependencies between waves.** Each wave's
  handoff facts (master.md §"Handoff facts → next wave") were
  honoured at every closeout; subsequent waves consumed the
  frozen surfaces without amending them.
- **Stop-and-audit per wave** (`audits/wave{N}-audit-A1` pre +
  `audits/wave{N}-audit-A2` post; slice-level audits for
  prerequisite slices). Six pre-execution audits + six
  post-execution audits + multiple followup passes recorded;
  every wave landed APPROVE or APPROVE WITH CHANGES with all
  findings folded in-wave.
- **Append-only Raven documents for journal + metrics + alerts.**
  W2 lifecycle, W4 snapshots, W6 alerts all follow the
  `{prefix}/{unixMs:D14}` id-padded pattern enforcing
  lex-ordered-equals-time-ordered queries and id-eviction
  retention.
- **vnext repo policy: no `[Obsolete]` shims, no backwards-
  compat for waves that change public contracts.** W5 broadcast
  collapse is the canonical example.
- **All admin endpoints behind `AdminAuthDefaults.Policy`.**
  W1-W6 endpoint inventory above.

## Audit trail

- 6 × `wave{N}-audit-A1` pre-execution audit prompts +
  Codex-verdict files.
- 6 × `wave{N}-audit-A2` post-execution audits (some with
  multiple followup passes).
- Slice-level audits for prerequisite slices: W6 S0
  (`wave6-S0-slice-audit*`), W6 S1+S2
  (`wave6-S1-S2-slice-audit*`).
- All findings folded in-wave; no carry-over findings into the
  program closeout.

## Program status: CLOSED

Six waves delivered. The only program-DoD item carried over to
post-closeout operator work is the live mainnet broadcast
validation, by design (per the SPA-defer pattern adopted
consistently across the program).
