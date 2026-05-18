---
created: 2026-05-18
type: wave
parent: consigliere-thin-node-observer-program
status: draft (A1 pass-2 APPROVE WITH CHANGES — fixes applied; see audits/wave6-audit-A1-followup-2.md)
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
  - New `PeerScore` value-type (0-100 integer, clamped).
    **Derived per tick** from current `PeerTelemetry`, NOT
    persisted on `PeerRecord` (which stays storage-only).
  - `IPeerScoringPolicy` interface (pure logic) +
    `DefaultPeerScoringPolicy` implementation with documented
    fixed weights:
    - `latency_penalty = clamp(PingRttP95Ms / 10.0, 0, 60)` —
      600 ms p95 caps the penalty.
    - `reject_penalty = clamp(5 * sum(RejectByClass), 0, 50)` —
      10 rejects caps the penalty.
    - `relay_back_bonus = min(RelayBackInvCount, 50)`.
    - Final: `clamp(100 - latency_penalty - reject_penalty + relay_back_bonus, 0, 100)`.
  - New `PeerRotationPlanner` (pure-logic) takes
    `(active peers + their scores, RotationPolicy)` and
    returns a `RotationDecision(evictKeys)`. This is the unit
    of test for "evicts lowest-scoring peer in fixture"; no
    sockets touched (A1-followup H2 fix).
  - `PeerManager.TickAsync` delegates the eviction decision
    to the planner; rotation evicts the lowest-scoring active
    peer when the pool is full, and proactively evicts peers
    below `MinimumScoreToRetain` (default 30) even when the
    pool isn't full, to drain bad peers quickly.
