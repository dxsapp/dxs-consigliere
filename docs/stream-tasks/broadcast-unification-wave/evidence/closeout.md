# Wave 5 Closeout — `broadcast-unification-wave`

Status: **CLOSED**. Wave-level Codex audit chain completed —
A2 (APPROVE WITH CHANGES — 1 H, 2 M, 5 L; revision folded) →
A2-followup (APPROVE WITH CHANGES — 1 PARTIAL + 2 LOW; folded) →
wave APPROVED. S0-S6 delivered; S7 (live mainnet)
operator-deferred per the W2 / W3 / W4 pattern.

## Delivery summary

| slice | commit | summary |
|---|---|---|
| S0 | `8a999d9` | `IBroadcastService.SubmitAsync` → `BroadcastAsync` rename + `IBroadcastServiceShapeTests` pin (interface shape: legacy `Broadcast(string)`/`Broadcast(Transaction)` absent; canonical `BroadcastAsync` present; return `Task<BroadcastReceipt>`; signature `(string rawHex, string clientConnectionId = null, CancellationToken ct = default)`) |
| S1 | this commit | `WalletHub.Broadcast(rawHex)` unified — replaces the legacy `Broadcast(string)` (returned `bool`) AND the transitional `BroadcastTracked`; returns `BroadcastReceiptDto`. `IWalletServer` interface signature updated. `TransactionController` exposes `POST /api/tx/broadcast` accepting `{ "rawHex": "..." }` body and returning `BroadcastReceiptDto`. Legacy `POST /api/tx/broadcast/{raw}` removed |
| S2 | this commit | `BroadcastService.Broadcast(string)` / `Broadcast(Transaction)` + `BroadcastToProviderAsync` + `BroadcastToNodeAsync` + `BroadcastToBitailsAsync` + `BroadcastToWhatsOnChainAsync` + `ResolveBroadcastTargetsAsync` + Polly retry policy + multi-attempt Raven document writer ALL DELETED. The collapsed ctor drops `IBitailsRestApiClient`, `IWhatsOnChainRestApiClient`, `IAdminProviderConfigService`, `IExternalChainProviderCatalog`, `IUtxoCache`, `INetworkProvider`, `IOptions<AppConfig>` dependencies (post A2 L1 also drops `IDocumentStore`; final 10 deps → 2: `IBitcoindService` + `ILogger`; A2 L5 corrects original closeout claim that `IBitcoindService` was dropped — it stays as the fee-rate source). Caller `UnconfirmedTransactionsMonitor.Broadcast(...)` updated to use `BroadcastAsync` |
| S3 | this commit | `IBroadcastProvider` (in `Dxs.Bsv`) renamed to `IFeeRateProvider` (broadcast moved to unified P2P path; fee-rate query is the surviving concern). `BitcoindService.Broadcast(string)` DELETED. `IBitcoindService` now extends `IFeeRateProvider`. `StasProtocolTransactionFactory` ctor param renamed `broadcastProvider` → `feeRateProvider`. `CorePlatformSetup` DI registration updated |
| S4 | this commit | `IBitailsRestApiClient.Broadcast(...)` + impl DELETED. `IWhatsOnChainRestApiClient.BroadcastAsync(...)` + impl DELETED. Both clients retain non-broadcast endpoints (address, tx, balance, UTXO, block, token data) |
| S5 | this commit | `BroadcastServiceTests` legacy multi-provider tests DELETED. New regression coverage lives in `IBroadcastServiceShapeTests` (S0) + `BroadcastUnificationGrepTests` (S6) + the existing W2 Gate-3 P2P announce / lifecycle test suite |
| S6 | this commit | `BroadcastUnificationGrepTests` (7 tests): reflection-based pins for "no Broadcast overloads on `IBroadcastService` / `BroadcastService`; no `IBroadcastProvider` type; `IFeeRateProvider` has no Broadcast method; `IBitcoindService` / `IBitailsRestApiClient` / `IWhatsOnChainRestApiClient` have no Broadcast method". Pins the W5 done-when at the build level |
| S7 | deferred | Operator-driven live mainnet validation |

## Test counts

- `Dxs.Bsv.Tests`: 220/220 (no Bsv-side broadcast tests; the
  `IBroadcastProvider` removal didn't trigger any test failures
  because no tests referenced it).
