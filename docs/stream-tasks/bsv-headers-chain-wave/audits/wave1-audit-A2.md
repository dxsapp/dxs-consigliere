# Wave 1 Audit A2 — Post-Execution Review

Verdict: **MAJOR REVISION REQUIRED**

Audited range: `c7b1428..557f8ad` on `codex/consigliere-vnext`.

## Findings

1. **H1 — W1 does not currently produce an authoritative mainnet header tip by default**

   **Where:** `docs/stream-tasks/bsv-headers-chain-wave/master.md:18-20`, `docs/stream-tasks/bsv-headers-chain-wave/master.md:72-74`; `docs/stream-tasks/bsv-headers-chain-wave/evidence/closeout.md:123-126`, `docs/stream-tasks/bsv-headers-chain-wave/evidence/closeout.md:138-141`; `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs:31-37`; `src/Dxs.Consigliere/Services/P2p/HeadersChainBootstrapper.cs:27-32`, `src/Dxs.Consigliere/Services/P2p/HeadersChainBootstrapper.cs:124-127`; `src/Dxs.Consigliere/Services/P2p/HeadersChainService.cs:102-113`, `src/Dxs.Consigliere/Services/P2p/HeadersChainService.cs:296-305`; `src/Dxs.Bsv/P2p/Chain/HeadersChain.cs:133-137`.

   **Evidence:** The wave goal says active mainnet tip and trailing headers are tracked from P2P and initial sync may seed from Bitails REST. The delivered DI binds `IHeadersBootstrapSource` to `NoopHeadersBootstrapSource`, whose `FetchAsync` always returns `null`. The closeout explicitly defers the real Bitails REST adapter. After an empty store, `HeadersChainService` calls `LoadFromStore(Array.Empty<...>())`, then continues after the no-op bootstrap. With an empty chain, `HeadersChain.TryExtend` accepts the first valid header it sees as height `0`; `BuildLocator()` sends either the current tip or an empty locator. That means the default W1 path cannot know the real mainnet height and cannot populate a current trailing ≤200 window unless some external bootstrap source is added later.

   **Why it matters:** W2 is supposed to consume `BlockHeaderStore` for tx-confirm checks, S6 is supposed to compare the admin tip against WhatsOnChain, and S7 is supposed to measure current-tip lag. A store seeded with an arbitrary first P2P header at height `0` is not a usable mainnet tip. Deferring the only height-aware source makes the W1 handoff incomplete.

   **Recommendation:** Ship a real default bootstrap path before opening W2, or make W1 explicitly fail closed when no height-aware seed is available. Concretely: add a production `IHeadersBootstrapSource` backed by an existing external adapter, seed `(header,height)` for the current tip or recent window, and test startup with an empty store through `HeadersChainService`. If pure-P2P cold start remains a goal, do not persist arbitrary first headers as height `0`; implement a bounded locator/backfill strategy that can reach the current tip without historical full-chain sync.

2. **H2 — S7 soak scaffold does not validate the production W1 path**

   **Where:** `docs/stream-tasks/bsv-headers-chain-wave/slices.md:589-595`, `docs/stream-tasks/bsv-headers-chain-wave/slices.md:650-663`; `docs/stream-tasks/bsv-headers-chain-wave/evidence/closeout.md:129-131`, `docs/stream-tasks/bsv-headers-chain-wave/evidence/closeout.md:171-172`; `tests/Spikes/P2p/HeadersSoakRecorder/README.md:7-13`, `tests/Spikes/P2p/HeadersSoakRecorder/README.md:77-82`; `tests/Spikes/P2p/HeadersSoakRecorder/HeadersSoakRecorder.csproj:11-13`; `tests/Spikes/P2p/HeadersSoakRecorder/Program.cs:98-135`, `tests/Spikes/P2p/HeadersSoakRecorder/Program.cs:179-182`; `tests/Spikes/P2p/HeadersSoakRecorder/analyze.py:99-105`.

   **Evidence:** `slices.md` requires the recorder to boot a minimal Consigliere headers-chain stack, reuse `PeerManager` + `HeadersChainService`, subscribe through a fake hub / notifier, and validate the admin endpoint at the end. The delivered spike references only `Dxs.Bsv`, says "no Consigliere, no Raven", hand-rolls `PeerSession` callbacks, assigns synthetic monotonic heights, and sends `getheaders` with an empty locator. The README also says to run `analyze.fsx`, but the committed analyzer is `analyze.py`. The actual 24h evidence file is still pending, and the analyzer treats `<128` joined blocks as a hard fail even though the spec says that case is inconclusive.

   **Why it matters:** The scaffold can measure some peer-pool callback behavior, but it does not prove `HeadersChainService`, `BlockHeaderStore`, `HubNewBlockNotifier`, or the admin endpoint work under the p95-lag claim. It also inherits the empty-locator/current-tip problem from H1, so it may never join current WhatsOnChain blocks by hash.

   **Recommendation:** Rework S7 to run the actual W1 service path: `PeerManager` + `HeadersChainService` + real/temporary store + fake `INewBlockNotifier` or in-process typed hub, then query `/api/admin/p2p/headers/tip` at the end. Align README and script names, implement the "inconclusive" result separately from fail, and do not mark S7 done until `evidence/headers-soak.md` exists or the wave explicitly reclassifies S7 as scaffold-only with a separate gate before W2.

