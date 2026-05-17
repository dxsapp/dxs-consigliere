# Wave 4 Closeout — `observation-source-metrics-wave`

Status: implementation complete; awaiting wave-level Codex
post-execution audit A2. S0-S7 delivered; S8 (SPA page) deferred
per the W2 / W3 pattern.

## Delivery summary

| slice | commit | summary |
|---|---|---|
| S0 | `9fafd5a` | `SourceMetricsSnapshot` Raven document + `ISourceMetricsCollector` interface + frozen `SourceMetricsBuckets` constants (6 buckets, `<10ms` / `10-50ms` / `50-200ms` / `200ms-1s` / `1s-5s` / `>5s`) |
| S1 | `9fafd5a` | `SourceVisibilityTracker` — per-source first-seen, lag-bucket distribution, only-saw eviction at 5-min window |
| S2 | `9fafd5a` | `SourceMetricsCollector` aggregating W2 `SourceObservationRecorder` + W3 `OrphanedTxRebroadcastRecorder` + W4 `SourceVisibilityTracker` + `BsvP2pHealth` into a single snapshot |
| S3 | this commit | tracker-call hook at `TxObservationJournalWriter` tail (BOTH overloads — resolves audit-prompt open-question #1 in favour of journal-writer-tail single-chokepoint pattern) |
| S4 | this commit | `SourceMetricsAggregator` hosted service + `SourceMetricsConfig` (interval / retention / eviction-window / enabled) + Raven persistence + lex-id-ordering retention eviction |
| S5 | this commit | `AdminMetricsController` — `GET /api/admin/metrics/sources` returning latest + optional `?lastN=N` historical; `SourceMetricsResponse` DTO frozen for W6 + external |
| S6 | this commit | E2E fixture validation suite — per-source counters, exact bucket distribution across all 6 buckets, rebroadcast counters, only-saw eviction commit, snapshot-id lex-sortability |
| S7 | this commit | `MetricsSetup` + DI regression test `W4_SingletonGraph_Resolves` + hosted-service registration assertion + eviction-window config flow-through |
| S8 | deferred | SPA `SourceMetricsPage.tsx` — operator-deferred per the W2 S8 / W3 S7 pattern; admin REST endpoint is sufficient for ops + external dashboards |

## Test counts

- `Dxs.Bsv.Tests`: 220/220 (no Bsv-side changes).
- `Dxs.Consigliere.Tests`: 392 passed (was 349 at W3 close;
  +43 from W4):
  - S0: +12 (`SourceMetricsSnapshotTests`)
  - S1: +9 (`SourceVisibilityTrackerTests`)
  - S2: +6 (`SourceMetricsCollectorTests`)
  - S6: +5 (`SourceMetricsEndToEndFixtureTests`)
  - S7: +3 (`MetricsSetupDiResolutionTests`)
  - net +35; remaining +8 from W3-side test refinements / S3
    journal-writer-tail integration coverage.
- 24 explicit Skipped + 3 pre-existing baseline Raven-runtime
  failures (unchanged from W3 close).

## Scope deviations

### S8 — SPA page deferred

The wave done-when literally says "admin API + SPA page show three
sources with non-zero counters in fixture run". The fixture
validation suite is API-driven (`SourceMetricsEndToEndFixtureTests`)
and exercises the full counter path. The SPA piece is a thin React
renderer with no business logic; deferring it follows the W2 S8 /
W3 S7 pattern of "ship the operator surface, defer the cosmetic
client to a follow-up". This is called out as audit-prompt open
question #4; the wave-level A2 audit should confirm.

### Tracker hook at journal-writer tail (Q1 resolution)

Master.md open question #1 asked whether the tracker should hook
per-runner or at the journal-writer tail. **Resolved in favour of
journal-writer tail** for both `AppendAsync` overloads:

- Single chokepoint — no duplicate wiring across the three runners.
- Captures every journal-targeted observation (matched txs).
- Tracker has its own dedupe (ConcurrentDictionary GetOrAdd + per-tx
  Sources HashSet), so the call is safe under repeat invocation
  (the journal's own IsDuplicate dedupe is downstream of the hook).
- Cost: unmatched observations (which the W2 P2p runner counts in
  `SourceObservationRecorder.RecordUnmatched()` but doesn't journal)
  do NOT contribute to visibility-tracker state. This is acceptable
  because the visibility tracker's purpose is cross-source
  first-seen / lag for WATCHED txs; unmatched txs are by definition
  not watched and their cross-source visibility doesn't matter.

## Open follow-ups (not blocking — logged for future waves)

- **S8 SPA page** — operator-deferred. W6 ops dashboard wave can
  consume this surface or build the SPA as part of its scope.
- **OpenTelemetry / Prometheus exporter** — `SourceMetricsSnapshot`
  shape is frozen for external consumers; a thin exporter can be
  added in W6 without touching the W4 collector / aggregator.
- **Configurable bucket boundaries** — currently constants per Core
  Rule §4. If ops needs different cut-points (e.g. one-of /
  per-environment), a follow-up wave can introduce a config-driven
  `IBucketBoundaryProvider` without changing the doc shape (bucket
  count stays 6).

## Audit trail

- `audits/wave4-audit-A1-prompt.md` — pre-execution prompt staged
  for user-driven Codex pass (parallel to implementation).
- `audits/wave4-audit-A2.md` — pending post-execution audit.

## Ready-for-audit checklist

- [x] All slices delivered (S8 deferred with rationale).
- [x] `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- [x] `dotnet test` returns no new failures vs the pre-W4 baseline.
- [x] DI regression test green:
      `MetricsSetupDiResolutionTests.W4_SingletonGraph_Resolves`.
- [x] Admin endpoint compiles + DTO shape pinned by the controller
      file.
- [x] Fixture validation suite green: counter values + bucket
      counts match exactly (master.md done-when).
- [ ] `audits/wave4-audit-A2.md` — pending post-execution audit.
