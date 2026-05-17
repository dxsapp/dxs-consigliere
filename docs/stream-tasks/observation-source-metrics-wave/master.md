---
created: 2026-05-18
type: wave
parent: consigliere-thin-node-observer-program
status: draft (awaiting wave-level Codex audit A1)
---

# Wave 4 — Observation Source Metrics

## Goal

Aggregate the in-memory counters W2 + W3 already maintain
(`SourceObservationRecorder`, `OrphanedTxRebroadcastRecorder`,
`BsvP2pHealth`) into per-source metrics surfaces:

1. A periodic Raven snapshot for rolling-window history.
2. An admin REST endpoint (`/api/admin/metrics/sources`) that
   ops + the SPA can poll.
3. A first-seen + cross-source lag histogram tracker so the
   program can answer "which source first saw this tx? how far
   behind were the others?".

Business outcome: Consigliere ops can see which observation
source is doing the work at any time — making the call to drop /
add / rotate sources data-driven instead of intuition-driven.

## Product Decision

W4 is an aggregation + read-surface wave; the heavy-lift recorder
infrastructure already exists from W2 / W3. The only new
in-memory tracker introduced here is `SourceVisibilityTracker`
which observes the multi-source first-seen / lag distribution.
The Raven snapshot is single-document append-only at a tunable
interval (default 30 s); rolling-window queries serve the SPA's
last-N-snapshots view.

The W3 audit pattern continues: every wave gets an A2
post-execution Codex audit; this package also ships an A1
pre-execution audit prompt for parallel review.

## Scope

In scope:

- **`SourceMetricsSnapshot` document model**
  (`src/Dxs.Consigliere/Data/Models/Metrics/`) — a single-row
  append-only Raven document per snapshot. Carries the
  aggregated per-source counters + the visibility-tracker
  buckets + `BsvP2pHealth.LastDegradedReorgAt` + the snapshot
  timestamp. Document ID: `metrics/sources/{snapshotUnixMs}`.
- **`ISourceMetricsCollector`** interface + production impl that
  reads from `SourceObservationRecorder`,
  `OrphanedTxRebroadcastRecorder`, `BsvP2pHealth`, and
  `SourceVisibilityTracker`, building a single
  `SourceMetricsSnapshot` instance. Pure (no I/O) for unit
  testability.
- **`SourceVisibilityTracker`** new in-memory tracker. Observes
  every successful tx observation (from W2's runner +
  Bitails / JungleBus runners) and computes:
  - `FirstSeenBySource[source]` — count of txids first observed
    by each source.
  - `LagBucketsBySource[source]` — bucket distribution of how
    long after the first source-of-record each subsequent source
    saw the same tx. Bucket boundaries: `<10ms`, `10-50ms`,
    `50-200ms`, `200ms-1s`, `1s-5s`, `>5s`. Six buckets.
  - `OnlySawBySource[source]` — count of txids that ONLY this
    source ever saw within the eviction window (default 5 min).
  Eviction loop runs every 30 s; evicted entries' `OnlySaw` value
  is committed once.
- **`SourceMetricsAggregator`** hosted service. Every
  `SnapshotIntervalMs` (default 30 000) calls
  `ISourceMetricsCollector.Collect` and writes the snapshot to
  Raven. Maintains last-N rolling window via doc-id eviction
  (default keep 720 snapshots = 6 hours @ 30 s).
- **Lag-record plumbing** into existing runners. On every
  successful matched / unmatched observation, the runner calls
  `SourceVisibilityTracker.RecordObservation(txId, source,
  observedAt)`. New plumbing in:
  - `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs`
  - `src/Dxs.Consigliere/BackgroundTasks/Realtime/BitailsRealtimeIngestRunner.cs`
  - `src/Dxs.Consigliere/BackgroundTasks/Realtime/JungleBusRealtimeIngestRunner.cs` (path/name TBD on survey)
  - The `TxObservationJournalWriter`'s source-neutral overload
    can host the call as a tail-side hook so the three runners
    don't each need to duplicate the wiring — DECIDE in slice
    audit.
- **Admin endpoint**
  `GET /api/admin/metrics/sources` returning:
  - the latest snapshot (current state);
  - optional `?lastN=60` returning the last N historical
    snapshots for trend rendering.
  Response DTO follows the existing
  `AdminP2pController.P2pHealthDto` pattern.
- **Fixture validation suite** matching the W4 validation matrix
  exactly: fixture-injected observations produce counter values
  the test asserts byte-for-byte equal, including lag bucket
  counts.
- **SPA page** (`src/admin-ui/src/pages/SourceMetricsPage.tsx`)
  consuming the admin endpoint via a new `source-metrics.store.ts`
  MobX store. Renders per-source counters + bucket histograms.
  Deferrable to a post-wave follow-up — see "Out of scope"
  rationale.

Out of scope:

- **SignalR push for live metric updates.** The admin SPA polls;
  pushing through `IWalletHub` would require a contract-freeze
  amendment in W1 and is unnecessary for W4's done-when.
- **OpenTelemetry / Prometheus export.** Future ops wave (W6)
  may add it; W4 is internal-aggregation-only.
