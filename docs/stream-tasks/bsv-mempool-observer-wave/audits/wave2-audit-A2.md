# Wave 2 Post-Execution Audit A2

Verdict: **MAJOR REVISION REQUIRED**

Scope reviewed: `2a81474..1bd7ee2` on `codex/consigliere-vnext`.

Build/test status:
- `dotnet build Dxs.Consigliere.sln -c Release`: 0 errors, warnings only.
- `dotnet test Dxs.Consigliere.sln -c Release --no-build`: 3 failures, all the pre-existing Raven embedded runtime failures in `TransactionStoreIntegrationTests`; `Dxs.Bsv.Tests` passed 201/201, `Dxs.Consigliere.Tests` passed 308, skipped 24, failed 3.

## Findings

### C1 - P2P relay services are not constructible from production DI

Where:
- `src/Dxs.Consigliere/Startup.cs:10-20`
- `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs:23-31`
- `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs:55-65`
- `src/Dxs.Consigliere/Services/P2p/BsvP2pHostedService.cs:54-98`
- `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs:72-82`

Evidence:
- `Startup.ConfigureServices` always includes `AddBsvP2pZoneServices(...)` in the application service graph.
- `AddBsvP2pZoneServices` registers `TxRelayCoordinator` and registers `OutgoingTransactionMonitor` as a hosted service at `BsvP2pSetup.cs:27-31`.
- `TxRelayCoordinator` requires `PeerManager` in its constructor at `TxRelayCoordinator.cs:55-59`.
- `BsvP2pHostedService` constructs `PeerManager` as a private runtime object and binds it into `BsvP2pHealth` at `BsvP2pHostedService.cs:54-98`; it is not registered as a service dependency for `TxRelayCoordinator`.
- `BroadcastServiceP2pWirer` is registered as a singleton at `BsvP2pSetup.cs:52`, but the only `Wire()` implementation is at `BsvP2pSetup.cs:78-82`; there is no call site in `src/Dxs.Consigliere`.

Why it matters:
- The S5 delivery note says the dispatcher-injected path is the production path. In the actual service graph, resolving the hosted `OutgoingTransactionMonitor` requires `TxRelayCoordinator`, which requires an unregistered `PeerManager`. That makes the P2P broadcast/relay side unwireable at host startup, independent of the dispatcher refactor.
- Even if the coordinator were made resolvable, the broadcast-service property wirer is inert unless something invokes `Wire()`.

Recommendation:
- Refactor `TxRelayCoordinator` to depend on `BsvP2pHealth` or another DI-registered session-provider abstraction instead of `PeerManager`, or register a single shared `PeerManager` factory before the hosted services resolve.
- Make the dispatcher registry mandatory on the production constructor path, with a separate explicit test-only constructor/factory for the legacy path if needed.
- Add a non-Raven DI validation test that builds the Consigliere service provider with `AddBsvP2pZoneServices` and resolves all hosted services.
- Either remove `BroadcastServiceP2pWirer` or invoke it from a hosted startup hook after dependencies are available.

### H1 - P2P txids are journaled in wire byte order, not canonical display order

