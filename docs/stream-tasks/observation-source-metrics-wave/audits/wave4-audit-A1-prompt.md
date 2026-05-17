# Wave 4 — Pre-execution audit A1 prompt

Audit target: `docs/stream-tasks/observation-source-metrics-wave/`
at the W4-package-draft commit. Run async; revisions land in-wave.

---

You are auditing a wave-level plan in a multi-wave program. The
program is `consigliere-thin-node-observer-program` and this is
Wave 4 (`observation-source-metrics-wave`). Read:

- `docs/stream-tasks/observation-source-metrics-wave/master.md`
- `docs/stream-tasks/observation-source-metrics-wave/launch-prompt.md`
- `docs/stream-tasks/consigliere-thin-node-observer-program/master.md`
  (W4 row + §"Wave 4" section + handoff facts)
- `docs/stream-tasks/bsv-mempool-observer-wave/evidence/closeout.md`
  (closed wave; provides `SourceObservationRecorder`)
- `docs/stream-tasks/reorg-handling-wave/evidence/closeout.md`
  (closed wave; provides `OrphanedTxRebroadcastRecorder` +
  `BsvP2pHealth.LastDegradedReorgAt`)

Examine the actual repo state to cross-validate the recorder
surfaces this wave consumes:

- `src/Dxs.Consigliere/Services/P2p/SourceObservationRecorder.cs`
- `src/Dxs.Consigliere/Services/P2p/OrphanedTxRebroadcastRecorder.cs`
- `src/Dxs.Consigliere/Services/P2p/BsvP2pHealth.cs`
- `src/Dxs.Consigliere/BackgroundTasks/Realtime/BitailsRealtimeIngestRunner.cs`
  (the runner-side observation site for Bitails)
- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs`
  (the runner-side observation site for P2p)
- The JungleBus realtime runner (path TBD on survey)
- `src/Dxs.Consigliere/BackgroundTasks/TxObservationJournalWriter.cs`
  (the journal-writer tail — potential single chokepoint for
  the tracker hook)

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** / **LOW**.

1. **Scope coherence.** Does the slice ledger cover the program's
   W4 done-when? "metrics recorder live; admin API + SPA page
   show three sources with non-zero counters in fixture run".
   Anything cut that should land in-wave?
2. **Surface freezes respected.** Are the consumed recorders /
   `BsvP2pHealth` actually stable, or does the design require new
   methods on them? Any silent contract amendments?
3. **Tracker hook location.** Master §"Open scope questions" #1.
   Audit must pick: individual runner sites vs journal-writer
   tail. Trade-offs: tail-side = single chokepoint, but might miss
   observations that never reach the journal (parse errors,
   unmatched). Per-runner = more wiring but covers the full
   observation surface.
4. **Bucket boundary correctness.** Default 6 buckets `<10ms,
   10-50ms, 50-200ms, 200ms-1s, 1s-5s, >5s`. Wide enough at the
   top, fine enough at the bottom? Any operator dashboard needing
   different cut-points?
5. **Tracker eviction semantics.** "Only-saw" counts commit at
   eviction. Race condition: a slow source observes a tx 4 min
   59 s after the first source; eviction loop runs every 30 s.
   Possible to commit only-saw before the late observation lands?
   Buffered increment pattern needed.
6. **Snapshot atomicity.** Aggregator calls `Collect` then writes
   the snapshot. During the read, recorders are incrementing.
   Is the snapshot expected to be a self-consistent point-in-
   time, or eventually-consistent across counters? (The latter is
   acceptable for a 30 s sampling interval but should be
   explicit.)
7. **Retention enforcement.** Eviction by doc-id (timestamp).
   Race: aggregator writes Snapshot at t+30s, then deletes the
   oldest. If two aggregator instances run (during a deployment
   crossover), could the wrong snapshot get deleted? Singleton
   guarantee from DI is the answer — confirm or escalate.
8. **Fixture validation rigour.** "Counter values match exactly"
   — strong requirement. Does the slice ledger include enough
   test scenarios to back this up?
9. **SPA scope cut.** Master §"Out of scope" allows S8 deferral.
   Does the program's done-when actually require the SPA, or can
   the admin REST endpoint alone satisfy ops?
10. **DI graph completeness.** S7 registration list — does it
    cover all the new singletons?
11. **Restart semantics.** Tracker is in-process; restart loses
    the active-tx → first-seen map. Is the 5-min eviction window
    short enough that this doesn't matter, or is there a
    persistence requirement?
12. **Lag clock source.** Master §Core Rule 5 — "Lag is observed
    - first-seen". Both timestamps come from
    `DateTimeOffset.UtcNow` in the runner. Clock skew across
    machines doesn't apply (single-process). Acceptable.

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

Then per-finding detail (`C1`, `H1`, `M1`, `L1` etc.) with:

- Severity
- Slice (or `wave-level`)
- Issue (1-2 sentences, with file path + line where applicable)
- Recommended fix (concrete)
