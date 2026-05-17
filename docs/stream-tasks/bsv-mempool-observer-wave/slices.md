# Wave 2 Slices — `bsv-mempool-observer-wave`

Nine slices total (S0 prerequisite + S1-S8 main). S0 is the
journal-contract extension and gets its own slice-level audit before
S1-S8 may open, mirroring the W1 pattern. S8 is operator-driven
live-mainnet validation; the wave can close with S8 explicitly
deferred if needed.

Wave-wide prerequisite (audit-noted): **Wave 1 closed.** Every slice
in this wave assumes `PeerSession.OnInvReceived(InvMessage)`,
`PeerSession.OnRejectReceived`, `BlockHeaderStore`, `HeadersChain`,
and `IPeerTelemetrySink` already exist per S0 frozen surface.

## S0 — Journal contract extension (prerequisite)

`depends_on = —`. Slice-level audit `audits/S0-A1.md` must APPROVE
before S1-S8 open.

### S0.1 — `TxObservationSource.P2p` constant

Add to `src/Dxs.Bsv/BitcoinMonitor/Models/TxObservation.cs`:

```csharp
public static class TxObservationSource
{
    public const string Node = "node";
    public const string JungleBus = "junglebus";
    public const string Bitails = "bitails";
    public const string P2p = "p2p"; // Wave 2 S0 — peer-to-peer observation source
}
```

No other field of `TxObservation` changes.

### S0.2 — Source-neutral `AppendAsync` overload

Add to `src/Dxs.Consigliere/BackgroundTasks/TxObservationJournalWriter.cs`:

```csharp
public async Task<bool> AppendAsync(
    TxObservation observation,
    RawTransactionPayloadReference? payload,
    string source,
    CancellationToken cancellationToken = default);
```

Semantics:

- The existing `AppendAsync(TxMessage, CancellationToken)` overload
  stays. Bitails / JungleBus runners continue to use it in W2;
  S6 only adjusts how they populate `TxMessage.Source` so it flows
  through to the same fingerprint.
- The new overload takes an already-built `TxObservation` (caller
  populates `Source` to one of the `TxObservationSource` constants),
  an optional payload reference (null when raw bytes weren't
  persisted), and the source string (kept as a separate parameter
  to make audit grep trivial — `rg -n 'AppendAsync\([^,]+, .+, "p2p"'`).
- Builds the same `DedupeFingerprint` as the existing overload
  via the internal `BuildFingerprint` helper, then calls
  `observationJournal.AppendAsync(...)`.
- Returns `true` on append, `false` on duplicate or invalid input.

### S0.3 — Projection rebuild test

Add `tests/Dxs.Consigliere.Tests/Data/Transactions/SeenBySourcesProjectionTests.cs`
(or extend an existing projection-rebuild test file if one matches):

- Build a fake journal with two entries for the same txid:
  1. `TxObservation(SeenInMempool, source = "p2p", txid = X)`
  2. `TxObservation(SeenInMempool, source = "bitails", txid = X)`
- Run the projection rebuilder over the journal.
- Assert `TxLifecycleProjectionDocument.SeenBySources` for txid X
  contains both `"p2p"` and `"bitails"` (order-independent).

### Owned paths (S0)

- `src/Dxs.Bsv/BitcoinMonitor/Models/TxObservation.cs` (extend with
  `P2p` constant)
- `src/Dxs.Consigliere/BackgroundTasks/TxObservationJournalWriter.cs`
  (add source-neutral overload)
- `tests/Dxs.Consigliere.Tests/Data/Transactions/SeenBySourcesProjectionTests.cs`
  (new — or extend an existing file)

### Validation (S0)

- `dotnet build Dxs.Consigliere.sln -c Release` → 0 errors.
- `dotnet test` → no new failures vs the pre-S0 baseline (record
  baseline counts in S0 evidence).
- `SeenBySourcesProjectionTests` green.
- Static check (audit grep, scoped to code only):
  `rg -n 'TxObservationSource\.P2p' src tests` returns at least one
  match in the source-neutral overload's tests; no occurrences in
  business-logic code that lands here in S0 (W2 main slices add
  those usages).

**Done when.** All declared surfaces compile, build green, tests
green, slice-level audit `audits/S0-A1.md` returns APPROVE.

## S1 — `TxScriptParser`

`depends_on = S0`.

**Intent.** Pure parsing primitives for extracting watchable
properties from a tx:

- P2PKH output `Hash160` (20 bytes) — used to match watched
  addresses.