Where:
- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs:198-212`
- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs:224-254`
- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs:278-318`
- `src/Dxs.Bsv/BitcoinHelpers.cs:18-26`
- `src/Dxs.Bsv/P2p/Chain/BlockHeaderHasher.cs:17-29`

Evidence:
- The runner derives `txid` from the inbound inventory hash with `Convert.ToHexString(item.Hash).ToLowerInvariant()` at `P2pMempoolIngestRunner.cs:200`.
- The same `item.Hash` bytes are used as the requested P2P inventory hash at `P2pMempoolIngestRunner.cs:224-254`, and tx-frame matching compares the raw double-SHA bytes directly to those bytes at `P2pMempoolIngestRunner.cs:236-240`. That confirms the value is the wire-order hash.
- The journal observation is appended with that same `txid` at `P2pMempoolIngestRunner.cs:313-318`.
- Existing canonical transaction ids reverse the double-SHA bytes before hex encoding in `BitcoinHelpers.GetTxId` at `BitcoinHelpers.cs:18-26`. Wave 1 made the same display-order distinction explicit for block hashes in `BlockHeaderHasher.ToDisplayHex` at `BlockHeaderHasher.cs:17-29`.

Why it matters:
- `TxObservation.TxId` must line up with Bitails, JungleBus, the projection rebuild tests, and W3/W4/W5 consumers. Recording P2P observations under reversed txids prevents `SeenBySources` from accumulating `p2p` with the same transaction observed by existing sources.
- W3 reorg handling would inherit split lifecycle records for the same transaction, which defeats the W2 handoff.

Recommendation:
- Keep wire-order bytes for `getdata` and tx-frame equality, but normalize the observation id with a helper equivalent to display order, for example `BitcoinHelpers.GetTxId(rawBytes)` after payload arrival or a dedicated `TxHash.ToDisplayHex(item.Hash)`.
- Add a fixture with known raw tx bytes where `inv.Hash == Hash.Sha256Sha256(raw)` and assert the persisted `TxObservation.TxId` equals `Transaction.Id` / display-order hex, not `Convert.ToHexString(inv.Hash)`.
- Audit `TxRelayCoordinator` for the same byte-order issue before relying on relay-back and getdata matching.

### H2 - The live mempool runner bypasses the S1 parser contract

Where:
- `src/Dxs.Bsv/P2p/Observer/TxScriptParser.cs:67-143`
- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs:322-341`
- `src/Dxs.Bsv/Models/Input.cs:22-40`
- `src/Dxs.Bsv/Script/Read/UnlockingScriptReader.cs:26-35`
- `tests/Dxs.Bsv.Tests/P2p/Observer/TxScriptParserTests.cs:131-143`

Evidence:
- S1 implements exact P2PKH output parsing, P2PKH input pubkey HASH160 derivation, and token parsing in `TxScriptParser.cs:67-143`.
- The production runner's `ToParsedTx` does not call `TxScriptParser`; it builds `ParsedTx` from `Transaction.Parse` convenience fields: `o.Address?.Hash160`, `o.TokenId`, and `inp.Address?.Hash160` at `P2pMempoolIngestRunner.cs:322-341`.
- The underlying input reader only derives an address for compressed 33-byte public keys at `UnlockingScriptReader.cs:30-35`, while S1 explicitly supports uncompressed 65-byte P2PKH pubkeys and tests that behavior at `TxScriptParserTests.cs:131-143`.

Why it matters:
- S1 is correct in isolation, but it is not the parsing boundary used by the live W2 P2P ingestion path. That means live observations do not necessarily follow the audited rules: exact 25-byte P2PKH output matching, compressed and uncompressed P2PKH input matching, and defensive token parsing through the S1 surface.
- This also weakens S7 coverage, because the fixture suite proves `TxScriptParser + WatchlistMatcher`, not the actual `P2pMempoolIngestRunner` conversion path.

Recommendation:
- Refactor `P2pMempoolIngestRunner.ToParsedTx` to materialize output scripts and input scriptSigs, then call `TxScriptParser.TryParseP2pkhOutput`, `TryParseP2pkhInputPubkey`, and `TryParseTokenId`.
- Add runner-level tests for compressed and uncompressed P2PKH inputs, exact P2PKH outputs, STAS/DSTAS token outputs, and malformed scripts that must not bubble exceptions.

### M1 - Rate-limited txids are deduped and not retried by the runner

Where:
- `src/Dxs.Bsv/P2p/Observer/MempoolWatcher.cs:80-102`
- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs:200-218`
- `tests/Dxs.Bsv.Tests/P2p/Observer/MempoolWatcherTests.cs:60-71`

Evidence:
- `MempoolWatcher.DecideFetch` inserts the txid into `_seenTxids` before checking the rate budget at `MempoolWatcher.cs:80-87`.
- On `RateLimited`, the runner records the counter and continues at `P2pMempoolIngestRunner.cs:204-208`; it does not call `Forget(txid)`.
- The test documents that a rate-limited txid can only retry if the runner calls `Forget` at `MempoolWatcherTests.cs:60-71`.

Why it matters:
- The implementation currently converts a transient one-second rate-limit miss into a dedupe miss until the dedupe TTL expires. That drops observations under load instead of applying the documented sliding-window retry behavior.

Recommendation:
- Either check the rate budget before inserting the txid into dedupe, or call `Forget(txid)` on the `RateLimited` branch so a later inv can retry once capacity is available.
- Add a runner-level test that sends a rate-limited inv, advances beyond the one-second window, sends the same inv again, and proves it can be fetched.

### M2 - S5 lacks an end-to-end runner test for the delivered P2P observation path

Where:
- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs:181-320`
- `tests/Dxs.Consigliere.Tests/P2p/PerSessionFrameDispatcherTests.cs:46-160`
- `tests/Shared/MiniBsvServer.cs:22-72`

