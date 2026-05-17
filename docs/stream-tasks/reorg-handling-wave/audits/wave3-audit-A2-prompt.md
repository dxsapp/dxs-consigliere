# Wave 3 — Post-execution audit A2 prompt

Audit target: implementation of `reorg-handling-wave` at commit
`1fab0ef` (wave closeout). Run after the A1 verdict has been
folded in (or in parallel — A1 covers the plan, A2 covers the
code).

Pass back through Codex GPT-5. Paste the verdict. In-wave
revisions land on the same branch.

---

You are auditing the executed implementation of Wave 3 of
`consigliere-thin-node-observer-program`. The pre-execution audit
prompt is at `audits/wave3-audit-A1-prompt.md`; you can reference
it for plan-level context but A2 focuses on the actual code.

Read the closeout: `docs/stream-tasks/reorg-handling-wave/evidence/closeout.md`.
Then read each shipped artefact and audit against the wave's
Core Rules (`master.md`).

## Files to audit

S0:
- `src/Dxs.Bsv/BitcoinMonitor/Models/BlockObservationSource.cs`
- `src/Dxs.Consigliere/BackgroundTasks/Blocks/BlockObservationJournalWriter.cs`
- `tests/Dxs.Consigliere.Tests/BackgroundTasks/Blocks/BlockObservationJournalWriterTests.cs`

S1:
- `src/Dxs.Bsv/P2p/Chain/{ReorgDetector,ReorgPlan,ICumulativeWorkComparer}.cs`
- `src/Dxs.Bsv/P2p/Chain/HeadersChain.cs`
  (read the two new public members `TryGetByWireHashHex` and
  `RetainedHeaderCount` — confirm they're safe additions to a
  W1-frozen surface)
- `tests/Dxs.Bsv.Tests/P2p/Chain/ReorgDetectorTests.cs`

S2 (DEFERRED): confirm the deferral rationale in closeout
"Scope deviations" §S2 is acceptable. Open A1 question #4 was
about this; A2 must explicitly accept or reject the deferral.

S3:
- `src/Dxs.Consigliere/Services/P2p/IReorgPipeline.cs`
- `src/Dxs.Consigliere/Services/P2p/ReorgPipeline.cs`
- `src/Dxs.Consigliere/Services/P2p/OrphanedTxIdReader.cs` (interface)
- `src/Dxs.Consigliere/Services/P2p/RavenOrphanedTxIdReader.cs`
- `src/Dxs.Consigliere/Services/P2p/BsvP2pHealth.cs`
  (read `LastDegradedReorgAt` + `MarkDegradedReorg` additions)
- `src/Dxs.Consigliere/Services/P2p/HeadersChainService.cs`
  (read the new optional `IReorgPipeline?` constructor parameter
  + the `case ExtendResult.Fork` branch hook)
- `tests/Dxs.Consigliere.Tests/P2p/Reorg/ReorgPipelineTests.cs`

S4:
- `src/Dxs.Consigliere/Services/P2p/IOrphanedTxRebroadcaster.cs`
- `src/Dxs.Consigliere/Services/P2p/OrphanedTxRebroadcaster.cs`
- `src/Dxs.Consigliere/Services/P2p/OrphanedTxRebroadcastRecorder.cs`
- `src/Dxs.Consigliere/Services/P2p/{ITxAnnouncer,IOutgoingRawLookup}.cs`
- `src/Dxs.Consigliere/Services/P2p/OutgoingTransactionStoreRawLookup.cs`
- `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs`
  (read the `: ITxAnnouncer` class-line edit — should be a
  signature-compatible adapter, not a behaviour change)
- `tests/Dxs.Consigliere.Tests/P2p/Reorg/OrphanedTxRebroadcasterTests.cs`

S5:
- `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs`
  (read the "Wave 3" registration block)
- `tests/Dxs.Consigliere.Tests/Setup/BsvP2pSetupDiResolutionTests.cs`
  (read `W3_SingletonGraph_Resolves` + the `BlockObservationJournalWriter`
  shim in `BuildProvider`)

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** / **LOW**.

1. **Code correctness vs each Core Rule** (master.md §"Core Rules"
   1-11). Specifically:
   - §1: no new contract surface — did we add anything to a
     frozen DTO / hub event / `PeerSession` callback?
   - §3: `IsDuplicate` propagation — does
     `AppendDisconnectedAsync` correctly return `!IsDuplicate`?
   - §4: height-as-cumulative-work approximation — safe within
     the 200-header window?
   - §6: coinbase exclusion — currently implicit via "no raw"
     skip. Is that defensible, or should we explicitly identify
     coinbases?
   - §7: idempotency by journal dedupe — does
     `RepeatPlan_SameOrphans_FingerprintsAreIdempotent` actually
     prove the projection state stays stable on replay?
   - §9: journal-before-hub ordering — does
     `OrderingPin_JournalAppendBeforeHubEmit` actually verify
     this? The current test only counts artefacts; consider if
     a stronger sequencing assertion is needed.
2. **Concurrency / thread safety.** The pipeline runs from
   `HeadersChainService.HandleHeadersAsync`. Multiple Fork
   events back-to-back — any state race in `BsvP2pHealth`,
   counters, or the appender?
3. **Error propagation.** `HeadersChainService` wraps the
   `_reorgPipeline.HandleForkObservedAsync` call in a try/catch
   that logs and continues. Is "log + continue" the right
   policy, or should a pipeline error block subsequent header
   processing? What if the projection query throws Raven errors?
4. **S2 deferral acceptance.** Read closeout §"Scope deviations"
   §S2. Is the rationale sufficient? Are there security threats
   the deferral introduces that the closeout misses?
5. **DI graph completeness.** Did `BsvP2pSetup` register every
   W3 type? Did `W3_SingletonGraph_Resolves` assert each one?
6. **Test coverage gaps.** Compare the S6 coverage matrix in
   closeout against the original slices.md §S6 scenarios. Are
   the "N/A — S2 deferred" and "implicit via no-raw" cases
   correctly classified?
7. **Naming + organisation.** Module names + namespace
   placement (e.g. `IReorgPipeline` in
   `Dxs.Consigliere.Services.P2p` rather than `.Reorg`). Is the
   convention consistent with W2?
8. **Doc fidelity.** Does the actual code match what
   `master.md` + `slices.md` claim? Any drift?
9. **Backwards compatibility.** Did the `TxRelayCoordinator :
   ITxAnnouncer` change break any existing call sites?

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

Then per-finding detail (`C1`, `H1`, `M1`, `L1` …) with:

- Severity
- Slice (or `wave-level`)
- Issue (1-2 sentences, with file path + line where applicable)
- Recommended fix (concrete)
