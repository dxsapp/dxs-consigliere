# Wave 4 — Post-execution audit A2 prompt

Audit target: implementation of `observation-source-metrics-wave`
at the wave-closeout commit. Run after the A1 verdict has landed
(or in parallel — A1 covers the plan, A2 covers the code).

---

You are auditing the executed implementation of Wave 4 of
`consigliere-thin-node-observer-program`. Read the closeout first:
`docs/stream-tasks/observation-source-metrics-wave/evidence/closeout.md`.
Then audit each shipped artefact against the wave's Core Rules in
`docs/stream-tasks/observation-source-metrics-wave/master.md`.

## Files to audit

**S0 — snapshot doc + collector interface**
- `src/Dxs.Consigliere/Data/Models/Metrics/SourceMetricsSnapshot.cs`
- `src/Dxs.Consigliere/Services/Metrics/ISourceMetricsCollector.cs`
- `tests/Dxs.Consigliere.Tests/Metrics/SourceMetricsSnapshotTests.cs`

**S1 — SourceVisibilityTracker**
- `src/Dxs.Consigliere/Services/Metrics/SourceVisibilityTracker.cs`
- `tests/Dxs.Consigliere.Tests/Metrics/SourceVisibilityTrackerTests.cs`

**S2 — SourceMetricsCollector**
- `src/Dxs.Consigliere/Services/Metrics/SourceMetricsCollector.cs`
- `tests/Dxs.Consigliere.Tests/Metrics/SourceMetricsCollectorTests.cs`

**S3 — journal-writer tail hook (Q1 resolution: single chokepoint)**
- `src/Dxs.Consigliere/BackgroundTasks/TxObservationJournalWriter.cs`
  (read the `visibilityTracker?.RecordObservation` calls on BOTH
  `AppendAsync` overloads — TxMessage path and source-neutral path)

**S4 — aggregator hosted service**
- `src/Dxs.Consigliere/Services/Metrics/SourceMetricsAggregator.cs`
- `src/Dxs.Consigliere/Configs/SourceMetricsConfig.cs`

**S5 — admin endpoint**
- `src/Dxs.Consigliere/Controllers/AdminMetricsController.cs`

**S6 — E2E fixture suite**
- `tests/Dxs.Consigliere.Tests/Metrics/SourceMetricsEndToEndFixtureTests.cs`

**S7 — DI wiring + regression test**
- `src/Dxs.Consigliere/Setup/MetricsSetup.cs`
- `src/Dxs.Consigliere/Startup.cs` (read the
  `.AddMetricsZoneServices(configuration)` line)
- `tests/Dxs.Consigliere.Tests/Metrics/MetricsSetupDiResolutionTests.cs`

**S8 — DEFERRED**. Confirm the deferral rationale in closeout
"Scope deviations §S8 — SPA page deferred" is acceptable.

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** / **LOW**.

1. **Each Core Rule** (master.md §"Core Rules" 1-8):
   - §1: no journal-shape changes — `TxObservation` /
     `BlockObservation` unedited?
   - §2: snapshots append-only — does `Persist` call
     `StoreAsync` (not modify in place)?
   - §3: tracker is in-process state — restart loses the buffer;
     is the 5-min eviction window justified given that?
   - §4: bucket boundaries are constants — confirm
     `SourceMetricsBuckets.UpperBoundsMs` is read-only / no setter.
   - §5: negative lag clamps to bucket 0 — verify
     `IndexFor(-1) == 0`.
   - §6: snapshot eventual-consistency across counters —
     `Collect` reads each counter via `Interlocked.Read`; the
     read set is not transactional. Acceptable per master.md?
   - §7: source enum stability —
     `KnownSources` field in `SourceMetricsCollector` references
     `TxObservationSource.{P2p, Bitails, JungleBus}`. Confirm.

2. **Tracker hook position (Q1 resolution).** Master.md open
   question #1 was resolved at journal-writer tail. Pros: single
   chokepoint, both overloads covered. Cons (closeout admits):
   unmatched observations don't reach the tracker. Was that the
   right call, or should the tracker also see unmatched txs?

3. **Eviction race revisited.** Master.md open question #5
   asked about the race between a late observation and the
   eviction commit. The tracker's eviction uses
   `_entries.TryRemove` to gate the OnlySaw commit, so a late
   observation arriving DURING eviction is either (a) rejected
   (entry already removed) or (b) succeeds (entry not yet
   removed). Verify the atomicity holds.

4. **Aggregator concurrency.** Singleton hosted service. The
   loop runs on a `Task.Run` background thread; `TickOnceAsync`
   is also exposed for tests. Could a test call `TickOnceAsync`
   while the loop is running and corrupt retention eviction?
   The aggregator is singleton so there's one loop; but tests
   are responsible for not racing.

5. **Retention eviction safety.** `EvictExcessSnapshotsAsync`
   loads ALL snapshot ids into memory then deletes the oldest
   excess. For 720 retention, that's 720 strings — trivial. For
   pathologically large retention or stale state from a config
   change (retention reduced), the load could be large. Worth
   batching?

6. **Admin endpoint behaviour.** `?lastN=0` returns no history
   (history list stays empty per the `lastN is > 0` check).
   Negative `lastN`? Excessive `lastN`? Are there bounds we
   should enforce?

7. **Test coverage gaps.** Compare master.md S6 criteria against
   `SourceMetricsEndToEndFixtureTests`. Does it exercise:
   - All three sources with non-zero counters? (yes via
     `Fixture_ThreeSources_OneWatchedTx_*`)
   - Exact 6-bucket distribution? (yes via
     `Fixture_ExactBucketDistribution_AcrossSixBucketRanges`)
   - Rebroadcast counters? (yes)
   - Only-saw eviction commit? (yes)
   - Snapshot id ordering? (yes)
   Anything missing for "fixture-injected observations match
   counter values exactly"?

8. **DI graph.** `MetricsSetupDiResolutionTests.W4_SingletonGraph_Resolves`
   asserts the four primary singletons. Does it cover
   `SourceVisibilityTracker` going through the config-aware
   factory (eviction window flows through)?

9. **Startup wiring.** `Startup.cs` calls
   `.AddMetricsZoneServices(configuration)` after the other
   zones. Is the order correct (does it need to come BEFORE
   `AddHostedTaskZoneServices` so the
   `TxObservationJournalWriter` ctor can resolve the optional
   tracker)? Or does the optional-default-null pattern make the
   order irrelevant?

10. **Coordination with W2 runners.** The journal-writer tail
    hook means `RecordInvObserved` (from
    `SourceObservationRecorder`) and tracker `RecordObservation`
    happen at different points in the W2 P2p runner. Is this
    divergence acceptable per the "InvObserved counts all invs;
    tracker only sees matched-and-journaled txs" semantics?

## Verdict format

End with:

```
Verdict: APPROVE | APPROVE WITH CHANGES | MAJOR REVISION REQUIRED
Critical findings: <count>
High findings: <count>
Medium findings: <count>
Low findings: <count>
Headline: <one sentence>
```

Then per-finding detail (`C1`, `H1`, `M1`, `L1` etc.):

- Severity
- Slice (or `wave-level`)
- Issue (1-2 sentences, with file path + line where applicable)
- Recommended fix (concrete)