- P2PKH input — extract `Hash160` from `OP_DUP OP_HASH160 ...`-style
  signature scripts (best-effort; some inputs spend non-standard
  outputs).
- STAS / DSTAS token output `TokenId` — when present.

**Owned paths.**

- `src/Dxs.Bsv/P2p/Observer/TxScriptParser.cs` (new)

Reuse primitives from `src/Dxs.Bsv/Script/` where they exist
(`OpCode`, script reader). Do NOT introduce a parallel script
interpreter; this is a thin extraction layer.

**Out of scope.** Full script evaluation. Multisig parsing.
Non-standard custom protocols beyond STAS / DSTAS.

**Validation.**

- `tests/Dxs.Bsv.Tests/P2p/Observer/TxScriptParserTests.cs`:
  - canonical P2PKH output → expected `Hash160`
  - P2PKH spend input → expected payer `Hash160` (or null for
    non-standard input)
  - STAS / DSTAS token output → expected `TokenId`
  - malformed scripts → null result, no exception
  - empty / oversized scripts → null result, no exception

**Done when.** All parser unit tests green; the cycle parse-
roundtrip succeeds for canonical fixtures.

## S2 — `WatchlistMatcher` pure logic

`depends_on = S0, S1`.

**Intent.** In-memory matcher answering "does this tx touch any
watched address or token?" with O(1) hot path.

**Design.**

- Two `HashSet<ulong>` indices:
  - `_addressPrefixes` — first 8 bytes of `Hash160` as LE `ulong`
  - `_tokenPrefixes` — first 8 bytes of `TokenId` as LE `ulong`
    (when token id is a 32-byte hash; otherwise an alternate
    indexing strategy noted in code comments)
- Per-prefix verification dictionaries:
  - `_addressFull` — `Dictionary<ulong, HashSet<byte[]>>` to confirm
    full hash160 (catches 8-byte prefix collisions)
  - `_tokenFull` — analogous for tokens
- `Add(WatchedAddress)`, `Add(WatchedToken)`, `Remove(...)` pairs.
- `Match(ParsedTx)` returns one of:
  - `MatchResult.None`
  - `MatchResult.AddressHit(Hash160[])`
  - `MatchResult.TokenHit(TokenId[])`
  - `MatchResult.Both(...)`

**Owned paths.**

- `src/Dxs.Bsv/P2p/Observer/WatchlistMatcher.cs` (new)
- `src/Dxs.Bsv/P2p/Observer/MatchResult.cs` (new)

**Out of scope.** Hot-reload-from-Raven plumbing (S3). Watchlist
authoring (already in `AdminTrackedController`).

**Validation.**

- `tests/Dxs.Bsv.Tests/P2p/Observer/WatchlistMatcherTests.cs`:
  - add address → match a tx output to it
  - remove address → next tx with that output is `None`
  - add two addresses with the same 8-byte prefix but different
    hash160; verify both match on their respective tx and a
    no-match tx returns `None` despite prefix hit
  - add token → token-output tx matches; other tx do not
  - very large addset (10 K random hash160) — no false positives
  - benchmark hook (used by S7 microbench): expose a static
    `Match(prefix)` overload for hot-path microbench

**Done when.** Every test green; matcher is allocation-free on the
hot path (no `new` in `Match`).

## S3 — `RavenWatchlistLoader`

`depends_on = S0, S2`.

**Intent.** Bridge between `WatchingAddress` / `WatchingToken` Raven
documents and the in-memory matcher.

**Behaviour.**

- On startup: bulk-query all `WatchingAddress` and `WatchingToken`
  docs; populate matcher.
- Open Raven Subscription on the `WatchingAddress` collection (and
  `WatchingToken` collection separately or jointly — implementer
  picks the cheapest). For each delta:
  - new doc → `matcher.Add(...)`
  - tombstone / deleted → `matcher.Remove(...)` (Raven subscription
    needs explicit deleted-doc tracking; use the same approach as
    any existing subscription in the repo)
- Expose `IsLoaded` so `P2pMempoolIngestRunner` waits before
  processing observed tx.

**Owned paths.**

- `src/Dxs.Consigliere/Services/P2p/RavenWatchlistLoader.cs` (new)

**Out of scope.** Cache invalidation across multiple Consigliere
instances (single-instance assumption per program scope §Out of
scope §multi-tenant watchlists).

**Validation.**