3. **H3 — External hash order is inconsistent with the WhatsOnChain/admin validation contract**

   **Where:** `docs/stream-tasks/bsv-headers-chain-wave/launch-prompt.md:153-157`; `docs/stream-tasks/bsv-headers-chain-wave/slices.md:658-659`; `src/Dxs.Consigliere/Data/Models/P2p/BlockHeaderDocument.cs:10-13`; `src/Dxs.Consigliere/Controllers/AdminP2pController.cs:88-90`, `src/Dxs.Consigliere/Controllers/AdminP2pController.cs:103-106`; `src/Dxs.Consigliere/Services/P2p/HeadersChainService.cs:308-315`; `tests/Spikes/P2p/HeadersSoakRecorder/Program.cs:179-182`; `tests/Dxs.Consigliere.Tests/Controllers/AdminP2pControllerHeadersTests.cs:74-86`.

   **Evidence:** `BlockHeaderDocument` states `Hash` and `PrevHash` are wire-order hex and that display order is the byte-reverse used by explorers. The admin endpoints return `doc.Hash` / `doc.PrevHash` unchanged, and `BlockTipDto` is built from `BlockHeaderHasher.Hash` unchanged. The soak recorder reverses WhatsOnChain's `bestblockhash` into wire order for joining, but the wave validation says the admin endpoint tip must match WhatsOnChain `chain/info`. The controller tests use artificial strings like `h102`, so they do not catch byte-order mismatches.

   **Why it matters:** Operators comparing `/api/admin/p2p/headers/tip` to WhatsOnChain will get different strings unless they manually reverse bytes. W6's admin UI handoff also consumes `BlockTipDto` / header tips; exposing internal wire order without an explicit contract will create downstream ambiguity.

   **Recommendation:** Pick and document one external hash order. The pragmatic fix is to keep Raven/internal linking in wire order but convert `Hash` / `PrevHash` to explorer/display order in `BlockTipDto` and `AdminP2pController` responses, with tests using a real known header and expected WhatsOnChain display hash. If wire order is intentionally exposed, update launch/slices/README to say validators must reverse WhatsOnChain before comparison.

4. **M1 — Several new tests pass locally without exercising the behavior they claim to protect**

   **Where:** `tests/Dxs.Consigliere.Tests/P2p/BlockHeaderStoreTests.cs:41-45`; `tests/Dxs.Consigliere.Tests/P2p/HeadersChainBootstrapperTests.cs:85-99`, `tests/Dxs.Consigliere.Tests/P2p/HeadersChainBootstrapperTests.cs:122-143`; `tests/Dxs.Consigliere.Tests/P2p/HeadersChainServiceTests.cs:129-167`; `tests/Dxs.Consigliere.Tests/P2p/HubNewBlockNotifierTests.cs:23-42`; `tests/Dxs.Consigliere.Tests/Controllers/AdminP2pControllerHeadersTests.cs:68-87`.

   **Evidence:** The Raven-dependent tests use `if (!DotNetRuntimeFacts.HasRuntimeMajor(8)) return;`, which records them as passed on this machine even though the embedded Raven flow did not run. The S5 test is a Moq check of `HubNewBlockNotifier`, not a SignalR subscription/end-to-end test or a reorg-silence test. The S4 tests verify only a fake `IHeadersBootstrapSource`, not a real adapter or default DI behavior. The admin tests use placeholder hash strings, so they miss H3.

   **Why it matters:** The test suite is useful, but the closeout overstates what it proves. In this environment, the new Raven-flow tests are counted as passed while only guarding syntax and non-Raven branches. That makes "no new failures" true, but weaker than the slice validation matrix.

   **Recommendation:** Keep the runtime gate if that is the repo convention, but make it explicit via skip/trait reporting rather than silent `return`. Add non-Raven unit coverage for hash-order conversion and empty-chain bootstrap behavior, add a real SignalR subscription test or clearly downgrade S5 validation, and run Raven-backed W1 tests on a CI/host with the required embedded runtime before approving the wave.