- `Dxs.Consigliere.Tests`: 424 passed (was 418 at W4 close;
  +6 from W5: 3 S0 shape pins + 7 S6 grep regressions = +10
  new tests, minus 4 deleted legacy `Broadcast_*` tests = +6
  net).
- 24 explicit Skipped + 3 pre-existing baseline Raven-runtime
  failures (unchanged from W4 close).
- 1 parallel-load-sensitive flaky test
  (`RateLimited_Inv_ForgetsTxid_NotRetainedInDedupe`) passes in
  isolation — pre-existing, unrelated to W5.

## Scope deviations

### S7 — live-mainnet deferred

The W5 done-when says "real mainnet tx confirmed via single
Broadcast method end-to-end". This requires an operator to submit
a real BSV mainnet tx via the new unified endpoint + observe
block-inclusion. Deferred per the W2 S8 / W3 S7 / W4 S8 pattern;
recorded as residual in this closeout.

### `BroadcastService.Broadcast(string)` / `Broadcast(Transaction)` deleted, not `[Obsolete]`-shimmed

Per the master.md product decision + vnext repo policy. External
wallet clients calling the legacy REST `POST /api/tx/broadcast/{raw}`
or SignalR `wallethub.invoke('Broadcast', rawHex)` returning bool
will break. Migration snippet for downstream:

```diff
- // OLD (legacy multi-provider HTTP path, returned Raven `Broadcast` doc):
- POST /api/tx/broadcast/${rawHex}        → { Success: bool, Attempts: [...] }
- wallethub.invoke('Broadcast', rawHex)   → bool

+ // NEW (Wave 5 unified P2P path):
+ POST /api/tx/broadcast  body: { "rawHex": "..." }  → BroadcastReceiptDto
+ wallethub.invoke('Broadcast', rawHex)               → BroadcastReceiptDto
+
+ // BroadcastReceiptDto shape (frozen by W1 S0.8):
+ //   { TxId, State, CreatedAtMs, FailReason? }
+ // Subsequent state transitions stream via SignalR OnBroadcastStateChanged.
```

### `IBroadcastProvider` renamed (not just deleted)

The interface combined fee-rate + broadcast. Broadcast went to
the unified P2P path; fee-rate stayed (consumed by
`StasProtocolTransactionFactory`). Renamed to `IFeeRateProvider`
to reflect the narrowed concern. The old type name
`Dxs.Bsv.IBroadcastProvider` is asserted absent by the grep
regression test.

### Raven `Broadcast` document model

The legacy multi-attempt `Broadcast` Raven document is no longer
written (the only writer was the deleted `BroadcastService.Broadcast`
helper). Historical docs remain in the DB for forensic queries;
admin can read them. A future ops wave can choose to archive /
delete the documents (and the model type) if storage hygiene
becomes a concern.

## Open follow-ups (carried for a future wave; not blocking)

- **Live mainnet validation (S7)** — operator-deferred.
- **Raven `Broadcast` document archival** — historical-only, no
  current writer.
- **Flaky parallel-load tests** — `RateLimited_Inv_ForgetsTxid_NotRetainedInDedupe`
  + the BSV-side `PeerManager_FailureRecordsNegativeCooldown` pass
  in isolation; not introduced by W5.

## Audit trail

- `audits/wave5-audit-A1-prompt.md` — pre-execution prompt staged
  for user-driven Codex pass (parallel to implementation).
- `audits/wave5-audit-A2.md` — pending post-execution audit.

## Ready-for-audit checklist

