---
created: 2026-05-18
type: wave
parent: consigliere-thin-node-observer-program
status: draft (awaiting wave-level Codex audit A1)
---

# Wave 6 — Production Ops

## Goal

Operator-grade hardening that the program's goal statement
requires but earlier waves intentionally deferred. After this
wave closes, Consigliere has: per-peer scoring + rotation of
low-scoring peers in `PeerManager`; a periodic alert poller that
fires events for the 4 critical conditions named in the program
master (pool size, relay-back rate, reorg depth, source-first
dropout); an admin REST surface exposing the alert log; an
explicit "inbound P2P listener: OFF" decision recorded in
config + runbook; an operator runbook covering the new admin
pages; public-API change notes documenting the W5 broadcast
collapse for downstream consumers; cross-reference to the
existing `thin-node-gate2-soak-runbook.md`.

Business outcome: Consigliere is operable in production. Ops
can read a single alert feed instead of correlating logs across
counters; bad peers rotate out automatically instead of
silently hanging the pool; downstream wallet teams have a
single document describing the broadcast surface change. The
program closes after this wave.

## Product Decision

**Inbound P2P listener stays OFF in W6.** Master.md §"Wave 6"
allows "opt-in or off"; W6 commits to off-by-default + a config
flag (`Consigliere:Broadcast:P2p:Inbound:Enabled` default false)
that, when set true, logs a warning that inbound is not
implemented in this release. Rationale: implementing inbound
expands the threat surface (DoS, malicious-peer behaviour, NAT
traversal) that the program's scope does not budget for. The
config flag exists so a future wave can land inbound without a
contract amendment.

**Alert events are append-only Raven documents** (mirroring the
W4 `SourceMetricsSnapshot` pattern). The poller writes; the
admin endpoint reads; no SignalR push. The W4-precedented
"operator polls" model is intentional.

**SPA alerts panel deferred** per the W2 S8 / W3 S7 / W4 S8 /
W5 S7 pattern. The REST endpoint is the operator surface; the
React renderer is a thin follow-up.

## Scope

In scope:

- **Per-peer scoring + rotation (S1).**
  - New `PeerScore` value-type carried in `PeerRecord`. Score
    formula:
    `score = 100 - latency_penalty - reject_penalty + relay_back_bonus`,
    clamped to `[0, 100]`.
  - `IPeerScoringPolicy` interface (pure logic) +
    `DefaultPeerScoringPolicy` implementation. Pluggable so a
    future wave can swap in operator-tuned weights.
  - `PeerManager.TickAsync` rotation: when the pool is full and
    a fresh-peer slot is needed, evict the **lowest-scoring**
    active peer (instead of an arbitrary one).
  - **`MinimumScoreToRetain` config knob** (default 30). Peers
    scoring below this threshold are eligible for proactive
    eviction even when the pool isn't full, to drain bad peers
    quickly.
- **Critical alert poller (S2).**
  - New `P2pAlertPoller` (`IHostedService`) ticks every
    `AlertPollIntervalMs` (default 60_000 ms).
  - Rule set (4 rules per master.md §"Wave 6"):
    - `PoolSizeBelowThreshold`: `BsvP2pHealth.PoolSize < AlertThreshold.MinPoolSize`.
    - `RelayBackRateBelowThreshold`: averaged across peers via
      `PeerTelemetry.RelayBackInvCount`. Threshold: relay-back
      rate < 30 % in the last poll window means the network
      isn't picking up our announcements.
    - `ReorgDepthExceeded`: `BsvP2pHealth.LastDegradedReorgAt`
      within the last 5 minutes signals a degraded-state reorg
      requiring operator action.
    - `SourceFirstDropout`: any of the 3 known sources (P2p /
      Bitails / JungleBus) reports zero `FirstSeen` increments
      in the last hour (via the W4 visibility tracker).
  - Each fire writes a `P2pAlertEvent` Raven document (append-
    only, doc id `p2p/alerts/{unixMs:D14}`). Retention via doc-
    id eviction (default keep 720 events = ~12 h at 1-min poll).
