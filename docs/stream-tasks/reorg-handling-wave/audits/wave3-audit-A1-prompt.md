# Wave 3 — Pre-execution audit A1 prompt

Audit target: `docs/stream-tasks/reorg-handling-wave/` at commit
`92ead7d` (initial draft).

Run this prompt through Codex GPT-5. Paste the verdict back to
Claude when ready. Implementation may proceed in parallel —
findings will be folded in-wave per the program's stop-and-audit
rule.

---

You are auditing a wave-level plan in a multi-wave program. The
program is `consigliere-thin-node-observer-program` and this is
Wave 3 (`reorg-handling-wave`). Read:

- `docs/stream-tasks/reorg-handling-wave/master.md`
- `docs/stream-tasks/reorg-handling-wave/slices.md`
- `docs/stream-tasks/reorg-handling-wave/launch-prompt.md`
- `docs/stream-tasks/consigliere-thin-node-observer-program/master.md`
  (the W3 row at line ~352 + "Wave 3" section at line ~215)
- `docs/stream-tasks/bsv-headers-chain-wave/master.md`
  (closed wave; provides `IBlockHeaderStore`,
  `HeadersChain`, `ReorgEventDto`, `IWalletHub.OnReorg`,
  `BlockObservation`)
- `docs/stream-tasks/bsv-mempool-observer-wave/master.md`
  + `evidence/closeout.md` (closed wave; provides
  `TxRelayCoordinator.AnnounceAsync`, `BsvP2pHealth`,
  `PerSessionDispatcherRegistry`,
  `IRawTransactionPayloadStore`,
  `BsvP2pSetupDiResolutionTests` pattern)

Examine the actual repo state for cross-validation:

- `src/Dxs.Bsv/P2p/Chain/HeadersChain.cs` — confirm
  `ExtendResult.Fork(header, parentHeight)` shape + retention
  window
- `src/Dxs.Bsv/BitcoinMonitor/Models/BlockObservation.cs` —
  confirm record shape + missing fields
- `src/Dxs.Consigliere/BackgroundTasks/Blocks/BlockObservationJournalWriter.cs`
  — confirm only `AppendConnectedAsync` exists today
- `src/Dxs.Consigliere/Data/Transactions/TxLifecycleProjectionRebuilder.cs`
  L189-227 — confirm `ApplyBlockObservationAsync` already
  handles `BlockObservationEventType.Disconnected`
- `src/Dxs.Consigliere/WebSockets/{IWalletHub.cs,ReorgEventDto.cs}`
  — confirm frozen surface
- `src/Dxs.Consigliere/Services/P2p/{TxRelayCoordinator,BsvP2pHealth,PerSessionDispatcherRegistry}.cs`
  — confirm signatures W3 consumes

## Audit dimensions

Score each finding as **CRITICAL** (blocks merge),
**HIGH** (must address in-wave), **MEDIUM** (should address
in-wave, can defer with rationale), or **LOW** (cosmetic /
follow-up).

1. **Scope coherence.** Does the slice ledger cover everything
   the program's W3 row requires? Does it over-reach into other
   waves' scope? Anything that should be cut?
2. **Surface freezes respected.** Are all consumed surfaces
   actually frozen contracts? Any silently-introduced new hub
   events, `PeerSession` callbacks, or DTO fields?
3. **Slice dependency graph.** Are dependencies declarable in
   topological order? Any cycle or missing prereq?
4. **Cumulative-work vs height for fork comparison** (Core Rule
   §4). Is this approximation safe for the 200-header retention
   window? What can it miss?
5. **Idempotency design.** Does the dedupe fingerprint
   `block.disconnected:{blockHash}:{source}` cover all replay
   scenarios? What about a reorg that's first source-tagged
   `reorg` and later replayed by an operator-forced rescan
   (different source)?
6. **Block-body fetch safety.** 256 MiB cap acceptable for BSV
   in 2026? Merkle validation on every fetch — correct
   algorithm reference (BSV double-SHA256 pairwise)? What
   about a peer that times out repeatedly — risk of livelock
   if all peers are slow?
7. **Re-broadcast safety.** Coinbase exclusion is correct.
   Lookup order outgoing-then-payload — does this match the
   per-program "if raw bytes exist, re-announce" intent? Any
   risk of re-announcing a tx that's now invalid because of
   the reorg (e.g., its inputs were also orphaned and are now
   spent on the new chain)?
8. **Hub event ordering** (Core Rule §9). "Journal append
   before hub emit" — is this enforceable given that the
   journal-replay pipeline is async (journal appender stores
   the entry; the rebuilder consumes the journal on a
   subscription)? Is there a race where a client receives
   `OnReorg`, queries projections, and gets pre-reorg state?
9. **Deep-reorg degraded-state semantics.** When `IsDegraded =
   true` we fire `OnReorg(DegradedState=true, OrphanedHashes=
   [])` and stop. Is this enough? Should we attempt a full
   chain rescan, or is this strictly a W6 ops concern?
10. **Testing strategy.** Are the S6 scenarios sufficient to
    pin every Core Rule? Any obvious gap (e.g., reorg during
    block-body fetch — the active chain extends past the
    fork's height while we're fetching)?
11. **DI / wiring.** Is the regression-test pattern
    (`W3_SingletonGraph_Resolves`) sufficient? Anything missing
    that production startup would catch but the test wouldn't?
12. **Out-of-scope items.** Re-broadcast retries, per-tx
    `OnTransactionDeleted` event, HTTP-provider block fetch
    — are these correctly deferred to W6 / future, or should
    any of them land in W3?
13. **Doc fidelity.** Do `master.md`, `slices.md`, and the
    launch-prompt agree? Any divergence in slice list, owned
    paths, or done-when criteria?

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

Then the per-finding detail (`C1`, `H1`, `M1`, `L1` etc.) with:

- Severity
- Slice (which slice it touches; `wave-level` if multiple)
- Issue (1-2 sentences)
- Recommended fix (concrete; pin to specific file / line where
  possible)