- [x] All slices delivered (S7 operator-deferred with rationale).
- [x] `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- [x] `dotnet test` returns no new failures vs the pre-W5 baseline.
- [x] Grep regression test green:
      `BroadcastUnificationGrepTests` (7 tests).
- [x] Interface shape pin test green:
      `IBroadcastServiceShapeTests` (3 tests).
- [x] All call sites of the legacy `Broadcast` /
      `IBroadcastProvider` migrated.
- [x] `audits/wave5-audit-A2.md` — APPROVE WITH CHANGES (1 H,
      2 M, 5 L). Revision committed; see "A2 revision summary"
      below. Awaiting A2-followup.

## A2 revision summary (this commit)

Codex A2 verdict: APPROVE WITH CHANGES — 1 HIGH (Core Rule §2
violation in the no-peer path), 2 MEDIUM (test-evidence gaps),
5 LOW (cleanup / docs). All 8 folded.

### H1 — `BroadcastAsync` no-ready-peer stays Dispatching, not Failed

**Before:** the background dispatch task set
`OutgoingTxState.Failed` when `TxRelayCoordinator.AnnounceAsync`
returned 0 served peers. `Failed` is in
`OutgoingTxStates.IsTerminal`, so
`OutgoingTransactionStore.GetNonTerminalAsync` excluded it — the
lifecycle monitor would never retry the tx when peers reconnect,
violating Core Rule §2 ("no-ready-peer broadcasts succeed at
validation/persistence and are picked up later").

**After:** the no-served-peer branch keeps `tx.State =
Dispatching`, records `LastError = "No peers available at
dispatch time; awaiting peer reconnect"` for operator
visibility, and saves. Pinned by
`BroadcastServiceBehaviorTests.BroadcastAsync_NoReadyPeer_StaysDispatching_NotFailed`.

### M1 — Behavioral test coverage

Two minimal abstractions introduced for testability:

- `IBroadcastPolicyValidator` wrapping the subset of
  `TxPolicyValidator` operations the service uses.
  `TxPolicyValidator` implements it.
- `IOutgoingTransactionRepository` wrapping the subset of
  `OutgoingTransactionStore` operations.
  `OutgoingTransactionStore` implements it.

`BroadcastService`'s internal property slots re-typed to the
interfaces (+ `ITxAnnouncer` from W3 A2). The W2 wirer host is
unchanged externally; it passes the concrete classes via their
interfaces.

5 new behavioral tests in `BroadcastServiceBehaviorTests`:
no-P2p-subsystem, policy-invalid, valid-persists, announce
observed, **no-ready-peer stays non-terminal** (H1 pin).

### M2 — Production DI graph test

`BroadcastServiceProductionDiTests` (2 tests) build a service
provider with the production `RealtimeSetup.AddRealtimeZoneServices`
+ `BsvP2pSetup.AddBsvP2pZoneServices` registrations (vs the
pre-existing `BsvP2pSetupDiResolutionTests` which registered a
mock and never exercised the real ctor). Asserts:

1. `IBroadcastService` resolves as the concrete `BroadcastService`.
2. Running `BroadcastServiceP2pWirer.Wire()` populates the
   property-injected slots (`PolicyValidator`, `OutgoingStore`,
   `Announcer`).

The test resolves the wirer DIRECTLY rather than enumerating
`IHostedService` because the W2 `OutgoingTransactionMonitor`
has a static duplicate-instance guard that fires across
parallel xunit fixtures (W2 closeout known constraint).

### L1 — `IDocumentStore` ctor dep removed

`BroadcastService` ctor reduced to 2 dependencies:
`IBitcoindService` (fee-rate forwarder) + `ILogger`. The unused
`Raven.Client.Documents` import was also removed.

### L2 — `BroadcastResponseDto` deleted

`src/Dxs.Infrastructure/Bitails/Dto/BroadcastResponseDto.cs`
removed (had no remaining references after S4 deleted
`IBitailsRestApiClient.Broadcast`).

### L3 — `OutgoingTxStates.IsActiveOrAccepted` helper

The `UnconfirmedTransactionsMonitor` re-broadcast success
check was previously hard-coded to 4 states, missing
duplicate-submission paths that return existing receipts at
`MempoolSeen`/`Mined`/`Confirmed`. Centralized as
`OutgoingTxStates.IsActiveOrAccepted(state)` covering all 7
post-Validated active states.

### L4 — `BroadcastTxRequest` moved to `Dto/Requests/`

Inline record in `TransactionController.cs` moved to
`src/Dxs.Consigliere/Dto/Requests/BroadcastTxRequest.cs` per
repo convention.

### L5 — Closeout S2 row corrected

The original S2 row claimed the ctor "drops `IBitcoindService`"
— wrong, since `SatoshisPerByte()` still forwards to it. The
S2 table row above is now accurate: ctor went 10 deps → 2
post-A2-L1; `IBitcoindService` + `ILogger` are the survivors.

### Final test counts after A2 revision

