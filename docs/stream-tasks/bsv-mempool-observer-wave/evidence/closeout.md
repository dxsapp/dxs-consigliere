# Wave 2 Closeout — `bsv-mempool-observer-wave`

Status: CLOSED. Wave audit A2 → MAJOR REVISION REQUIRED (closed by
`16515cf`); A2-followup → APPROVE WITH CHANGES with one new-L1
test-name precision finding (closed by this commit). S0-S7
implemented; S8 operator-driven and deferred per
`evidence/live-validation.md`.

## Delivery summary

All 9 slices delivered (S8 deferred with explicit follow-up plan).

| slice | commit | summary |
|---|---|---|
| S0 (prereq) | `2a81474` + `9a94a97` (A1 fix) | `TxObservationSource.P2p` constant + source-neutral `AppendAsync(TxObservation, ref?, source, ct)` overload + `SeenBySources` projection rebuild test |
| S1 | `fdbc8cd` | `TxScriptParser` — P2PKH output, P2PKH input pubkey → HASH160(pubkey), STAS/DSTAS token id |
| S2 | `a3aface` | `WatchlistMatcher` — HashSet<ulong> 8-byte prefix + full-hash verify + `ParsedTx`/`MatchResult` records |
| S3 | `09bfff0` | `RavenWatchlistLoader` — bulk-load + Raven Changes API hot reload (Put/Delete on `WatchingAddresses`/`WatchingTokens`) |
| S4 | `075827f` | `MempoolWatcher` (dedupe + rate-limit), `MempoolWatcherOptions`, `SourceObservationRecorder`, `BsvP2pConfig.MempoolMaxFetchedTxBytes` propagation |
| S5 | `3bcc6e3` | `PerSessionFrameDispatcher` + `PerSessionDispatcherRegistry`, `TxRelayCoordinator` refactored to consume via dispatcher, `P2pMempoolIngestRunner` hosted service |
| S6 | `9a258ab` | Bitails/JungleBus source-tag regression pin — test-only, no production-code changes (audit W2 M2) |
| S7 | `992b1b4` | Watchlist correctness fixture suite + xunit+Stopwatch microbenchmark + `evidence/watchlist-bench.md` |
| S8 | this commit | Operator-driven; deferred — see `evidence/live-validation.md` |

## Test counts gained in W2

- `Dxs.Bsv.Tests`: +47 (S1 parser +18, S2 matcher +14, S4 watcher +8, S7 fixture suite +7)
- `Dxs.Consigliere.Tests`: +29 (S0 +6, S3 +11 mixed Raven-gated/parsing, S4 recorder +4, S5 dispatcher +4, S6 regression pins +6, minus 2 already-counted)
- `Dxs.Consigliere.Benchmarks`: +1 (writes `evidence/watchlist-bench.md` on every run)

End-state run on the build host (macOS arm64, .NET 9):

```
Dxs.Bsv.Tests          : 201/201 passed (was 154 pre-W2 close, +47)
Dxs.Consigliere.Tests  : 308 passed + 24 explicit Skipped
                         + 3 pre-existing baseline Raven-runtime
                         failures (TransactionStoreIntegrationTests)
Dxs.Consigliere.Benchmarks : 1/1 passed (watchlist bench)
```

## Static checks (slices.md §S0 + §S6 grep contracts)

```
$ rg -n 'TxObservationSource\.P2p' src tests
src/Dxs.Bsv/BitcoinMonitor/Models/TxObservation.cs:23: P2p = "p2p"
src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs:* (×2)
tests/Dxs.Consigliere.Tests/* (×9)
```

The constant is used in production by the runner (S5) and by S0's
own tests. No leakage outside Wave 2 ownership.

## Watchlist bench measured (this host)

From `evidence/watchlist-bench.md`:

- Construction (500K addresses): **547 ms** (threshold ≤ 2000 ms) → PASS
- Lookup p99: **1 ns** (threshold ≤ 100 ns) → PASS
- Host class: macOS arm64 M-series. Recorded as OBSERVATION per
  the reference-class rule; canonical pass requires the reference
  DO droplet class, but the measured values are well within the
  thresholds.