## Per-Slice Closure Assessment

| Slice | Assessment |
| --- | --- |
| S0 — Contract freeze | **Closed.** Frozen surfaces remain intact after S0; `PeerSession.cs`, `IWalletHub.cs`, `IWalletServer.cs`, and DTOs were not changed by S1-S7. Contract-freeze and additive-dispatch tests are part of the passing `Dxs.Consigliere.Tests` / `Dxs.Bsv.Tests` runs. Code-only grep for `OnBlockInvReceived|OnInvReceived\(tx\)` returned zero matches. |
| S1 — HeadersChain pure logic | **Closed with integration caveat.** Pure chain validation, PoW target checks, duplicate/fork/orphan paths, and pruning are covered by unit tests. The empty-chain "first header is height 0" behavior is acceptable only behind a height-aware bootstrap; it is not safe as the default production current-tip path. |
| S2 — BlockHeaderStore | **Closed with environment caveat.** Store APIs exist and Raven tests are structurally meaningful, but they are runtime-gated locally. The API is enough for W2/W3 once H1/H3 are fixed. |
| S3 — HeadersChainService | **Partially closed.** Callback wiring, `getheaders` send, persistence, notifier call, and fork persistence are implemented. Cold-start/current-tip behavior is not closed because empty-store startup plus no-op bootstrap can persist a real mainnet header at height `0`. |
| S4 — Bootstrap | **Not closed.** The abstraction is reasonable, but the real Bitails/default adapter was deferred and the shipped default is `NoopHeadersBootstrapSource`. That is not enough to satisfy the wave's current-tip and 10s bootstrap claims. |
| S5 — Hub notifier | **Partially closed.** `HubNewBlockNotifier` routes to `block:tip` through the typed hub context, and `OnReorg` has no emitter. End-to-end SignalR subscription and reorg-silence coverage did not land. |
| S6 — Admin endpoints | **Partially closed.** Routes exist and basic shape/count tests exist. The hash-order mismatch means the documented WhatsOnChain comparison is not currently reproducible without manual conversion. |
| S7 — Soak harness | **Not closed.** The scaffold compiles, but it does not run the production W1 headers stack, the 24h evidence is pending, and the README/analyzer are not fully aligned with `slices.md` §S7. |

## Verification

- `dotnet build Dxs.Consigliere.sln -c Release` — passed, 0 errors.
- `dotnet build tests/Spikes/P2p/HeadersSoakRecorder/HeadersSoakRecorder.csproj -c Release` — passed, 0 errors.
- `dotnet test tests/Dxs.Bsv.Tests/Dxs.Bsv.Tests.csproj -c Release --no-restore` — passed, 149/149. I did not reproduce `PeerManager_FailureRecordsNegativeCooldown`; the test existed before W1 and the W1 diff only adds a using import, so the intermittency claim is not W1-introduced on this evidence.
- `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release --no-restore` — failed only the 3 known `TransactionStoreIntegrationTests` Raven embedded-runtime failures, 279/282 passed.
- Baseline check at `c7b1428`: `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release` also failed the same 3 `TransactionStoreIntegrationTests` with the same missing `.NET 8.0.18+` Raven embedded runtime and passed 260/263.
- Static freeze grep: `rg -n "OnBlockInvReceived|OnInvReceived\(tx\)" src tests` returned no matches.

## Closing Rationale

The contract freeze itself held, and S1/S2 establish useful primitives. The wave is not ready to approve as the handoff to W2, because the delivered default system does not yet produce a trustworthy current mainnet header tip, the admin/hash comparison contract is ambiguous, and the soak scaffold does not validate the production path it is supposed to prove.

Wave 2 should **not** open until H1 and H2 are addressed. H3 should be resolved in the same fix pass because it affects operator validation and W6-facing admin semantics.