- **Admin alerts endpoint (S3).**
  - `GET /api/admin/p2p/alerts` returns the latest N alert
    events (default 20; clamped via `ClampLastN` pattern from
    W5 A2 M2).
  - Optional `?since={unixMs}` filter for incremental polling.
  - Frozen DTO `P2pAlertResponse(IReadOnlyList<P2pAlertEventDto>)`
    for W7+ consumers + external dashboards.
- **Inbound listener decision (S4 — config-only).**
  - Add `BsvP2pConfig.Inbound { Enabled, ListenPort }` nested
    config section. Default `Enabled = false`.
  - Add a one-line warning log + `BsvP2pHealth.InboundEnabled`
    boolean exposing the decision so the admin page can show
    it. No actual inbound accept logic ships.
- **Operator runbook + change notes (S5).**
  - New `docs/platform-api/thin-node-prod-runbook.md` — covers
    the new admin pages (`/api/admin/metrics/sources`,
    `/api/admin/p2p/alerts`), the alert thresholds, and the
    peer-rotation explanation. Cross-references
    `thin-node-gate2-soak-runbook.md`.
  - New `docs/platform-api/broadcast-w5-changeout.md` — the
    W5 broadcast contract migration snippet expanded into a
    standalone document for downstream wallet teams.
- **Fixture validation suite (S6).**
  - `peer rotation evicts low-scoring peer in fixture` — drives
    `PeerManager.TickAsync` with seeded peer records at varying
    scores; asserts the lowest is evicted on rotation.
  - `alert fires when pool drops below threshold in fixture` —
    drives `P2pAlertPoller.TickOnceAsync` against a stubbed
    `BsvP2pHealth` returning `PoolSize = 1, MinPoolSize = 5`;
    asserts a `PoolSizeBelowThreshold` event lands in the fake
    document store.
  - Each of the other 3 alert rules also gets a deterministic
    fixture test.
- **DI regression test (S7).**
  - `W6_SingletonGraph_Resolves` mirrors the W4 / W5 pattern.
- **SPA alerts panel (S8 — deferrable).**
  - New `src/admin-ui/src/pages/AlertsPage.tsx` consuming
    `/api/admin/p2p/alerts`. Operator-deferred per the W2 / W3
    / W4 / W5 SPA-defer precedent.

Out of scope:

- **Inbound P2P listener implementation.** Config flag only;
  no accept logic. A future wave can land it once the threat
  model is budgeted.
- **Alert escalation / webhook / Slack / PagerDuty.** The Raven
  append-only log + admin polling is the W6 deliverable. A
  follow-up wave can add notification sinks.
- **Per-peer manual eviction admin endpoint.** Rotation is
  automatic; manual operator override is a post-W6 follow-up.
- **Score weight tuning UI / runtime config.** Default policy
  ships with fixed weights; operator changes require a code
  edit (or a future config-driven policy).
- **OpenTelemetry / Prometheus exporter for alerts.** Future
  follow-up; W6 internal-aggregation only.
- **`thin-node-gate2-soak-runbook.md` content rewrite.** W6
  references it; it stays as-is.

## Core Rules

1. **No new contract surfaces beyond frozen W1.** Alert events
   are internal admin-API DTOs; not in the hub-event contract.
2. **`PeerScore` is pure data + a pure-logic policy.** No I/O
   in scoring evaluation so the unit tests can drive
   deterministic scenarios.
3. **`P2pAlertPoller` writes append-only.** A fired event is
   immutable; subsequent evaluation may fire the same rule
   again (each fire = new document). Retention is doc-id
   eviction, not in-place update.
4. **Alert rules read from already-shipped surfaces.** No new
   counters or recorders in W6 — the poller composes from
   W2 / W3 / W4 outputs.
5. **Eviction is single-peer-per-tick.** `PeerManager.TickAsync`
   evicts at most one low-scoring peer per maintenance tick so
   the pool can refill before the next eviction decision —
   prevents flap loops.
