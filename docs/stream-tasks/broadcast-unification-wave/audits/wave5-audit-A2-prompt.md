# Wave 5 — Post-execution audit A2 prompt

Audit target: implementation of `broadcast-unification-wave` at
the wave-closeout commit. Run after the A1 verdict has landed
(or in parallel — A1 covers the plan, A2 covers the code).

---

You are auditing the executed implementation of Wave 5 of
`consigliere-thin-node-observer-program`. Read the closeout first:
`docs/stream-tasks/broadcast-unification-wave/evidence/closeout.md`.
Then audit each shipped artefact against the wave's Core Rules in
`docs/stream-tasks/broadcast-unification-wave/master.md`.

## Files to audit

**S0 — interface rename**
- `src/Dxs.Consigliere/Services/IBroadcastService.cs`
- `src/Dxs.Consigliere/Services/Impl/BroadcastService.cs`
- `src/Dxs.Consigliere/WebSockets/WalletHub.cs` (the
  `BroadcastAsync` call site)
- `tests/Dxs.Consigliere.Tests/Broadcast/IBroadcastServiceShapeTests.cs`

**S1 — entrypoint replacement**
- `src/Dxs.Consigliere/Controllers/TransactionController.cs` (new
  `POST /api/tx/broadcast` with JSON body + `BroadcastTxRequest`
  record)
- `src/Dxs.Consigliere/WebSockets/WalletHub.cs` (unified
  `Broadcast(rawHex)` returning `BroadcastReceiptDto`)
- `src/Dxs.Consigliere/WebSockets/IWalletServer.cs` (interface
  signature change)

**S2 — BroadcastService legacy deletion**
- `src/Dxs.Consigliere/Services/Impl/BroadcastService.cs` (ctor
  reduced from 10 deps to 3; all multi-provider helpers + Polly
  retry + Raven multi-attempt writer deleted)
- `src/Dxs.Consigliere/BackgroundTasks/UnconfirmedTransactionsMonitor.cs`
  (caller migrated to `BroadcastAsync`)