- `Dxs.Bsv.Tests` 220/220 (unchanged).
- `Dxs.Consigliere.Tests` 432 passed (was 424 pre-A2 revision,
  +8: 5 M1 + 2 M2 + 1 ripple). 24 explicit Skipped + 3
  pre-existing baseline Raven-runtime failures (unchanged).

## A2-followup revision summary (final — this commit)

A2-followup verdict: **APPROVE WITH CHANGES** — 7 closed,
1 PARTIAL (M1 missing duplicate-receipt test), 0 regressed,
2 new LOW (N1 doc drift, N2 nullable warnings). All 4 items
folded.

### M1 (PARTIAL → CLOSED) — duplicate-receipt test added

The A2 revision shipped 5 behavioral tests but the
`FakePolicyValidator.IsDuplicateResult` was hard-coded to
`false`, so the duplicate branch in
`BroadcastService.BroadcastAsync` never executed under test.
The validator fake now has overridable
`IsDuplicateOverride` / `ExtractTxIdOverride` callbacks, and
the new
`BroadcastAsync_DuplicateSubmission_ReturnsExistingReceipt_NoPersistOrAnnounce`
test:
- Seeds `FakeOutgoingRepo.PreExisting` with an existing tx
  document at `OutgoingTxState.PeerRelayed`.
- Configures the validator to flag the next submission as
  duplicate + extract the seeded txid.
- Calls `BroadcastAsync` and asserts the returned receipt
  mirrors the existing doc's `TxId` / `State` / `CreatedAtMs`,
  AND that the repo's `SaveAsync` was NOT called, AND the
  announcer was NOT called (duplicate path is read-only).

### M2 (CLOSED WITH NOTE → CLOSED) — class comment corrected

The A2 revision dropped the `[Collection]` attribute when the
direct-wirer-resolve change removed the static-guard race,
but left a class-level XML comment claiming `[Collection]` was
present. The comment now accurately describes the
direct-resolve approach + explicitly notes "no `[Collection]`
attribute needed".

### N1 (LOW new → CLOSED) — wave docs ctor drift

`master.md` §S2 + parent program ledger row #5 still carried
pre-A2 claims ("ctor drops `IBitcoindService`", "ctor 10 → 3
deps", "A2 pending"). Updated to reflect the post-A2 reality:
ctor 10 → 2 deps with `IBitcoindService` + `ILogger`
surviving; property-injected slots interface-typed
(`IBroadcastPolicyValidator` + `IOutgoingTransactionRepository`
+ `ITxAnnouncer`); A2-followup status reflected.

### N2 (LOW new → CLOSED) — test nullable annotations

`BroadcastServiceBehaviorTests.FakeOutgoingRepo`:
- `LastSaved` is now `OutgoingTransaction?` (was non-nullable;
  null until first Save landed).
- `GetOrNullAsync` returns `Task<OutgoingTransaction?>`
  matching the `IOutgoingTransactionRepository` interface
  signature.

The `PreExisting` slot (added for M1 duplicate test) is
nullable by construction, so the `Task.FromResult(PreExisting)`
return infers `Task<OutgoingTransaction?>` cleanly.

### Wave 5 close

All slices closed (S2 / S7 deferred with documented rationale).
Audit chain: A2 → A2-followup APPROVE WITH CHANGES (closed).
Wave 6 (`production-ops-wave`) may now open per the program
dependency graph (depends on W2; W5 recommended).

Open follow-ups (carried for a future wave; not blocking):

- **S7 live-mainnet validation** — operator-driven.
- **Raven `Broadcast` document archival** — historical-only,
  no current writer; storage-hygiene concern.
- **Flaky parallel-load tests** — pre-existing
  `RateLimited_Inv_*` + `PeerManager_FailureRecordsNegativeCooldown`;
  not introduced by W5.

### Final-final test counts

- `Dxs.Bsv.Tests` 220/220 (unchanged).
- `Dxs.Consigliere.Tests` 433 passed (was 432 pre-A2-followup,
  +1 from the new duplicate-receipt test) + 24 explicit
  Skipped + 3 pre-existing baseline Raven-runtime failures
  (unchanged).
- Broadcast-filtered: 35/35 passed (was 27 pre-A2 revision,
  +8 from A2 + A2-followup).