6. **Score floor + ceiling clamps.** `[0, 100]`. A negative
   reject penalty cannot drop the score below 0; a relay-back
   bonus cannot push it above 100. Documented for operator
   intuition.
7. **`InboundEnabled = true` logs a warning + is no-op.** The
   decision-only stub is explicit so operators are not
   surprised.
8. **W2 + W4 + W5 prereq.** All closed (commit `00f9cfc` for W5).
9. **Stop-and-audit per wave.** S0 slice-level audit gates S1+
   open; S1-S7 covered by `audits/wave6-audit-A1.md`.

## Ownership Zones

| Program zone | Repo zone | Files (new unless noted) |
|---|---|---|
| `bsv-p2p-pool` | `bsv-protocol-core` | `src/Dxs.Bsv/P2p/Pool/{PeerScore,IPeerScoringPolicy,DefaultPeerScoringPolicy}.cs`; `src/Dxs.Bsv/P2p/Pool/PeerManager.cs` (edit — score-aware rotation in `TickAsync`); `src/Dxs.Bsv/P2p/Pool/PeerRecord.cs` (edit — `Score` field) |
| `consigliere-p2p-services` | `indexer-ingest-orchestration` | `src/Dxs.Consigliere/Services/P2p/{P2pAlertPoller,P2pAlertEvaluator,IAlertEventRepository,RavenAlertEventRepository}.cs`; `src/Dxs.Consigliere/Services/P2p/BsvP2pHealth.cs` (edit — `InboundEnabled` accessor) |
| `consigliere-config` | `service-bootstrap-and-ops` | `src/Dxs.Consigliere/Configs/BsvP2pConfig.cs` (edit — `Alert` + `Inbound` nested configs + `MinimumScoreToRetain`) |
| `consigliere-admin-api` | `public-api-and-realtime` | `src/Dxs.Consigliere/Controllers/AdminP2pController.cs` (edit — `GET /api/admin/p2p/alerts`); `src/Dxs.Consigliere/Data/Models/P2p/P2pAlertEvent.cs` (new doc) |
| `consigliere-setup` | `service-bootstrap-and-ops` | `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` (edit — register S2-S4 services + hosted poller) |
| `admin-ui` (out of catalog) | `admin-ui` | `src/admin-ui/src/pages/AlertsPage.tsx` + store (DEFERRED per S8) |
| `program-tests` | `verification-and-conformance` | `tests/Dxs.Bsv.Tests/P2p/Pool/PeerScoringTests.cs`; `tests/Dxs.Consigliere.Tests/Ops/{P2pAlertPollerTests,P2pAlertEvaluatorTests}.cs`; `tests/Dxs.Consigliere.Tests/Setup/W6_SingletonGraph_Resolves` |
| `program-docs` | `repo-governance` | `docs/platform-api/{thin-node-prod-runbook,broadcast-w5-changeout}.md`; `docs/stream-tasks/production-ops-wave/` |

### Handoff facts → next program

| Consumer | Consumes | Allowed change | Forbidden without amendment |
|---|---|---|---|
| External wallet teams | `broadcast-w5-changeout.md` migration doc | implement client-side migration | n/a (W5 is closed) |
| Future ops dashboards (Grafana / Prometheus exporter) | `P2pAlertEvent` doc shape + admin endpoint DTO | implement exporter | rename `P2pAlertType` enum values |
| Future inbound-listener wave | `BsvP2pConfig.Inbound` config skeleton + `BsvP2pHealth.InboundEnabled` flag | implement inbound accept logic | rename the config section |
| Future operator-action waves (manual evict, score tuning) | `IPeerScoringPolicy` interface + `PeerManager.TickAsync` eviction hook | swap policy + extend hook | rename `PeerScore` / `IPeerScoringPolicy` |