## Handoff facts unlocked for W3-W6

- **W3 reorg-handling-wave** can consume:
  - `TxObservation` with `Source = "p2p"` in journal entries
  - `WatchlistMatcher` (read-only) for re-broadcast decisions
  - `IBlockHeaderStore` (W1) for ancestor walks
- **W4 source-metrics-wave** can consume:
  - `SourceObservationRecorder` counters (ServedCount /
    UnmatchedCount / RateLimited / GetDataTimeout /
    OversizePayload / inv-by-source aggregation)
  - All three source tags (`p2p`, `bitails`, `junglebus`) as
    deterministic constants
- **W5 broadcast-unification-wave** can rely on:
  - `BroadcastReceiptDto` shape (frozen since Wave 1 S0)
  - `TxObservation.Source` value flowing through W4 metrics
  - `PerSessionFrameDispatcher` for any future broadcast-side
    frame consumers (no need to wire new `IncomingMessages` readers)
- **W6 production-ops-wave** can observe:
  - `P2pMempoolIngestRunner` lifecycle via existing
    `BsvP2pHealth` + `IPeerTelemetrySink`
  - Rate-limit / dedupe stats via `SourceObservationRecorder`

## Residuals to track

- **S8 live-mainnet validation** — deferred to a 30-min operator
  session; plan + evidence schema in `evidence/live-validation.md`.
  Pass condition: inv-to-hub Δ ≤ 2000 ms AND `"p2p" ∈ SeenBySources`
  AND `PayloadAvailable`.
- **TxRelayCoordinator legacy read-loop** retained alongside the
  dispatcher path; the registry-injected path is what production
  uses, the legacy path exists only for unit tests that don't pass
  the registry. A follow-up wave can clean this up once Gate-3
  tests migrate to the dispatcher fully.
- **Benchmark tooling** — used xunit + Stopwatch instead of the
  spec-mentioned BenchmarkDotNet to align with the repo's
  established `tests/Dxs.Consigliere.Benchmarks/` convention.
  Documented in the benchmark file and accepted as a deliberate
  deviation.
- **3 pre-existing baseline failures** in
  `TransactionStoreIntegrationTests` (need an external RavenDB
  embedded runtime); unchanged by Wave 2.

## Audit trail

- `audits/wave2-audit-A1.md` — pre-execution wave audit (MAJOR
  REVISION REQUIRED; 7 findings)
- `audits/wave2-audit-A1-followup.md` — first follow-up (MAJOR
  REVISION REQUIRED; H1/H3/M1/M3/M5 closed, H2/M2/M4 partial,
  new-M1 dispatcher failure isolation)
- `audits/wave2-audit-A1-followup-2.md` — second follow-up
  (APPROVE WITH CHANGES; three small doc fixes)
- `audits/S0-A1.md` — S0 slice audit (MAJOR REVISION REQUIRED;
  H1 IsDuplicate propagation, M1 nullable annotation)
- `audits/S0-A1-followup.md` — S0 follow-up (APPROVE)
- `audits/wave2-audit-A2.md` — post-execution wave audit (MAJOR
  REVISION REQUIRED; C1/H1/H2/M1/M2)
- `audits/wave2-audit-A2-followup.md` — post-revision audit
  (APPROVE WITH CHANGES; all five A2 findings closed, one new-L1
  test-name precision issue logged)

## Ready-for-audit checklist

