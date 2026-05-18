# Wave 4 Closeout — `observation-source-metrics-wave`

Status: **CLOSED**. Wave-level Codex audit chain completed —
A2 (MAJOR REVISION REQUIRED) → A2-followup (APPROVE WITH CHANGES,
2 LOW closed) → wave APPROVED. S0-S7 delivered; S8 (SPA page)
operator-deferred per the W2 / W3 pattern.

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
- [x] `audits/wave4-audit-A2.md` — MAJOR REVISION REQUIRED (0 C,
      3 H, 3 M, 2 L). Revision committed; see "A2 revision summary"
      below. Awaiting A2-followup.

## A2 revision summary (this commit)

Codex A2 verdict: MAJOR REVISION REQUIRED with 3 HIGH findings
covering concurrency + timestamp correctness in the visibility
tracker, 3 MEDIUMs on bucket immutability + admin-endpoint bounds
+ persistence round-trip coverage, and 2 LOWs. All 8 closed.

### H1 — TryAdd pattern in `SourceVisibilityTracker`

**Before:** `ConcurrentDictionary.GetOrAdd(_, factory)` was called
with a side-effect-bearing factory (`added = true`). Per the
ConcurrentDictionary contract, the factory CAN run even when the
returned value is NOT inserted (lost race), so two simultaneous
first observations could both increment `FirstSeen` AND the losing
source would skip the lag-bucket recording.

**After:** explicit `TryAdd(txId, candidate)` returns `true` iff
THIS call won the insert race. On loss, the code looks up the
existing entry via `TryGetValue` and falls through to the lock-
guarded `Sources.Add` path. Concurrent regression test
`Concurrent_TwoFirstObservations_SameTx_FirstSeenIncrementsExactlyOnce`
drives 32 parallel sources at the same txid through a
`Barrier.SignalAndWait` and asserts `totalFirstSeen == 1` +
`totalBucketHits == 31`.

### H2 — Lock-aware eviction + `Removed` flag

**Before:** eviction's `TryRemove` was outside the per-entry lock.
A late `RecordObservation` arriving DURING eviction could (a) get
a reference to the entry pre-removal, then (b) see eviction commit
`OnlySaw` (because `Sources.Count == 1` at that moment), then (c)
lock the in-memory-still-alive entry and record a lag bucket. End
state: both `OnlySaw[FirstSource]` AND `LagBuckets[lateSource]`
incremented for the same tx.

**After:** `EvictStaleEntries` takes `lock(entry)` BEFORE
`TryRemove`. Inside the lock it sets `entry.Removed = true` and
commits `OnlySaw` if `Sources.Count == 1`. `RecordObservation`'s
existing-entry path also takes the same lock and checks `Removed`
first — on true, it `continue`s the while-loop, which re-attempts
via `TryAdd` against a fresh dict slot. The retry installs a new
entry (correct semantics for an observation after the eviction
window has expired). Regression test
`EvictionRace_RecordObservationConcurrentWithEviction_NeverDoubleCounts`
fires 64 record + 64 evict tasks concurrently with a 1 ms eviction
window and asserts `OnlySaw + LagBuckets <= FirstSeen` invariant.

Also added: `_entries.TryRemove(new KeyValuePair<string, Entry>(...))`
overload — conditional remove that only succeeds if the dict's
current value is still the SAME instance, protecting against
remove-and-reinsert races.

### H3 — Pass source's `ObservedAt` timestamp, not `UtcNow`

**Before:** both `AppendAsync` overloads passed
`DateTimeOffset.UtcNow` to `tracker.RecordObservation`. This
measured local journal-processing latency rather than the source's
own first-seen timing, defeating the lag histogram's purpose.

**After:**

- `AppendAsync(TxMessage)` uses
  `DateTimeOffset.FromUnixTimeSeconds(message.Timestamp)` (the
  Bitails / JungleBus runners pass their `ObservedAt.ToUnixTimeSeconds()`
  into the factory). Falls back to `UtcNow` only when
  `message.Timestamp <= 0` (legacy / `RemovedFromMempool` defaults).
- `AppendAsync(TxObservation, payload, source)` uses
  `observation.ObservedAt ?? DateTimeOffset.UtcNow`.

Regression test
`JournalWriterVisibilityHookTests.AppendAsync_SourceNeutralOverload_UsesObservationObservedAt_NotUtcNow`
constructs an observation with `ObservedAt` 3 minutes in the past
and a second-source observation 100 ms after that — the lag bucket
should be index 2 (50-200 ms), proving the source timestamp drove
the calculation rather than the journal-tail wall clock (which
would have put the lag in bucket 5, >5 s).

### M1 — Immutable bucket boundaries

`SourceMetricsBuckets.UpperBoundsMs` was `public static readonly
long[]` — reassignment-safe but caller-mutable in place. Now the
backing array is `private` and the public accessor returns
`IReadOnlyList<long>`. Reflection test pins the static return type
so a future refactor to `long[]` fails the build.

### M2 — `lastN` clamp on admin endpoint

`AdminMetricsController.GetSourceMetrics` clamps the caller's
`lastN` to `min(requestedLastN, min(retention, HardLastNCeiling))`
where `HardLastNCeiling = 1440`. `lastN <= 0` short-circuits (no
history). Negative / zero / excessive bound tests in
`AdminMetricsControllerTests`.

### M3 — Aggregator round-trip via `ISnapshotPersistence`