## Slice Ledger

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S0 | `bsv-p2p-pool` (`PeerScore` + `IPeerScoringPolicy` shape) | todo | — | new types compile; unit test pins clamp + formula; no `PeerManager` rotation change yet | data model + interface land with frozen contract; rotation still arbitrary | slice-A1 |
| S1 | `bsv-p2p-pool` (`PeerManager.TickAsync` score-aware rotation) | todo | S0 | unit + integration tests: seed pool with peers of varying score; assert lowest-scoring is evicted first; assert MinimumScoreToRetain proactive evict | rotation evicts lowest-scoring peer on full-pool + sub-floor peers proactively | wave-A1 |
| S2 | `consigliere-p2p-services` (`P2pAlertPoller` + evaluator + Raven repo) | todo | S0 | unit tests for evaluator (4 rules); integration test for poller via fake repo + mocked health | poller ticks, evaluator fires rules, repo writes alert events with append-only id | wave-A1 |
| S3 | `consigliere-admin-api` (`GET /api/admin/p2p/alerts`) | todo | S2 | controller test resolves through DI; returns latest N; `?since=` filter works; `lastN` clamped via the W5 helper pattern | endpoint live; DTO frozen for SPA + W7+ consumers | wave-A1 |
| S4 | `consigliere-config` (`Inbound.Enabled` opt-in stub) | todo | — | unit test: `Enabled=true` logs warning + `BsvP2pHealth.InboundEnabled = true`; no listener thread starts | config flag in place; admin health surface reflects it | wave-A1 |
| S5 | `program-docs` (runbook + change notes) | todo | S1, S2, S3 | manual review of `thin-node-prod-runbook.md` + `broadcast-w5-changeout.md`; cross-ref to `thin-node-gate2-soak-runbook.md` present | both docs land; cover scoring + alerts + Broadcast migration | wave-A1 |
| S6 | `program-tests` (fixture suite — done-when pins) | todo | S0-S4 | "alert fires when pool drops below threshold in fixture" + "rotation evicts low-scoring peer in fixture" plus the other 3 alert rules | every program-stated done-when scenario green in the fixture suite | wave-A1 |
| S7 | `consigliere-setup` (DI wiring + regression test) | todo | S1, S2, S3, S4 | `W6_SingletonGraph_Resolves` against the production DI graph; admin endpoint reachable | every W6 singleton resolves; hosted poller registers | wave-A1 |
| S8 | `admin-ui` (Alerts SPA page) | todo (deferrable) | S3 | manual smoke test in dev: SPA renders alerts from the fixture-driven endpoint | new page + store + API extension | wave-A1 |

S0 (`PeerScore` data model + scoring interface) is the
**prerequisite slice** required by the program launch rule. Its
slice-level audit gates S1+ open. S1-S7 covered by the
wave-level audit at `audits/wave6-audit-A1.md`. S8 may close
after the audit if operator-deferred (W2 / W3 / W4 / W5 SPA
defer pattern).

## Definition of Done

- All slices `done` (S8 may be operator-deferred with rationale).
- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- `dotnet test` returns no new failures vs the pre-W6 baseline
  (3 pre-existing Raven embedded-runtime failures unchanged
  from W5 close).
- Fixture validation suite green:
  - `PeerManager_Rotation_EvictsLowestScoringPeer`
  - `P2pAlertPoller_PoolBelowThreshold_FiresEvent`
  - `P2pAlertPoller_RelayBackRateBelowThreshold_FiresEvent`
  - `P2pAlertPoller_ReorgDepthExceeded_FiresEvent`
  - `P2pAlertPoller_SourceFirstDropout_FiresEvent`
- DI regression test green:
  `W6_SingletonGraph_Resolves`.
- `docs/platform-api/thin-node-prod-runbook.md` exists and
  cross-references the existing gate2 soak runbook.
- `docs/platform-api/broadcast-w5-changeout.md` exists with the
  full migration snippet.
- Wave-level Codex audit at `audits/wave6-audit-A1.md` returns
  APPROVE (or APPROVE WITH CHANGES addressed in-wave).
- `evidence/closeout.md` lists delivery hashes per slice and
  end-state metrics.

## Delivery Notes

Commit hashes recorded here as slices close.

- Wave package created: this commit (initial draft + A1 audit
  prompt)
- Wave audit A1: pending