- **Per-peer metrics.** `BsvP2pHealth.ActiveSessions` already
  exposes per-peer state via the existing admin/p2p endpoints;
  W4 aggregates by SOURCE (p2p / bitails / junglebus), not by
  peer.
- **The SPA page itself** may be deferred to a post-wave
  follow-up (like W2 S8 live-mainnet) if the admin REST endpoint
  + fixture validation suite alone satisfy the operator. W4's
  done-when explicitly mentions "admin API + SPA page show three
  sources with non-zero counters in fixture run" — but the
  fixture is API-driven; the SPA is a thin React renderer. The
  wave A1 audit should confirm whether the SPA is in-scope or
  deferred.
- **Metric retention beyond the 6-hour rolling window** (config
  default; tunable). Long-term archival is a separate concern.
- **Alerting rules** on metric thresholds. Operator-driven /
  external (Grafana / Prometheus). The aggregator only writes;
  it does not fire alerts.
- **No production-code changes in existing runners except the
  tracker hook.** The new
  `SourceVisibilityTracker.RecordObservation` call is the ONLY
  edit; everything else in W2 / W3 runners stays as-is.

## Core Rules

1. **No journal-shape changes.** `TxObservation` /
   `BlockObservation` are W1 / W2 contracts. W4 reads via the
   existing recorders only.
2. **Snapshot writes are append-only.** Once a
   `SourceMetricsSnapshot` is written, it is never edited.
   Rolling-window enforcement is purely doc-id eviction (delete
   the oldest).
3. **Tracker is in-process state.** Restart loses the tracker's
   buffer (active tx → first-seen mapping). Acceptable because
   the buffer's purpose is the 5-minute eviction window for
   "only-saw" counts; longer-term state lives in the snapshot
   doc history.
4. **Bucket boundaries are constants.** No runtime configuration
   of the histogram buckets. Operator dashboards encode the same
   boundaries.
5. **Lag is observed - first-seen.** Negative lags (clock skew /
   reordering) are clamped to 0 and counted in the first bucket.
6. **W3 prereq dependency.** Wave 3 closed 2026-05-18. W4
   consumes `BsvP2pHealth.LastDegradedReorgAt` +
   `OrphanedTxRebroadcastRecorder.*` as read-only inputs.
7. **Source enum stability.** Per program contract,
   `TxObservationSource.{Bitails, JungleBus, P2p}` are
   deterministic constants frozen by Wave 1 + Wave 2. W4 reads
   them as keys; renames require a contract-freeze amendment.
8. **Stop-and-audit per wave** (program rule). S0 gets its own
   slice-level audit before S1+ open; S1-S6 covered by the
   wave-level audit `audits/wave4-audit-A1.md`.

## Ownership Zones

| Program zone | Repo zone | Files (new unless noted) |
|---|---|---|
| `metrics-aggregation` (new) | `indexer-state-and-storage` | `src/Dxs.Consigliere/Data/Models/Metrics/SourceMetricsSnapshot.cs`; `src/Dxs.Consigliere/Services/Metrics/{ISourceMetricsCollector,SourceMetricsCollector,SourceVisibilityTracker,SourceMetricsAggregator}.cs` |
| `consigliere-p2p-realtime` | `indexer-ingest-orchestration` | tracker-call hooks in `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs` + the Bitails / JungleBus runners |
| `consigliere-admin-api` | `public-api-and-realtime` | `src/Dxs.Consigliere/Controllers/AdminMetricsController.cs` (new) |
| `consigliere-config` | `service-bootstrap-and-ops` | `src/Dxs.Consigliere/Configs/SourceMetricsConfig.cs` (new — snapshot interval, retention, bucket override) |
| `consigliere-setup` | `service-bootstrap-and-ops` | `src/Dxs.Consigliere/Setup/IndexerStateSetup.cs` (edit — register W4 services) |
| `admin-ui` (out of catalog) | `admin-ui` | `src/admin-ui/src/pages/SourceMetricsPage.tsx` + `stores/source-metrics.store.ts` + `types/api.ts` (extend) |
| `program-tests` | `verification-and-conformance` | `tests/Dxs.Consigliere.Tests/Metrics/`; `tests/Dxs.Bsv.Tests/` (no W4 Bsv-side tests expected — pure Consigliere work) |
| `program-docs` | `repo-governance` | `docs/stream-tasks/observation-source-metrics-wave/` |

### Handoff facts → consumers

| Consumer | Consumes | Allowed change | Forbidden without amendment |
|---|---|---|---|
| W5 broadcast-unification | `SourceObservationRecorder.GetInvObservedCount(source)` semantics unchanged; W4 reads them | unchanged | n/a |
| W6 production-ops | `SourceMetricsSnapshot` document shape for alarm rules + dashboards; admin endpoint for live polling | implement alert rules on top | rename document fields |
| External Grafana / Prometheus exporter (future) | `SourceMetricsSnapshot` shape | export via a new endpoint | rename document fields |

