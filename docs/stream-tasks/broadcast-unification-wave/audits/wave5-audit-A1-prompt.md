# Wave 5 — Pre-execution audit A1 prompt

Audit target: `docs/stream-tasks/broadcast-unification-wave/` at
the W5-package-draft commit. Run async; revisions land in-wave.

---

You are auditing a wave-level plan in a multi-wave program. The
program is `consigliere-thin-node-observer-program` and this is
Wave 5 (`broadcast-unification-wave`). Read:

- `docs/stream-tasks/broadcast-unification-wave/master.md`
- `docs/stream-tasks/consigliere-thin-node-observer-program/master.md`
  (W5 row + §"Wave 5" section + handoff facts)
- `docs/stream-tasks/bsv-mempool-observer-wave/evidence/closeout.md`
  (closed wave; provides `TxRelayCoordinator.AnnounceAsync`)
- `docs/stream-tasks/observation-source-metrics-wave/evidence/closeout.md`
  (closed wave; provides broadcast-side metrics surfaces)

Cross-validate against the actual repo state:

- `src/Dxs.Consigliere/Services/IBroadcastService.cs`
- `src/Dxs.Consigliere/Services/Impl/BroadcastService.cs`
- `src/Dxs.Consigliere/WebSockets/WalletHub.cs` (legacy
  `Broadcast` + `BroadcastTracked` methods)
- `src/Dxs.Consigliere/Controllers/TransactionController.cs`
  (`POST /api/tx/broadcast/{raw}` legacy route)
- `src/Dxs.Bsv/IBroadcastProvider.cs`
- `src/Dxs.Consigliere/Services/Impl/BitcoindService.cs`
- `src/Dxs.Infrastructure/Bitails/{IBitailsRestApiClient,BitailsRestApiClient}.cs`
- `src/Dxs.Infrastructure/WoC/{IWhatsOnChainRestApiClient,WhatsOnChainRestApiClient}.cs`
- `tests/Dxs.Consigliere.Tests/Services/Impl/BroadcastServiceTests.cs`

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

1. **Scope coherence.** Does the slice ledger cover the program's
   W5 done-when ("grep shows no legacy broadcast HTTP-provider
   paths; real mainnet tx confirmed via single Broadcast
   method")? Anything cut that should land in-wave?

2. **Vnext breaking-change policy.** Master.md §"Product Decision"
   removes `Broadcast(string)` / `Broadcast(Transaction)` outright
   instead of `[Obsolete]`-shimming. Is the breaking change
   acceptable, or do downstream consumers (Bitails realtime
   runner, JungleBus, wallet-side JavaScript) need a
   compatibility window?

3. **`SubmitAsync` → `BroadcastAsync` rename.** The rename touches
   every caller of the existing P2P broadcast path. Is the rename
   worth the churn, or should the canonical name stay
   `SubmitAsync` and only the legacy methods be deleted? Pick one
   for the audit's recommendation.

4. **`IBroadcastProvider` removal vs `BitcoindService.SatoshisPerByte`.**
   The plan keeps `SatoshisPerByte` on `BitcoindService` while
   removing `Broadcast`. But `IBroadcastProvider` had both methods
   in its surface — removing the interface and keeping the method
   on the concrete class means callers that resolved via the
   interface for fee-only purposes now break. Are there any?

5. **No-HTTP-fallback safety.** Master.md Core Rule §2 says when
   no peers are Ready, `BroadcastAsync` still validates +
   persists, returning a `Validated` receipt with zero announce
   count. Is that "delayed broadcast" semantic clearly
   communicated to wallet clients via the receipt? Could a
   wallet display "broadcast succeeded" when in fact no peer
   received the inv?

6. **Raven `Broadcast` document model leftover.** The plan keeps
   the old `Broadcast` Raven document model in place for
   forensic queries but stops writing new instances. Is that
   acceptable, or should W5 actively remove the document type
   to avoid silent staleness?

7. **Test rework rigour.** S4 plans 4 new `BroadcastAsync_*`
   tests. Are they enough for the W5 done-when ("real mainnet
   tx confirmed via single Broadcast method end-to-end")? Should
   S6 add an end-to-end fixture via `MiniBsvServer` (W2 S5
   pattern)?

8. **Admin endpoint shape.** New `POST /api/tx/broadcast` accepts
   a JSON body `{ "rawHex": "..." }`. Should it be `{
   "rawTransaction": "..." }` for consistency with other admin
   endpoints? Or use the W2 `OutgoingTransactionStore` field
   naming (`RawHex`)? Pick the canonical name for the audit's
   verdict.

9. **`SatoshisPerByte` preservation.** Master.md §Out of scope
   keeps fee estimation as-is. But after `IBroadcastProvider` is
   removed, where does the fee-estimation interface live? Does
   `IBroadcastService.SatoshisPerByte()` need a different
   abstraction (e.g. `IFeeEstimator`)?

10. **Wallet-client migration documentation.** Master.md §"Out of
    scope" defers wallet-side migration to a `MIGRATION.md`
    snippet in the closeout. Is one snippet enough, or should the
    migration text be expanded into its own document /
    `docs/MIGRATION/W5.md`?

11. **DI graph impact.** `BroadcastService`'s ctor changes
    significantly (loses `IBitcoindService`, `IBitailsRestApiClient`,
    `IWhatsOnChainRestApiClient`). Will the DI graph still
    resolve when those clients have other consumers (fee /
    history)? Verify the audit asks for a DI regression test.

12. **Reverse dependency from `Dxs.Bsv` to `Dxs.Consigliere`.**
    Removing `IBroadcastProvider` from `Dxs.Bsv` is a downward
    cleanup. Anything else in `Dxs.Bsv` depend on broadcast
    semantics?

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
- Issue (1-2 sentences with file:line where applicable)
- Recommended fix (concrete)