Refactored: `SourceMetricsAggregator` no longer takes
`IDocumentStore` directly. Instead depends on a new
`ISnapshotPersistence` interface with `StoreAsync` /
`GetAllIdsOrderedAsync` / `DeleteAsync`. Production implementation
`RavenSnapshotPersistence` wraps the session-call chain that was
previously inline in the aggregator. Tests inject a
`FakePersistence` and assert: single-tick persists one snapshot;
below-retention no-eviction; above-retention oldest-deleted-first;
tracker eviction is triggered per tick. Five tests in
`SourceMetricsAggregatorTests`.

### L1 — Distinct-window DI flow-through assertion

`MetricsSetupDiResolutionTests.SourceVisibilityTracker_HonoursConfiguredEvictionWindow_DistinctFromDefault`
uses a 1 s configured window (vs 5 min production default) and
asserts both survival at 500 ms AND eviction at 1500 ms. The
production default would fail both assertions, proving the config
is actually flowing through.

### L2 — accepted as documented note

`SourceMetricsAggregator.EvictExcessSnapshotsAsync` still loads
all snapshot ids before deleting the excess. For default 720
retention that's trivial; the audit accepted this as a non-
blocking note. A future wave could add streaming-paged eviction
if retention grows substantially.

### Final test counts after A2 revision

- `Dxs.Bsv.Tests` 220/220 in isolation (one flaky
  timing-dependent test `PeerManager_FailureRecordsNegativeCooldown`
  fails under parallel test load — pre-existing, unrelated to
  W4 changes; passes when run alone).
- `Dxs.Consigliere.Tests` 412 passed (+20 from A2 revision:
  H1 +1 concurrent, H2 +1 race, H3 +2, M1 +1, M2 +2,
  M3 +5 aggregator, L1 +0 in-place rewrite of an existing
  assertion, + 8 integration ripple from existing test re-runs)
  + 24 explicit Skipped + 3 pre-existing baseline Raven-runtime
  failures (unchanged).

Metrics-filtered run: 65/65 passed (was 45 before A2 revision).

## A2-followup revision summary (final — this commit)

A2-followup verdict: **APPROVE WITH CHANGES**, 8 closed,
0 partial / regressed, 2 new LOW for test-evidence gaps. Both
closed in this commit.

### N1 — TxMessage overload regression test

The A2 H3 test only covered the source-neutral `AppendAsync`
overload; the `TxMessage` path was unverified post-fix.
`JournalWriterVisibilityHookTests` now adds:

- `AppendAsync_TxMessageOverload_UsesMessageTimestamp_NotUtcNow`:
  builds two `TxMessage.AddedToMempool` messages with the same
  txid — P2p from 5 seconds ago, Bitails from 4 seconds ago — and
  asserts the Bitails lag bucket is index 4 (1 s - 5 s), proving
  `message.Timestamp` (unix seconds) drove the lag math.
- `AppendAsync_TxMessageOverload_FallsBackToUtcNow_WhenTimestampIsZero`:
  pins the `> 0` threshold (default-zero from `RemovedFromMempool`
  falls back to UtcNow).
- `TestTransaction(seed)` helper builds a minimal valid serialized
  transaction so each test case hashes to a distinct txid without
  needing to mock the parser.

### N2 — `ClampLastN` extracted to internal helper

The pre-fix `AdminMetricsControllerTests` mirrored the clamp
arithmetic in a separate `Theory` rather than exercising the
production code. Now the controller exposes
`internal static int ClampLastN(int? requestedLastN, int retentionCount)`
and the test theory invokes it directly:

```text
[Theory]
[InlineData(null,         720, 0)]
[InlineData(0,            720, 0)]
[InlineData(-5,           720, 0)]
[InlineData(int.MinValue, 720, 0)]
[InlineData(50,           720, 50)]
[InlineData(720,          720, 720)]
[InlineData(721,          720, 720)]
[InlineData(5000,         720, 720)]
[InlineData(50,           0,   50)]
[InlineData(1500,         0,   1440)]
[InlineData(1500,         2000, 1440)]
[InlineData(int.MaxValue, 720, 720)]
[InlineData(int.MaxValue, 0,   1440)]
```

`Dxs.Consigliere` now grants `InternalsVisibleTo` to
`Dxs.Consigliere.Tests` so the test can call the internal helper +
read the internal `HardLastNCeiling` constant directly. The
controller's `GetSourceMetrics` body now reads
`var bounded = ClampLastN(lastN, _config.SnapshotRetentionCount);`
+ `if (bounded > 0)` — so the helper is the actual production
code path, not a parallel implementation.

### Wave 4 close

All slices closed (S2 / S8 deferred with documented rationale).
Audit chain: A2 → A2-followup APPROVE WITH CHANGES (closed).
Wave 5 (`broadcast-unification-wave`) and Wave 6
(`production-ops-wave`) may now open per the program dependency
graph in
`docs/stream-tasks/consigliere-thin-node-observer-program/master.md`.

Open follow-ups (not blocking; carried for a future wave):

- BSV-side flaky parallel-load test
  (`PeerManager_FailureRecordsNegativeCooldown`) — pre-existing,
  unrelated to W4.
- Streaming-paged retention eviction (A2 L2 note) — fine at
  default 720 retention; pathological config would benefit from
  paging.
- SPA `SourceMetricsPage.tsx` (S8) — deferred; admin REST endpoint
  is sufficient for ops + external dashboards.

### Final-final test counts

- `Dxs.Bsv.Tests` 220/220.
- `Dxs.Consigliere.Tests` 418 passed (+6 from A2-followup: 2 N1
  + 4 N2 expanded edge-case theory cases) + 24 explicit Skipped +
  3 pre-existing baseline Raven-runtime failures (unchanged).
- Metrics-filtered: 71/71 passed (was 65 pre-A2-followup, +6).
