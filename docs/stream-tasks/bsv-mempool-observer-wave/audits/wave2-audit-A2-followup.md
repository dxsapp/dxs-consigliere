# Wave 2 Post-Execution Audit A2 Follow-Up

Verdict: **APPROVE WITH CHANGES**

Scope reviewed: A2 fixes in `16515cf` plus closeout docs in `848d7c4`.

Build/test status:
- `dotnet build Dxs.Consigliere.sln -c Release`: 0 errors, warnings only.
- `dotnet test Dxs.Consigliere.sln -c Release --no-build`: expected baseline result. `Dxs.Bsv.Tests` passed 207/207. `Dxs.Consigliere.Tests` passed 314, skipped 24, failed 3. The 3 failures are the pre-existing Raven embedded runtime failures in `TransactionStoreIntegrationTests` (`Could not find a matching runtime for '8.0.18+'`; available runtimes are 10.0.7 and 9.0.0).

## A2 Closure Table

| Finding | Status | Where addressed | Residual concern |
| --- | --- | --- | --- |
| C1 - P2P relay services are not constructible from production DI | **closed** | `TxRelayCoordinator` now depends on `BsvP2pHealth`, not `PeerManager`, at `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs:42-68`, and `AnnounceAsync` reads `_health.ActiveSessions` at `:76-83`. `BsvP2pSetup` registers `BroadcastServiceP2pWirer` plus hosted `BroadcastServiceP2pWirerHost` at `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs:51-56`; the host calls `Wire()` in `StartAsync` at `:94-104`. DI regression tests cover coordinator resolution, W2 singleton graph resolution, and wirer host registration at `tests/Dxs.Consigliere.Tests/Setup/BsvP2pSetupDiResolutionTests.cs:74-118`. | The DI tests prove the host resolves; they do not assert `Wire()` mutates a real `BroadcastService`. The code path is straightforward enough that this is not a blocker. |
| H1 - P2P txids are journaled in wire byte order | **closed** | `TxHashOrder` defines `WireToDisplayHex` and `DisplayHexToWire` at `src/Dxs.Bsv/P2p/Observer/TxHashOrder.cs:17-49`. The runner normalizes inbound `inv` hashes before dedupe and journal append at `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs:198-207`, preserves wire bytes for `getdata` at `:222-264`, and appends the display-order txid at `:323-328`. `TxRelayCoordinator` converts display txids back to wire order for outbound inv at `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs:91-94`, and normalizes inbound getdata/inv/reject hashes in dispatcher and legacy paths at `:155-160`, `:180-184`, `:207-211`, `:264-267`, `:283-285`, and `:305-309`. Tests pin inverse conversion, the BSV genesis coinbase txid, and `BitcoinHelpers.GetTxId` equivalence at `tests/Dxs.Bsv.Tests/P2p/Observer/TxHashOrderTests.cs:19-85`; the runner E2E asserts persisted `observation.TxId == BitcoinHelpers.GetTxId(raw)` at `tests/Dxs.Consigliere.Tests/P2p/P2pMempoolIngestRunnerTests.cs:174-236`. | None. |
| H2 - The live mempool runner bypasses the S1 parser contract | **closed** | `P2pMempoolIngestRunner.ToParsedTx` now materializes output and input scripts and calls `TxScriptParser.TryParseP2pkhOutput`, `TryParseTokenId`, and `TryParseP2pkhInputPubkey` at `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs:332-368`; coinbase inputs are skipped at `:360-364`. Existing parser tests include compressed and uncompressed P2PKH input coverage at `tests/Dxs.Bsv.Tests/P2p/Observer/TxScriptParserTests.cs:115-143`. | None. |
| M1 - Rate-limited txids are deduped and not retried by the runner | **closed** | The `RateLimited` branch now calls `_watcher.Forget(txid)` before recording the counter at `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs:212-218`; timeout/error paths still forget at `:224-228` and `:266-285`. The new runner test exercises a rate-limited `inv` and asserts the branch was hit at `tests/Dxs.Consigliere.Tests/P2p/P2pMempoolIngestRunnerTests.cs:286-350`. | The implementation is fixed. The test name says "AllowsRetryAfterWindowSlides", but the test stops after proving the rate-limited txid is not retained; see new-L1. |
| M2 - S5 lacks an end-to-end runner test | **closed** | `tests/Dxs.Consigliere.Tests/P2p/P2pMempoolIngestRunnerTests.cs:28-42` documents the non-Raven runner E2E harness. `InvToJournal_PersistsObservation_WithDisplayOrderTxid` drives `inv -> getdata -> tx -> journal append` and checks `Source = "p2p"`, display-order txid, payload reference, and matched count at `:174-236`. `UnmatchedTx_IncrementsUnmatchedCount_NotJournal` covers unmatched classification at `:238-284`. `RateLimited_Inv_AllowsRetryAfterWindowSlides` covers the rate-limit branch at `:286-350`. | None for the original E2E gap. |

## New Findings

### new-L1 - Rate-limit retry test name overclaims the behavior it asserts

Where:
- `tests/Dxs.Consigliere.Tests/P2p/P2pMempoolIngestRunnerTests.cs:286-350`

Evidence:
- The test is named `RateLimited_Inv_AllowsRetryAfterWindowSlides` at `:287`.
- It exhausts the one-per-second budget, sends one rate-limited inv, waits for `RecordRateLimited`, and asserts `watcher.DedupeSize < 5` at `:326-350`.
- It never waits for the one-second window to slide, sends the same inv again, observes a second `getdata`, or replies with the tx.

Why it matters:
- The production behavior is fixed because the runner calls `Forget(txid)` on rate-limit, but the regression test does not fully pin the promised retry behavior. A future change could keep the current test green while still failing to fetch the same txid after the window clears.

Recommendation:
- Extend the test to wait until the rate window clears, send the same `inv` again, assert a `getdata` is emitted for that txid, and optionally complete the tx reply path. Alternatively, rename the test to match the narrower assertion.

## Closing Rationale

The A2 blockers are closed. Wave 2 now has a constructible production DI path, canonical display-order txids for P2P observations and relay comparisons, the live runner consumes the S1 parser surface, rate-limited invs are no longer poisoned in dedupe, and the runner has non-Raven E2E coverage from `inv` to journal append.

Wave 3 (`reorg-handling-wave`) may open. The only new finding is a non-blocking test precision issue that should be cleaned up but does not invalidate the W2 handoff surface.