Evidence:
- The runner owns the actual production flow: additive `OnInvReceived`, watcher decision, one-shot tx dispatcher subscription, `getdata`, parse/match, payload persistence, and journal append at `P2pMempoolIngestRunner.cs:181-320`.
- The S5 tests present in the tree exercise dispatcher fan-out and failure isolation at `PerSessionFrameDispatcherTests.cs:46-160`.
- There is no `P2pMempoolIngestRunner` test in `tests/Dxs.Consigliere.Tests/P2p/`, even though the existing `MiniBsvServer` test peer can drive real P2P frames.

Why it matters:
- The current test set did not catch H1 or H2 because it never exercises the live runner from `inv` through journal append. The most important W2 path is therefore covered mostly by component tests, not an integration test of the path that will unblock W3.

Recommendation:
- Add a non-Raven fake-journal/fake-payload-store runner test using `MiniBsvServer` that sends `inv(MSG_TX)`, observes `getdata`, replies with `tx`, and asserts canonical txid, match classification, source `"p2p"`, payload reference behavior, and dispatcher unsubscription.
- Add timeout and oversize-classification tests with controllable fake `PeerSession` or a test seam around the classifier.

## Per-Slice Closure Assessment

| Slice | Assessment |
| --- | --- |
| S0 - Journal contract extension | Closed. `TxObservationSource.P2p` is present, the source-neutral overload propagates duplicate status, source mismatch is guarded, and the projection tests are in place. |
| S1 - Parser | Partially closed. The parser itself is implemented and unit-tested, including uncompressed P2PKH input coverage, but the production runner bypasses it. See H2. |
| S2 - Matcher | Closed. The matcher uses an 8-byte prefix index with full-hash verification, returns `MatchResult.None.Instance` on no-hit, and uses concurrent dictionaries for add/remove. The implementation uses a concurrent dictionary rather than a literal `HashSet<ulong>`, but it preserves the required prefix/full-verify behavior. |
| S3 - Raven watchlist loader | Closed with residual runtime risk. It bulk-loads through `Advanced.StreamAsync`, uses the Raven Changes API rather than Subscription API, handles Put/Delete for address and token ids, and Raven-gated tests use `[SkippableFact]`. I did not find a new blocker here. |
| S4 - Watcher, payload-size policy, recorder | Partially closed. Dedupe uses `TryAdd`, payload-size config propagates to `PeerSessionConfig.InitialMaxRecvPayloadLength`, and counters exist for served/timeout/oversize. The rate-limited retry behavior is inconsistent with the runner. See M1. |
| S5 - Dispatcher, relay refactor, runner | Not closed. The dispatcher itself satisfies single-consumer fan-out and failure isolation, but production DI is unwireable, txids are not normalized for journal identity, and the runner lacks an end-to-end test. See C1, H1, and M2. |
| S6 - Source-tag regression pin | Closed. The tests pin `bitails`, `junglebus`, and `p2p` constant values without production edits to the existing Bitails/JungleBus runners. |
| S7 - Fixture suite and benchmark | Mostly closed. Fixture coverage spans the mandatory matcher scenarios, and `watchlist-bench.md` records the locked fields. The benchmark uses xunit + `Stopwatch` rather than BenchmarkDotNet, but the deviation is documented and the host is marked OBSERVATION rather than canonical reference-class PASS. |
| S8 - Operator live validation | Deferred by design. `live-validation.md` records the operator-run schema and closeout rule. The deferral is acceptable only after the S5 production-path blockers above are fixed, because the live validation would otherwise exercise a path whose txid and DI assumptions are already known to be wrong. |

## Closing Rationale

Wave 2 is not ready to hand off to Wave 3. The package compiles and most component tests are meaningful, but the production P2P path is not yet a reliable first-class observation source: DI cannot construct the relay side, P2P txids are journaled under the wrong byte order, and the runner does not consume the audited S1 parser. Fix those, add one real runner-level test from `inv` to journal append, and then Wave 3 can open with confidence that `Source = "p2p"` observations share the same tx identity and parsing semantics as the rest of the lifecycle pipeline.
