# thin-node S4 — slice-audit prompt (NOT_OPENED — verification only)

S4 was planned as "per-source first-seen latency metric + admin surface"
but is **already delivered** by the program's Wave 4
(`observation-source-metrics-wave`). No code shipped in this wave for S4.
This prompt is a verification checklist, not a diff audit.

## Why not_opened
- `src/Dxs.Consigliere/Services/Metrics/SourceVisibilityTracker.cs` already
  computes, per source: `FirstSeen` (cross-source race winner), `LagBuckets`
  (6-bucket lag distribution of laggards), `OnlySaw`.
- `TxObservationJournalWriter.cs` (L41, L95) calls
  `visibilityTracker?.RecordObservation(observation.TxId, message.Source, …)`
  for EVERY observation regardless of source — so `p2p` is tracked
  source-agnostically with no new wiring.
- Pipeline: tracker → `SourceMetricsCollector.SnapshotInto` →
  `SourceMetricsAggregator` (evict + snapshot) →
  `SourceMetricsSnapshot.VisibilityCounters` → `AdminMetricsController`
  `/api/admin/metrics/sources`.
- UI: `src/admin-ui/src/screens/source-metrics/` renders per-source
  first-seen sparklines; `source-metrics.store.test.ts` asserts
  `firstSeenSeries("p2p")`.

## Verify (read-only)
1. Confirm `RecordObservation` is reached for p2p observations end-to-end
   (the journal-writer call site fires for the P2P observer's appends).
2. Confirm the metrics screen lists `p2p` as a source label
   (`SOURCE_KEYS`/`SOURCE_LABEL` include p2p).
3. Confirm nothing in this wave (S1-S3, S5) regressed the metrics pipeline.

If any of the above is false, OPEN S4 as real work; otherwise confirm
not_opened.