- `tests/Dxs.Consigliere.Tests/P2p/RavenWatchlistLoaderTests.cs`
  (runtime-gated via `[SkippableFact]` / `Skip.IfNot(...)` per
  W1 M1 convention):
  - bulk load: store 100 addresses, start loader, assert matcher
    contains all 100 within `IsLoaded` true
  - subscription delta: add a new address while loader is running;
    matcher contains it within 200 ms
  - delete: remove an address; matcher returns `None` for it
    within 200 ms
  - cold-start with 500 K addresses → `IsLoaded = true` within 2 s
    (gate threshold — record actual wall-clock in evidence)

**Done when.** Tests green; benchmark report
`evidence/watchlist-load-bench.md` records measured load time.

## S4 — `MempoolWatcher` core

`depends_on = S0, S2`.

**Intent.** Pure-ish coordinator. Inputs are `inv(MSG_TX)` items;
side-effects are `getdata` requests, journal appends, and recorder
ticks.

**Behaviour.**

- Dedupe `inv` items across peers using a bounded
  `ConcurrentDictionary<txid, FirstSeenAtMs>` (TTL eviction).
- For each newly-seen txid: issue exactly one `getdata` via a
  caller-provided `Func<txid, PeerSession, CT, Task>` (the runner
  injects the policy; tests inject a fake).
- Receive `tx` payloads via a `RecordTx(rawHex)` method:
  - Parse via `TxScriptParser`.
  - Match via `WatchlistMatcher`.
  - On `None`: increment `SourceObservationRecorder.UnmatchedCount`
    (W4 will consume).
  - On hit: build `TxObservation(SeenInMempool, source = "p2p",
    txid)`, call new journal overload, also persist raw bytes via
    `IRawTransactionPayloadStore` when configured.

**Owned paths.**

- `src/Dxs.Bsv/P2p/Observer/MempoolWatcher.cs` (new)
- `src/Dxs.Consigliere/Services/P2p/SourceObservationRecorder.cs`
  (new — basic counter surface that W4 will extend)

**Out of scope.** Rate-limit tuning beyond a sane default
(`MaxGetDataPerSec` config knob). Persistence of dedupe state
across restart.

**Validation.**

- `tests/Dxs.Bsv.Tests/P2p/Observer/MempoolWatcherTests.cs`:
  - dedupe: two peers push inv for same txid → exactly one
    `getdata` issued
  - rate limit: pushing N inv items >`MaxGetDataPerSec` causes
    deferred getdata, no dropped txids
  - hit path: matched tx is appended to a fake journal recorder
    with `source = "p2p"`
  - miss path: unmatched tx increments
    `SourceObservationRecorder.UnmatchedCount` and does NOT touch
    the journal

**Done when.** All watcher unit tests green; allocation
hot-path measured (no `new` per matched tx in steady state — record
in evidence if tractable).

## S5 — `P2pMempoolIngestRunner` hosted service

`depends_on = S0, S2, S3, S4`.

**Intent.** Wire the live `PeerManager` pool into `MempoolWatcher`
using the frozen S0 callbacks.

**Behaviour.**

- `IHostedService`. On start, wait for `RavenWatchlistLoader.IsLoaded`
  (so the matcher is warm before any tx flow); periodic reconcile
  (every 5 s) attaches `OnInvReceived` to each Ready peer that's
  not already wired — same pattern as
  `HeadersChainService.Reconcile`.
- Callback flow:
  1. `inv.Items` filtered to `InvType.Tx`
  2. for each item → `watcher.OnInv(txid, session)`
  3. `watcher` calls back into a `GetDataAsync(txid, session, ct)`
     delegate that calls `session.SendGetDataAsync(...)` and waits
     for the `tx` frame on `session.IncomingMessages`
- DI registration in `BsvP2pSetup`.

**Owned paths.**

- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs` (new)
- `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` (extend — register
  `WatchlistMatcher`, `RavenWatchlistLoader`, `MempoolWatcher`,
  `SourceObservationRecorder`, `P2pMempoolIngestRunner`)

**Out of scope.** Bootstrap from non-P2P sources (we don't need
to pre-seed the matcher beyond Raven). Peer scoring (W6).

**Validation.**

- `tests/Dxs.Consigliere.Tests/P2p/P2pMempoolIngestRunnerTests.cs`
  (runtime-gated; uses `MiniBsvServer` from `tests/Shared/`):
  - fake peer sends `inv(MSG_TX, txid)` → service issues `getdata`
  - fake peer responds with `tx` → journal recorder sees one
    observation with `source = "p2p"`
  - 1 K-tx fixture run → no exceptions; observation count ≥ 95 %
    of inv count (rate-limit headroom)

**Done when.** All runner tests green; service starts cleanly in
the embedded Consigliere host with watchlist loader joined.

## S6 — Bitails / JungleBus runner source-tag cleanup

`depends_on = S0`.

**Intent.** Make sure both existing realtime runners pass their
source string explicitly through the new journal overload so the
journal stays source-aware. **No behavioural change** beyond
explicit tagging.

**Behaviour.**

- `BitailsRealtimeIngestRunner`: ensure every `TxMessage.Source` it
  builds is `TxObservationSource.Bitails` and is preserved when
  the message flows through `TxObservationJournalWriter.AppendAsync`.
- `JungleBusRealtimeIngestRunner`: same with
  `TxObservationSource.JungleBus`.
- If the current runners already do this correctly,
  S6's only deliverable is a regression test pinning the behaviour
  so a future refactor can't silently drop the tag.

**Owned paths.**

- `src/Dxs.Consigliere/BackgroundTasks/Realtime/BitailsRealtimeIngestRunner.cs`
- `src/Dxs.Consigliere/BackgroundTasks/Realtime/JungleBusRealtimeIngestRunner.cs`
- `tests/Dxs.Consigliere.Tests/BackgroundTasks/Realtime/*` (extend
  the existing runner tests with a source-tag assertion)

**Out of scope.** Refactoring runner internals. Per-source metrics
(W4).

**Validation.**

- existing runner tests still green after the source-tag pin
- new regression-test assertion: `TxMessage.Source` is the expected
  constant for every captured observation

**Done when.** Both runner test suites green with the tagging
assertion in place.

## S7 — Watchlist correctness fixture suite + microbenchmark

`depends_on = S0, S1, S2, S3`.

**Intent.** First-class validation of the watchlist matcher across
the scenarios called out by the program §Mandatory second slice
(parent `slices.md` §Wave 2).

**Fixtures.**

- address-output (P2PKH): tx pays a watched address → match
- address-input (spending tx): tx spends a UTXO whose script
  reveals the watched address as payer → match
- STAS token output: tx output declares watched `TokenId` → match
- DSTAS token output: same with DSTAS encoding → match
- delete-during-observation: address watched, observed, then
  removed mid-stream → next tx with that address is `None`
- 8-byte prefix collision: two distinct hash160 sharing the prefix;
  exactly one is watched; the unwatched tx returns `None`
- 500 K-address load benchmark (BenchmarkDotNet):
  - load time wall-clock ≤ 2 s
  - hot-path lookup p99 ≤ 100 ns

**Owned paths.**

- `tests/Dxs.Bsv.Tests/P2p/Observer/WatchlistFixtureSuiteTests.cs`
  (extends `WatchlistMatcherTests` with the full suite)
- `tests/Dxs.Consigliere.Benchmarks/WatchlistMatcherBench.cs` (new —
  BenchmarkDotNet microbenchmark; mirrors any existing bench
  csproj pattern)

**Out of scope.** Multi-tenant watchlist scaling. Bloom-filter
comparison.

**Validation.**

- every fixture scenario green
- microbenchmark report `evidence/watchlist-bench.md` records
  measured p50 / p95 / p99 lookup latency and load time

**Done when.** Fixture suite + bench produce green results; bench
report committed.

## S8 — Live mainnet validation (operator-driven)

`depends_on = S0–S7`.

**Intent.** Prove end-to-end on real BSV mainnet: a transaction
paying a watched address surfaces via `WalletHub.OnTransactionFound`
with `SeenBySources` containing `p2p`.

**Procedure.**

1. Operator picks a watched address that's likely to receive a
   small testnet-style payment (or sends from their own wallet).
2. Run Consigliere with `Consigliere:Broadcast:P2p:Enabled = true`
   and the address registered as a `WatchingAddress`.
3. Pay the address with a tiny BSV amount.
4. Confirm `WalletHub.OnTransactionFound` fires within 2 s of
   `inv(MSG_TX)` arrival.
5. Query `TxLifecycleProjectionDocument` for that txid and
   confirm `SeenBySources` contains `p2p` (and possibly
   `bitails` / `junglebus` race-dependent).
6. Record evidence in `evidence/live-validation.md`: txid, watched
   address, `inv` arrival timestamp (from logs), hub-fire timestamp,
   end-state `SeenBySources` array.

**Owned paths.**

- `docs/stream-tasks/bsv-mempool-observer-wave/evidence/live-validation.md`
  (new — written by the operator at the time of the run)

**Out of scope.** A standalone soak harness for S8 unless the
in-process Consigliere run can't produce the evidence cleanly
(then optionally fall back to `tests/Spikes/P2p/MempoolWatcherSoak/`).

**Validation.**

- `evidence/live-validation.md` exists with the recorded fields.

**Done when.** Live evidence file committed, OR the wave's
closeout explicitly notes S8 deferred to a separate operator
session with a reason and a date for the follow-up.

## Dependency Graph

```
                ┌────────────────────────────────────────────┐
                │ S0 — Journal contract extension (prereq)  │
                │ slice-level audit gates S1-S8             │
                └─────┬──────────────────────────────────────┘
                      │
        ┌─────────────┼─────────────┬────────────┐
        ▼             ▼             ▼            ▼
   ┌─────────┐  ┌─────────────┐  ┌──────┐   ┌────────────┐
   │ S1      │  │ S2          │  │ S6   │   │            │
   │ Tx      │  │ Matcher     │  │ Bita │   │            │
   │ script  │  │ pure logic  │  │ /JBus│   │            │
   │ parser  │  │             │  │ tags │   │            │
   └────┬────┘  └──────┬──────┘  └──────┘   │            │
        │              │                     │            │
        └──────┬───────┘                     │            │
               │                             │            │
               ▼                             │            │
         ┌──────────────┐                    │            │
         │ S3           │                    │            │
         │ Raven loader │                    │            │
         └─────┬────────┘                    │            │
               │                             │            │
               │ ┌───────────────────────────┘            │
               ▼ ▼                                        │
         ┌──────────────┐    ┌──────────────┐             │
         │ S4 (S0,S2)   │    │ S7 (S0..S3)  │             │
         │ Watcher core │    │ fixture +    │             │
         │              │    │ bench        │             │
         └─────┬────────┘    └──────────────┘             │
               │                                          │
               ▼                                          │
         ┌──────────────────────┐                         │
         │ S5 (S0,S2,S3,S4)     │                         │
         │ P2pMempoolIngestRunner│                        │
         └─────┬────────────────┘                         │
               │                                          │
               ▼                                          │
         ┌──────────────────────┐                         │
         │ S8 (S0..S7)          │                         │
         │ Live mainnet validate│                         │
         └──────────────────────┘                         │
```

Direct dependency edges encoded in the ledger:

- S0: —
- S1: S0
- S2: S0, S1
- S3: S0, S2
- S4: S0, S2
- S5: S0, S2, S3, S4
- S6: S0
- S7: S0, S1, S2, S3
- S8: S0, S1, S2, S3, S4, S5, S6, S7

Default sequential order (strict stop-and-audit, operator preferred):
S0 → S1 → S2 → S3 → S4 → S5 → S6 → S7 → S8. Parallelism is allowed
only with per-slice audit gates; default does not require those.

## Per-slice Audit Rules

- S0 receives its own slice-level audit at `audits/S0-A1.md`. **No
  main slice opens until S0's slice audit returns APPROVE.**
- S1-S8 are covered by the single wave-level audit at
  `audits/wave2-audit-A1.md` after all slices are `done`.
- If any slice surfaces a residual that requires a fix-and-re-audit
  pass, open `audits/A<n+1>.md` or `audits/<slice>-A<n+1>.md`.
- S8 may close the wave even if deferred; the closeout records the
  defer reason and a follow-up date.

## Validation Matrix (wave-level)

| signal | slice | how validated |
|---|---|---|
| `TxObservationSource.P2p` constant present | S0 | grep + projection rebuild test |
| Source-neutral journal overload accepts `p2p` | S0 | unit test calling `AppendAsync(observation, payload, "p2p", ct)` |
| `SeenBySources` accumulates `p2p` + `bitails` | S0 | projection-rebuild test in `SeenBySourcesProjectionTests` |
| P2PKH parsing | S1 | canonical fixture inputs / outputs |
| Token parsing (STAS / DSTAS) | S1 | canonical token-output fixtures |
| Matcher hit / miss / collision | S2 | `WatchlistMatcherTests` |
| Matcher hot path p99 ≤ 100 ns | S2 / S7 | BenchmarkDotNet report |
| Watchlist 500 K load ≤ 2 s | S3 / S7 | BenchmarkDotNet + Raven integration test |
| Hot reload (add / remove) within 200 ms | S3 | Raven integration test |
| Dedupe + rate limit | S4 | unit test |
| End-to-end fake peer → journal | S5 | integration test with `MiniBsvServer` |
| Bitails / JBus tags preserved | S6 | runner regression tests |
| Live mainnet hit | S8 | `evidence/live-validation.md` |
| Build green | every | `dotnet build Dxs.Consigliere.sln -c Release` 0 errors |
| Tests green | every | `dotnet test` no new failures vs baseline |
