# Wave 5 Closeout — `broadcast-unification-wave`

Status: implementation complete; awaiting wave-level Codex
post-execution audit A2. S0-S6 delivered; S7 (live mainnet)
operator-deferred per the W2 / W3 / W4 pattern.

## Delivery summary

| slice | commit | summary |
|---|---|---|
| S0 | `8a999d9` | `IBroadcastService.SubmitAsync` → `BroadcastAsync` rename + `IBroadcastServiceShapeTests` pin (interface shape: legacy `Broadcast(string)`/`Broadcast(Transaction)` absent; canonical `BroadcastAsync` present; return `Task<BroadcastReceipt>`; signature `(string rawHex, string clientConnectionId = null, CancellationToken ct = default)`) |
| S1 | this commit | `WalletHub.Broadcast(rawHex)` unified — replaces the legacy `Broadcast(string)` (returned `bool`) AND the transitional `BroadcastTracked`; returns `BroadcastReceiptDto`. `IWalletServer` interface signature updated. `TransactionController` exposes `POST /api/tx/broadcast` accepting `{ "rawHex": "..." }` body and returning `BroadcastReceiptDto`. Legacy `POST /api/tx/broadcast/{raw}` removed |
| S2 | this commit | `BroadcastService.Broadcast(string)` / `Broadcast(Transaction)` + `BroadcastToProviderAsync` + `BroadcastToNodeAsync` + `BroadcastToBitailsAsync` + `BroadcastToWhatsOnChainAsync` + `ResolveBroadcastTargetsAsync` + Polly retry policy + multi-attempt Raven document writer ALL DELETED. The collapsed ctor drops `IBitcoindService`, `IBitailsRestApiClient`, `IWhatsOnChainRestApiClient`, `IAdminProviderConfigService`, `IExternalChainProviderCatalog`, `IUtxoCache`, `INetworkProvider`, `IOptions<AppConfig>` dependencies (10 deps → 3). Caller `UnconfirmedTransactionsMonitor.Broadcast(...)` updated to use `BroadcastAsync` |
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
- [ ] `audits/wave5-audit-A2.md` — pending post-execution audit.