## Slice Ledger

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S0 | `metrics-aggregation` (snapshot document + collector interface) | todo | — | document compiles; collector interface compiles; unit test pins document field shape | `SourceMetricsSnapshot` + `ISourceMetricsCollector` land with frozen field set | slice-A1 |
| S1 | `metrics-aggregation` (`SourceVisibilityTracker` + buckets) | todo | S0 | unit tests: bucket boundary correctness, first-seen counts, only-saw eviction at window expiry, multi-source same-tx | `SourceVisibilityTracker.RecordObservation` + `Collect` return deterministic counts on fixture input | wave-A1 |
| S2 | `metrics-aggregation` (`SourceMetricsCollector` aggregating recorders) | todo | S0, S1 | unit tests with fake `SourceObservationRecorder` + `OrphanedTxRebroadcastRecorder` + `BsvP2pHealth` + `SourceVisibilityTracker`; assert collected snapshot mirrors inputs exactly | aggregator pulls in-memory state into a single `SourceMetricsSnapshot` | wave-A1 |
| S3 | `consigliere-p2p-realtime` (tracker-call hooks in runners) | todo | S1 | runner test confirms `SourceVisibilityTracker.RecordObservation` is called per successful observation; runner tests for P2p / Bitails / JungleBus all green; no regression in W2 / W3 existing runner tests | every successful runner observation path records into the tracker; no double-counting; no missing source tag | wave-A1 |
| S4 | `metrics-aggregation` (`SourceMetricsAggregator` hosted service + Raven snapshot) | todo | S0, S2 | unit test with mock `IDocumentStore`: aggregator calls `Collect` every `SnapshotIntervalMs`, writes via `StoreAsync`, evicts oldest beyond retention; smoke test with embedded Raven (Skippable) confirms doc persists | hosted service ticks, persists, evicts; configurable interval + retention via `SourceMetricsConfig` | wave-A1 |
| S5 | `consigliere-admin-api` (`AdminMetricsController` + DTOs) | todo | S0, S4 | controller test with mocked `IDocumentStore`: `GET /api/admin/metrics/sources` returns the latest snapshot; `?lastN=60` returns up to 60 historical snapshots ordered desc | endpoint live; DTO frozen for W6 + external consumers | wave-A1 |
| S6 | `program-tests` (end-to-end fixture validation suite) | todo | S0-S5 | drive observations through the three runners; trigger the aggregator manually; assert snapshot counters match fixture-injected exact counts; assert lag bucket counts match exact distribution (Core Rule §5: clock-skew-clamped) | every per-source counter + every histogram bucket matches the fixture's known input | wave-A1 |
| S7 | `consigliere-setup` (DI wiring + regression test) | todo | S0-S6 | new DI test `IndexerStateSetupDiResolutionTests.W4_SingletonGraph_Resolves` resolves every W4 service from a mocked external-deps service provider | every W4 singleton resolves from production DI graph | wave-A1 |
| S8 | `admin-ui` (SPA SourceMetricsPage) | todo (deferrable) | S5 | manual smoke test in dev: SPA page renders the metrics with non-zero counts from the fixture-driven backend; admin auth gate enforced | new page + store + API client extension; per-source cards + histogram view | wave-A1 |

S0 (`SourceMetricsSnapshot` + `ISourceMetricsCollector`) is the
**prerequisite slice** required by the program launch rule. Its
slice-level audit gates S1+ open. S1-S7 covered by the single
wave-level audit at `audits/wave4-audit-A1.md`. S8 (SPA) may close
after the audit if operator-deferred — same pattern as W2 S8 / W3
S7.

## Definition of Done

- All slices `done` (S8 may be operator-deferred with rationale).
- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- `dotnet test` returns no new failures vs the pre-W4 baseline
  (3 pre-existing Raven embedded-runtime failures unchanged from
  W3 close).
- Fixture validation suite green: counter values + bucket counts
  match exactly.
- DI regression test green:
  `IndexerStateSetupDiResolutionTests.W4_SingletonGraph_Resolves`.
- Admin endpoint reachable + returns expected DTO shape (smoke
  test).
- (If S8 lands) SPA page renders without console errors in dev
  build.
- Wave-level Codex audit at `audits/wave4-audit-A1.md` returns
  APPROVE (or APPROVE WITH CHANGES addressed in-wave).
- `evidence/closeout.md` lists delivery hashes per slice and
  end-state metrics.

## Delivery Notes

Commit hashes recorded here as slices close.

- Wave package created: `26a823f` (initial draft)
- Wave audit A1 pre-execution prompt: `26a823f`
  (`audits/wave4-audit-A1-prompt.md`); user runs Codex async
- Slices S0 + S1 + S2: `9fafd5a` (snapshot doc + tracker +
  collector + 27 unit tests)
- Slices S3 + S4 + S5 + S6 + S7: this commit (journal-writer
  tail hook + hosted aggregator + admin endpoint + E2E fixture
  suite + DI regression test + Startup wiring)
- Slice S8 (SPA): deferred — see `evidence/closeout.md`
  "Scope deviations"
- Wave closeout evidence: `evidence/closeout.md`
- Wave audit A2 (post-execution): pending