- **Critical alert poller (S2).**
  - New `P2pAlertPoller` (`IHostedService`) ticks every
    `AlertPollIntervalMs` (default 60_000 ms).
  - Rule set (4 rules per master.md §"Wave 6"):
    - `PoolSizeBelowThreshold`: `BsvP2pHealth.PoolSize < AlertThreshold.MinPoolSize`.
    - `RelayBackRateBelowThreshold`: the evaluator carries an
      **in-process previous-tick snapshot** of
      `(RelayBackInvCount, GetDataRequestedCount)` per peer
      (lifetime counters from `PeerTelemetry`). Each tick
      computes per-peer deltas and sums across peers. The
      rule fires when **both** of the following hold:
      (a) `sum(ΔGetDataRequested) > 0` (the window had at
          least one inv-request — there is signal to
          evaluate), and
      (b) `sum(ΔRelayBackInv) / sum(ΔGetDataRequested) <
          AlertConfig.MinRelayBackRate` (default 0.30).
      A window with `sum(ΔGetDataRequested) == 0` is treated
      as **no-signal** and the rule is a no-op for that tick
      — suppresses false fires in quiet windows with no
      broadcast/getdata activity (A1-pass-2 H1 fix). The
      first tick after startup records the baseline only —
      no fire (A1-followup C1 fix). No new counters in
      `PeerTelemetry`.
    - `ReorgDepthExceeded`: `BsvP2pHealth.LastDegradedReorgAt`
      within the last 5 minutes signals a degraded-state reorg
      requiring operator action.
    - `SourceFirstDropout`: evaluator reads the **last two
      W4 `SourceMetricsSnapshot` documents** spanning at
      least `AlertConfig.SourceFirstDropoutWindowMs` (default
      1 h) via a new read-only query
      `ISnapshotPersistence.GetSnapshotsInWindowAsync`. For
      each source, fires when `FirstSeen(T_now) -
      FirstSeen(T_window_start) == 0` AND at least one other
      source's delta is > 0 (a dropout that's not a
      system-wide quiet period) (A1-followup H1 fix). No new
      counter; the tracker stays cumulative.
  - Each fire writes a `P2pAlertEvent` Raven document (append-
    only, doc id `p2p/alerts/{unixMs:D14}`). Retention via doc-
    id eviction (default keep 720 events = ~12 h at 1-min poll).
  - **All thresholds operator-tunable via `BsvP2pConfig.Alert`**
    (A1-followup M3 fix):

    ```csharp
    AlertConfig {
      MinPoolSize                = 5;
      MinRelayBackRate           = 0.30;
      ReorgDepthWindowMs         = 5 * 60_000;
      SourceFirstDropoutWindowMs = 60 * 60_000;
      AlertPollIntervalMs        = 60_000;
      AlertRetentionEvents       = 720;
    }
    ```

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
    **`PeerRotationPlanner.Plan`** with seeded `(Key, Score)`
    tuples at varying scores (no sockets, no real
    `PeerManager`); asserts the lowest-scoring key is in
    `RotationDecision.EvictKeys`. Mirrors the
    A1-pass-2 M1 fix: the planner is the test seam, not
    `PeerManager.TickAsync`.
  - `alert fires when pool drops below threshold in fixture` —
    drives `P2pAlertPoller.TickOnceAsync` against a stubbed
    `BsvP2pHealth` returning `PoolSize = 1, MinPoolSize = 5`;
    asserts a `PoolSizeBelowThreshold` event lands in the fake
    document store.
  - **`relay-back rule is a no-op in zero-sample windows`** —
    drives the evaluator with `sum(ΔGetDataRequested) == 0`
    across the poll window; asserts NO `RelayBackRateBelowThreshold`
    event is written (A1-pass-2 H1 fix pin). Paired test:
    non-zero requested + zero relay-back DOES fire.
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
   W2 / W3 / W4 outputs. The evaluator MAY hold in-process
   previous-tick state for delta arithmetic (e.g. lifetime
   counter Δ between ticks); this is not a "new counter"
   because nothing new is recorded into a peer's lifetime
   telemetry (A1-followup C1).
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
| `bsv-p2p-pool` | `bsv-protocol-core` | `src/Dxs.Bsv/P2p/Pool/{PeerScore,IPeerScoringPolicy,DefaultPeerScoringPolicy,PeerRotationPlanner,RotationDecision}.cs`; `src/Dxs.Bsv/P2p/Pool/PeerManager.cs` (edit — score-aware rotation in `TickAsync`, delegates eviction to planner). **No edit to `PeerRecord.cs`** — score is derived per-tick, not persisted (A1-followup M1 fix). |
| `consigliere-p2p-services` | `indexer-ingest-orchestration` | `src/Dxs.Consigliere/Services/P2p/{P2pAlertPoller,P2pAlertEvaluator,IAlertEventRepository,RavenAlertEventRepository}.cs`; `src/Dxs.Consigliere/Services/P2p/BsvP2pHealth.cs` (edit — `InboundEnabled` accessor); `src/Dxs.Consigliere/Services/Metrics/ISnapshotPersistence.cs` (edit — adds read-only `GetSnapshotsInWindowAsync` for H1 fix) |
| `consigliere-config` | `service-bootstrap-and-ops` | `src/Dxs.Consigliere/Configs/BsvP2pConfig.cs` (edit — `Alert` + `Inbound` nested configs + `MinimumScoreToRetain`) |
| `consigliere-admin-api` | `public-api-and-realtime` | `src/Dxs.Consigliere/Controllers/AdminP2pController.cs` (edit — `GET /api/admin/p2p/alerts`); `src/Dxs.Consigliere/Data/Models/P2p/P2pAlertEvent.cs` (new doc) |
| `consigliere-setup` | `service-bootstrap-and-ops` | `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` (edit — register S2-S4 services + hosted poller) |
| `admin-ui` (out of catalog) | `admin-ui` | `src/admin-ui/src/pages/AlertsPage.tsx` + store (DEFERRED per S8) |
| `program-tests` | `verification-and-conformance` | `tests/Dxs.Bsv.Tests/P2p/Pool/{PeerScoringTests,PeerRotationPlannerTests}.cs`; `tests/Dxs.Consigliere.Tests/Ops/{P2pAlertPollerTests,P2pAlertEvaluatorTests}.cs`; `tests/Dxs.Consigliere.Tests/Setup/W6_SingletonGraph_Resolves` |
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
| S0 | `bsv-p2p-pool` (`PeerScore` + `IPeerScoringPolicy` shape + `DefaultPeerScoringPolicy` with fixed weights) | **done — `07fdac6`** | — | new types compile; unit test pins clamp + each weight component (latency / reject / relay-back) + final formula | data model + interface + default policy land with frozen contract; rotation still arbitrary | slice-A1 cleared (20 green tests) |
| S1 | `bsv-p2p-pool` (`PeerRotationPlanner` + `PeerManager.TickAsync` integration) | **done** | S0 | planner unit tests: golden eviction set for varying-score fixtures; `MinimumScoreToRetain` proactive evict; `PeerManager` test exercises planner contract — no sockets opened | rotation evicts lowest-scoring peer on full-pool + sub-floor peers proactively; planner is the testable seam | wave-A1 |
| S2 | `consigliere-p2p-services` (`P2pAlertPoller` + evaluator + Raven repo + `ISnapshotPersistence.GetSnapshotsInWindowAsync`) | **done** | S0 | unit tests for evaluator (4 rules) including delta-based RelayBackRate + snapshot-history-delta SourceFirstDropout; integration test for poller via fake repo + mocked health | poller ticks, evaluator fires rules via in-process delta state + W4 snapshot history, repo writes alert events with append-only id | wave-A1 |
| S3 | `consigliere-admin-api` (`GET /api/admin/p2p/alerts`) | **done** | S2 | controller test resolves through DI; returns latest N; `?since=` filter works; `lastN` clamped via the W5 helper pattern | endpoint live; DTO frozen for SPA + W7+ consumers | wave-A1 |
| S4 | `consigliere-config` (`Inbound.Enabled` opt-in stub) | **done** | — | unit test: `Enabled=true` logs warning + `BsvP2pHealth.InboundEnabled = true`; no listener thread starts | config flag in place; admin health surface reflects it | wave-A1 |
| S5 | `program-docs` (runbook + change notes) | **done** | S1, S2, S3 | manual review of `thin-node-prod-runbook.md` + `broadcast-w5-changeout.md`; cross-ref to `thin-node-gate2-soak-runbook.md` present | both docs land; cover scoring + alerts + Broadcast migration | wave-A1 |
| S6 | `program-tests` (fixture suite — done-when pins) | todo | S0-S4 | "alert fires when pool drops below threshold in fixture" + "rotation evicts low-scoring peer in fixture" plus the other 3 alert rules | every program-stated done-when scenario green in the fixture suite | wave-A1 |
| S7 | `consigliere-setup` (DI wiring + regression test) | todo | S1, S2, S3, S4 | `W6_SingletonGraph_Resolves` against the production DI graph; admin endpoint reachable | every W6 singleton resolves; hosted poller registers | wave-A1 |
| S8 | `admin-ui` (Alerts SPA page) | todo (deferrable) | S3 | manual smoke test in dev: SPA renders alerts from the fixture-driven endpoint | new page + store + API extension | wave-A1 |
| S9 | `program-docs` (final program closeout) | todo | S1-S7 | manual review of `docs/stream-tasks/consigliere-thin-node-observer-program/evidence/closeout.md` — every program-level done-when from master.md verified green; commit hashes per wave (W1-W6); handoff notes | program-level closeout doc lands; program closed (A1-followup L2 fix) | wave-A1 |

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
  - `PeerRotationPlanner_EvictsLowestScoringPeer`
  - `P2pAlertPoller_PoolBelowThreshold_FiresEvent`
  - `P2pAlertPoller_RelayBackRateBelowThreshold_FiresEvent`
  - `P2pAlertPoller_RelayBackRate_ZeroSampleWindow_DoesNotFire`
    (A1-pass-2 H1 fix pin)
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
- **Program closeout** at
  `docs/stream-tasks/consigliere-thin-node-observer-program/evidence/closeout.md`
  exists and verifies every program-level done-when (S9).
- `thin-node-prod-runbook.md` includes the "Status of
  `thin-node-gate2-soak-runbook.md`" paragraph clarifying
  the two documents' respective roles (A1-followup L1).

## Delivery Notes

Commit hashes recorded here as slices close.

- Wave package created: commit `3daf4ce` (initial draft + A1
  audit prompt)
- Wave audit A1 pass-1 (commit `3daf4ce`): MAJOR REVISION
  REQUIRED — 1 C / 2 H / 3 M / 2 L; revisions in commit
  `4eeefd1` per `audits/wave6-audit-A1-followup.md`.
- Wave audit A1 pass-2 (commit `4eeefd1`): APPROVE WITH
  CHANGES — 0 C / 1 H / 1 M / 1 L; H1 (relay-back
  zero-sample window suppression) + M1 (S6 wording aligned
  to `PeerRotationPlanner`) applied in this commit per
  `audits/wave6-audit-A1-followup-2.md`.
