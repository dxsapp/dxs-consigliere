# Launch — Wave 4: Observation Source Metrics

## Mission

Aggregate the in-memory counters W2 + W3 left behind into:

1. **`SourceMetricsSnapshot` Raven document** (append-only, rolling
   6-hour window @ 30 s intervals) carrying per-source counter
   values + cross-source lag-histogram buckets +
   `BsvP2pHealth.LastDegradedReorgAt`.
2. **`SourceVisibilityTracker`** in-process tracker that observes
   every successful tx observation across the three runners
   (P2p / Bitails / JungleBus) and computes first-seen counts,
   cross-source lag bucket distribution, and only-saw counts.
3. **Admin REST endpoint** `GET /api/admin/metrics/sources` that
   returns the latest snapshot + optionally the last-N historical.
4. **Fixture validation suite** that drives observations,
   triggers the aggregator manually, and asserts counter values +
   bucket counts match byte-for-byte (Core Rule §5: clock-skew-
   clamped to bucket 0).

End state at wave close:

- `SourceVisibilityTracker.RecordObservation(txId, source, at)`
  invoked from every runner's successful-observation site.
- `SourceMetricsAggregator` hosted service ticks at
  `SnapshotIntervalMs` (default 30 000) and persists snapshots
  to Raven; oldest snapshots evicted beyond
  `SnapshotRetentionCount` (default 720 = 6 hours @ 30 s).
- `AdminMetricsController` exposes the endpoint; DTO frozen for
  W6 + external consumers.
- DI regression test pins the new singleton graph.
- (Optional, deferrable) SPA `SourceMetricsPage.tsx` consumes the
  endpoint and renders per-source cards + histograms.

## Package path

`docs/stream-tasks/observation-source-metrics-wave/`
- `master.md` — goal, scope, ownership, slice ledger, DoD
- `launch-prompt.md` — this file
- `audits/` — slice + wave audit reports
- `evidence/` — closeout + validation evidence

## Prerequisites

- Wave 2 closed: `SourceObservationRecorder` +
  `TxObservationSource.{P2p, Bitails, JungleBus}` constants live.
- Wave 3 closed: `OrphanedTxRebroadcastRecorder` +
  `BsvP2pHealth.LastDegradedReorgAt` live.

## Stop-and-audit protocol

Same as W1 / W2 / W3:

1. Wave package draft committed → request wave-level
   pre-execution audit A1 prompt.
2. Address findings in-wave; commit revision.
3. S0 lands; request slice-A1 audit; address findings.
4. S1-S7 land in dependency order.
5. Wave-level post-execution audit A2; address findings.
6. Closeout (`evidence/closeout.md`); update parent program
   ledger; W4 row flipped to `done`.

S8 (SPA) may close after the wave audit if operator-deferred —
same pattern as W2 S8 / W3 S7.

## Scope guards (do NOT silently expand)

- No new hub events, no SignalR pushes for live metrics — admin
  SPA polls.
- No journal-shape changes; W4 reads recorders only.
- No per-peer metrics — that surface already exists in admin/p2p
  endpoints.
- No OpenTelemetry / Prometheus export — W6 ops concern.
- No alerting rules; aggregator writes only.

## Open scope questions for the audit

1. **Tracker hook location.** Where does
   `SourceVisibilityTracker.RecordObservation` get called: at each
   runner's individual observation site, or once at the
   journal-writer's tail (so the three runners don't each
   duplicate the wiring)?  The journal-writer is the natural
   single chokepoint but it's W1-frozen for shape — additive
   tracker call is acceptable; confirm.
2. **Bucket boundaries.** Default `<10ms, 10-50ms, 50-200ms,
   200ms-1s, 1s-5s, >5s`. Six buckets. Acceptable for the
   "lag histogram bucket counts deterministic" validation, or
   should the boundaries be configurable?
3. **Eviction window for "only-saw" counts.** Default 5 minutes
   after first-seen. Long enough for a slow source to catch up;
   short enough to bound tracker memory. Acceptable?
4. **SPA in-scope vs deferred.** W4 done-when literally says
   "admin API + SPA page show three sources with non-zero
   counters in fixture run", but the fixture validation is
   API-driven. Is the SPA a hard W4 deliverable or deferrable
   like W2 S8 live-mainnet?
5. **Snapshot retention.** 720 snapshots @ 30 s = 6 h rolling
   window. Configurable via `SourceMetricsConfig` but the
   default is the question.