**S3 — IBroadcastProvider → IFeeRateProvider**
- `src/Dxs.Bsv/IFeeRateProvider.cs` (new)
- `src/Dxs.Bsv/IBroadcastProvider.cs` (DELETED — verify file
  doesn't exist)
- `src/Dxs.Consigliere/Services/IBitcoindService.cs` (now extends
  `IFeeRateProvider`)
- `src/Dxs.Consigliere/Services/Impl/BitcoindService.cs` (Broadcast
  method deleted)
- `src/Dxs.Bsv/Tokens/Stas/StasProtocolTransactionFactory.cs` (ctor
  param + usage migrated)
- `src/Dxs.Consigliere/Setup/CorePlatformSetup.cs` (DI forwarder
  registration updated)

**S4 — HTTP client cleanup**
- `src/Dxs.Infrastructure/Bitails/IBitailsRestApiClient.cs` (no
  Broadcast method)
- `src/Dxs.Infrastructure/Bitails/BitailsRestApiClient.cs` (impl
  removed)
- `src/Dxs.Infrastructure/WoC/IWhatsOnChainRestApiClient.cs` (no
  BroadcastAsync method)
- `src/Dxs.Infrastructure/WoC/WhatsOnChainRestApiClient.cs` (impl
  removed)

**S5 — legacy test deletion**
- `tests/Dxs.Consigliere.Tests/Services/Impl/BroadcastServiceTests.cs`
  (placeholder file documenting the deletion)

**S6 — grep regression suite**
- `tests/Dxs.Consigliere.Tests/Broadcast/BroadcastUnificationGrepTests.cs`
  (7 reflection-based tests)

**S7 — DEFERRED**. Confirm the operator-deferred rationale in
closeout is acceptable.

## Audit dimensions

Score each finding **CRITICAL** / **HIGH** / **MEDIUM** /
**LOW**.

1. **Each Core Rule** (master.md §"Core Rules" 1-7):
   - §1: no new contract surface. `BroadcastReceiptDto` shape
     unchanged. Confirm `IBroadcastService.BroadcastAsync` is a
     rename, not a new method.
   - §2: no HTTP fallback. Confirm `BroadcastService` no longer
     has any HTTP-client dependency in its ctor.
   - §3: all broadcast paths through one method. Grep
     `\.BroadcastAsync\(` / `\.Broadcast\(` and confirm only the
     unified call sites remain.
   - §4: vnext breaking-change policy. Confirm no `[Obsolete]`
     attributes / compatibility shims.
   - §5: frozen DTOs (`BroadcastReceiptDto`, `BroadcastStateEvent`)
     shapes unchanged.
   - §6: W2 + W4 prereq dependencies — confirm no W2 / W4
     surfaces were touched.
   - §7: stop-and-audit per wave. S0 slice-level audit gate was
     followed.

2. **`IBroadcastProvider` rename.** S3 renamed it to
   `IFeeRateProvider` rather than splitting fee-estimation into
   a separate concern. Was that the right call? Should
   `IFeeRateProvider` live in `Dxs.Bsv` (where the consumers are)
   or in `Dxs.Consigliere.Services` (where the implementations
   are)?

3. **`BitcoindService.Broadcast` deletion.** Confirm no caller
   anywhere in the codebase still references it. Grep
   `bitcoindService.Broadcast` / `IBitcoindService.Broadcast` —
   should return zero matches outside the dleeted method.

4. **`UnconfirmedTransactionsMonitor.Broadcast` migration.** The
   caller was migrated to `BroadcastAsync`. The state check now
   uses `OutgoingTxState.{Validated, Dispatching, PeerAcked,
   PeerRelayed}` as the "success" set. Is that semantically
   correct, or did the migration drop a state that should count
   as success?

5. **`POST /api/tx/broadcast` JSON body shape.** Master.md
   audit-prompt question #8 was about field naming
   (`rawHex` vs `rawTransaction`). S1 went with `rawHex`. Is
   that the canonical name aligned with `OutgoingTransaction.RawHex`?
   Backward-compat impact?

6. **`BroadcastTxRequest` record placement.** Defined inline in
   `TransactionController.cs`. Should it live in a dedicated
   `Dto/Requests/` file alongside other request DTOs?

7. **Grep regression rigor.** `BroadcastUnificationGrepTests`
   uses reflection on 7 audited types. Does it cover every legacy
   broadcast surface? Specifically:
   - Is `Dxs.Bsv.IBroadcastProvider` actually absent (the
     `bsvAsm.GetType(...)` lookup)?
   - Does any third interface still expose a `Broadcast` method
     that the test misses?

8. **Test isolation: parallel-load flakies.** The closeout flags
   `RateLimited_Inv_ForgetsTxid_NotRetainedInDedupe` as a flaky
   test that passes in isolation. Was this regression introduced
   by W5, or pre-existing?

9. **Migration snippet completeness.** Closeout's migration
   snippet covers REST + SignalR but doesn't mention the
   `WalletHub.BroadcastTracked` rename (it was deleted). Should
   the snippet be expanded?

10. **DI graph completeness.** `BroadcastService` now takes only
    `IBitcoindService`, `IDocumentStore`, `ILogger`. Does the
    DI graph still resolve in production? Verify
    `BsvP2pSetupDiResolutionTests` (or equivalent) still passes.

11. **Backwards compat: existing tests using removed types.** Did
    deleting `BroadcastServiceTests` legacy methods break any
    other test file that referenced them transitively?

12. **`BroadcastService.SatoshisPerByte` still works.** The
    method delegates to `bitcoindService.SatoshisPerByte()`. With
    bitcoind broadcast removed, is this fee-rate query still
    meaningful — does it return a sensible default or hit a
    deleted RPC endpoint?

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

If APPROVE or APPROVE WITH CHANGES, Wave 5 closes (modulo folded
changes). Otherwise expect another iteration.