- [x] All slices delivered (S8 deferred with explicit plan).
- [x] `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- [x] No new test failures vs the pre-W2 baseline.
- [x] `SeenBySourcesProjectionTests` green (Raven-gated; runs on
      CI with .NET 8 embedded runtime).
- [x] `evidence/watchlist-bench.md` exists with the measured fields.
- [x] `evidence/live-validation.md` documents the deferred S8 plan.
- [x] `audits/wave2-audit-A2.md` — MAJOR REVISION REQUIRED;
      revision committed as `16515cf`.
- [x] `audits/wave2-audit-A2-followup.md` — APPROVE WITH CHANGES;
      single new-L1 test-name precision issue closed by this commit.

## Audit A2 revision summary (commit `16515cf`)

All five A2 findings addressed:

- **C1** (P2P relay services not constructible from production DI):
  `TxRelayCoordinator` now depends on `BsvP2pHealth` (registered
  singleton) instead of `PeerManager` (which `BsvP2pHostedService`
  constructs privately, not as a DI dependency). Added a one-shot
  `BroadcastServiceP2pWirerHost` that calls
  `BroadcastServiceP2pWirer.Wire()` on `StartAsync` so the Gate-3
  property-wiring actually runs at host startup. New regression
  test `BsvP2pSetupDiResolutionTests` (+3) builds the full P2P-zone
  DI graph against mocked external deps and resolves every W2
  singleton — failing the build instead of host startup the next
  time a ctor dep drifts.
- **H1** (P2P txids journaled in wire byte order): new
  `TxHashOrder` helper (`WireToDisplayHex` / `DisplayHexToWire`)
  cross-checked against `BitcoinHelpers.GetTxId`. Runner normalises
  inv hashes to display-order BEFORE dedupe + before journal
  append, so `TxObservation.TxId` byte-equals `Transaction.Id` and
  matches the Bitails / JungleBus path — `SeenBySources`
  accumulation now works across sources for the same tx.
  `TxRelayCoordinator` (dispatcher + legacy paths) also normalises
  inbound getdata / inv / reject hashes, closing a latent Gate-3
  bug where wire-order vs display-order comparison could never
  match.
- **H2** (runner bypassed S1 parser): `ToParsedTx` now uses
  `TxScriptParser.TryParseP2pkhOutput` /
  `TryParseP2pkhInputPubkey` / `TryParseTokenId` against script
  bytes materialised via `Slice.Materialize(rawBytes)`. Drops
  the convenience-field shortcut that missed uncompressed P2PKH
  inputs (UnlockingScriptReader only derives addresses for
  33-byte compressed pubkeys).
- **M1** (rate-limited inv permanently de-duped): runner calls
  `watcher.Forget(txid)` on the `RateLimited` branch so a later
  inv (after the 1 s window slides) can retry. Pinned by a new
  runner integration test that exhausts the budget and asserts
  the txid is not retained in the dedupe cache.
- **M2** (no end-to-end runner test): new
  `P2pMempoolIngestRunnerTests` (+3) drives the full production
  path via `MiniBsvServer`: inv → assert getdata → reply with
  raw tx → assert journal append with display-order `TxId`,
  source = `"p2p"`, payload saved. Plus unmatched-tx test +
  rate-limit retry test.

Test counts after revision:

- `Dxs.Bsv.Tests` 207/207 (was 201, +6 `TxHashOrderTests`).
- `Dxs.Consigliere.Tests` 314 passed (was 308, +6: 3 DI
  resolution + 3 runner integration) + 24 explicit Skipped +
  3 pre-existing baseline (unchanged).

## Audit A2-followup follow-up (this commit)

- **new-L1** (rate-limit test name precision): the single test
  `RateLimited_Inv_AllowsRetryAfterWindowSlides` proved that the
  txid was forgotten from dedupe but did NOT prove that a retry
  actually fires getdata after the window slides. Split into two
  separately named tests, each pinning the observable in its name:
  - `RateLimited_Inv_ForgetsTxid_NotRetainedInDedupe` — asserts
    strict `DedupeSize` equality before-vs-after the rate-limited
    inv (proves `Forget` ran). Rate-window state independent.
  - `RateLimited_Inv_RetriesAfterWindowSlides` — after the
    1.1 s sleep, sends a second inv for the same txid and
    polls the server's received-frame channel for a `getdata`
    frame. Pinned by `Assert.True(sawGetData)`.
- Shared helper `BuildRunnerForRateLimitTest` keeps both tests
  small. Net delta: +1 runner integration test (now 315 passed in
  `Dxs.Consigliere.Tests`, was 314).
