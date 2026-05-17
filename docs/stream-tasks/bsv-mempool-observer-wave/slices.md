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
  stays. Bitails / JungleBus runners continue to use it in W2
  **without any production-code change** (audit W2 M2 reconciliation
  — inspection confirmed both already construct `TxMessage` with
  the correct source constant). S6 only adds source-tag regression
  assertions to their existing test files; no migration.
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

- P2PKH output `Hash160` (20 bytes) extracted from the standard
  locking script
  `OP_DUP OP_HASH160 <20-byte-hash> OP_EQUALVERIFY OP_CHECKSIG`.
- P2PKH **input** payer `Hash160` — corrected per audit W2 H3.
  The standard P2PKH unlocking script (scriptSig) has the shape
  `<sig> <pubkey>` (a 71-73-byte signature push followed by a
  33-byte compressed or 65-byte uncompressed pubkey push). Parser
  extracts the pubkey push and computes `HASH160(pubkey)`; that
  hash is what we match against `WatchingAddress.Address` hashes.
  Returns null for non-standard inputs (multisig, custom scripts);
  W2 does not parse those.
- STAS / DSTAS token output `TokenId` — when present.

**Owned paths.**

- `src/Dxs.Bsv/P2p/Observer/TxScriptParser.cs` (new)

Reuse primitives from `src/Dxs.Bsv/Script/` where they exist
(`OpCode`, script reader, `Hash.Sha256Sha256Ripedm160` for the
input pubkey → hash160 derivation). Do NOT introduce a parallel
script interpreter; this is a thin extraction layer.

**Out of scope.** Full script evaluation. Multisig parsing.
Non-standard custom protocols beyond STAS / DSTAS. Prevout
context — we don't have UTXO lookups in W2; input matching is
the pubkey-derivation path only.

**Validation.**

- `tests/Dxs.Bsv.Tests/P2p/Observer/TxScriptParserTests.cs`:
  - canonical P2PKH **output** with known address → expected
    `Hash160` extracted from locking script
  - canonical P2PKH **input** with known pubkey → expected
    payer `Hash160 = HASH160(pubkey)` computed from scriptSig
  - P2PKH input with **compressed** vs **uncompressed** pubkey
    (33 vs 65-byte push) both round-trip correctly
  - non-standard input (multisig / custom) → null, no exception
  - canonical STAS token output → expected `TokenId`
  - canonical DSTAS token output → expected `TokenId`
  - malformed scripts → null result, no exception
  - empty / oversized scripts → null result, no exception
  - false-positive prefix-collision case: fabricate an output
    that shares an 8-byte prefix with a watched hash160 but
    differs in the remaining 12 bytes — parser returns the full
    20-byte `Hash160`, matcher's full-verify (S2) rejects.

**Done when.** All parser unit tests green; the cycle parse-
roundtrip succeeds for canonical fixtures including a real
mainnet P2PKH input/output pair.

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
- Subscribe to Raven **Changes API** (NOT Raven Subscription —
  audit W2 H2 reconciliation; matches the existing repo pattern in
  `src/Dxs.Consigliere/BackgroundTasks/StasAttributesChangeObserverTask.cs`
  which listens for `Put` and `Delete` on a collection). For each
  change:
  - `DocumentChangeTypes.Put` on `WatchingAddresses` collection
    → load the document by id → `matcher.Add(address)`
  - `DocumentChangeTypes.Delete` on `WatchingAddresses` collection
    → derive the address from the document id (`address/{Address}`
    convention from `WatchingAddress.GetId()`) and call
    `matcher.Remove(address)`
  - same for `WatchingTokens` collection using `WatchingToken.GetId()`
    convention `token/{TokenId}/{Symbol}`
- Reconnect-on-error using the same `Subscribe` + log-on-error
  pattern as `StasAttributesChangeObserverTask` (the
  `PeriodicTask.RunAsync` outer loop owns retry).
- Expose `IsLoaded` so `P2pMempoolIngestRunner` waits before
  processing observed tx.

**Owned paths.**

- `src/Dxs.Consigliere/Services/P2p/RavenWatchlistLoader.cs` (new)

**Out of scope.** Cache invalidation across multiple Consigliere
instances (single-instance assumption per program scope §Out of
scope §multi-tenant watchlists). Raven Subscription API as a
mechanism — explicitly NOT used in W2 because its delete-stream
semantics don't fit the watchlist remove path; Changes API is the
right primitive here.

**Validation.**

- `tests/Dxs.Consigliere.Tests/P2p/RavenWatchlistLoaderTests.cs`
  (runtime-gated via `[SkippableFact]` / `Skip.IfNot(...)` per
  W1 M1 convention):
  - bulk load: store 100 addresses, start loader, assert matcher
    contains all 100 within `IsLoaded` true
  - **Put delta**: add a new `WatchingAddress` while loader is
    running; matcher contains it within 200 ms
  - **Delete via admin untrack path**: exercise the existing
    untrack flow (which deletes the `WatchingAddress` doc — see
    `TrackedEntityRegistrationStore.cs` lines ~207-226 for the
    delete site); matcher returns `None` for the removed address
    within 200 ms
  - **WatchingToken** same as address — separate Put + Delete tests
  - cold-start with 500 K addresses → `IsLoaded = true` within 2 s
    (gate threshold — record actual wall-clock in evidence,
    benchmark settings per §S8 reproducibility block)

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

### Payload-size policy (audit W2 M4 + followup)

BSV transactions routinely exceed the legacy 2 MiB
`PeerSessionConfig.InitialMaxRecvPayloadLength` default. W2 must
have an explicit, fully-owned config path.

**Config knob ownership.** S4 adds a new config field to the
existing `src/Dxs.Consigliere/Configs/BsvP2pConfig.cs`:

```csharp
public sealed class BsvP2pConfig
{
    // ... existing fields (PoolSize, Network, UserAgent, etc.) ...

    /// <summary>
    /// Maximum P2P tx payload accepted by mempool observation
    /// (audit W2 M4). Default 32 MiB — matches BSV mainnet
    /// typical mempool acceptance ceiling; operators may raise.
    /// Propagates into PeerSessionConfig.InitialMaxRecvPayloadLength
    /// at session construction time (see BsvP2pHostedService).
    /// </summary>
    public int MempoolMaxFetchedTxBytes { get; set; } = 32 * 1024 * 1024;
}
```

**Propagation path.** S5 (the slice that wires the runner) also
edits `src/Dxs.Consigliere/Services/P2p/BsvP2pHostedService.cs`
lines ~66-80 where `PeerSessionConfig` is constructed:

```csharp
SessionConfig = new PeerSessionConfig
{
    ConnectTimeout = TimeSpan.FromMilliseconds(_config.ConnectTimeoutMs),
    HandshakeTimeout = TimeSpan.FromMilliseconds(_config.HandshakeTimeoutMs),
    SendProtoconfAfterVerack = _config.SendProtoconfAfterVerack,
    // Audit W2 M4: raise the inbound payload cap so mempool tx
    // beyond the legacy 2 MiB legacy default still get accepted.
    InitialMaxRecvPayloadLength = _config.MempoolMaxFetchedTxBytes,
},
```

`MempoolWatcherOptions.MaxFetchedTxBytes` mirrors the same value
so the watcher's sanity-check is the same as the session's actual
ceiling. Production DI binds them from the single config field.

**Failure mode distinction (audit W2 M4 followup).** The runner
must distinguish three outcomes per outstanding `getdata`:

| Outcome | Trigger | Recorder counter | Session effect |
|---|---|---|---|
| `Served` | matching `tx` frame arrives within `GetDataTimeoutMs` | `ServedCount` | session stays alive |
| `OversizeRejected` | inbound frame exceeds `MempoolMaxFetchedTxBytes` and `PeerSession.ReceiveLoopAsync` raises `P2pDecodeException("Inbound payload … exceeds limit …")` → session ends with `DisconnectReason.ProtocolViolation` | `OversizePayloadCount` | session disconnects (Wave 1 behaviour) |
| `Timeout` | no `tx` frame and no disconnect within `GetDataTimeoutMs` (peer silently dropped or is slow) | `GetDataTimeoutCount` | session stays alive |

The recorder distinguishes these so an operator can tell a busted
peer from a real oversize tx; W4's metrics surface will read all
three counters. Classification mechanism for the one-shot
dispatcher handler (S5) at timeout — `PeerSession` exposes only
`Task<DisconnectReason> Completion`, no separate
`LastDisconnectReason` property, so the classifier reads
`Completion`:

- `session.Completion.IsCompletedSuccessfully &&
   session.Completion.Result == DisconnectReason.ProtocolViolation`
  → classify as `OversizeRejected` (the receive loop tripped the
  payload-size guard and called `EndWith(ProtocolViolation, ...)`).
  Note: not perfectly unambiguous — any frame decode failure also
  ends with `ProtocolViolation` — so the classifier records the
  oversize-bucket count only when the disconnect happened **within
  the same `getdata` round** AND `session.PeerMaxRecvPayloadLength
  >= MempoolMaxFetchedTxBytes` (i.e. the peer's negotiated cap was
  high enough that we have plausible cause to believe the frame
  exceeded *our* cap, not the peer's).
- Otherwise (session still alive OR completed with any other
  reason) → `Timeout`.

The implementation lives inside the one-shot tx-frame handler's
finally block; the recorder gets one counter increment per
outstanding `getdata` that didn't get its `tx`.

**Operator-warning rule.** Startup emits a warning if
`BsvP2pConfig.MempoolMaxFetchedTxBytes` is set below 4 MiB —
most modern BSV mempools see legitimate tx beyond the legacy
2 MiB cap. The warning is in `BsvP2pHostedService.StartAsync`.

### Owned paths (S4)

- `src/Dxs.Bsv/P2p/Observer/MempoolWatcher.cs` (new)
- `src/Dxs.Bsv/P2p/Observer/MempoolWatcherOptions.cs` (new — config
  record incl. `MaxGetDataPerSec`, `MaxFetchedTxBytes`,
  `GetDataTimeoutMs`)
- `src/Dxs.Consigliere/Configs/BsvP2pConfig.cs` (extend — add
  `MempoolMaxFetchedTxBytes` field per the payload-size policy
  block above; audit W2 followup M4)
- `src/Dxs.Consigliere/Services/P2p/BsvP2pHostedService.cs`
  (extend lines ~66-80 — propagate `MempoolMaxFetchedTxBytes`
  into `PeerSessionConfig.InitialMaxRecvPayloadLength` at session
  construction; add startup warning if value < 4 MiB)
- `src/Dxs.Consigliere/Services/P2p/SourceObservationRecorder.cs`
  (new — see §Ownership note below; placement reconciled with
  parent program per audit W2 M3)

> **Ownership note (audit W2 M3 reconciliation).**
> `SourceObservationRecorder` is Consigliere-orchestration state
> (counts per "bitails" / "junglebus" / "p2p" — labels that are
> Consigliere-level, not BSV-protocol-level), so it lives under
> `src/Dxs.Consigliere/Services/P2p/`. The parent program's
> `slices.md` initially listed it under `src/Dxs.Bsv/P2p/Observer/`;
> a parallel parent-program update moves it here so the two agree.

**Out of scope.** Rate-limit tuning beyond a sane default
(`MaxGetDataPerSec`). Persistence of dedupe state across restart.

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
  - **oversize-tx case**: simulate a `tx` frame exceeding
    `MaxFetchedTxBytes` — recorder increments `OversizePayloadCount`,
    no journal append, no exception bubble
  - **large-but-allowed case**: 4 MiB tx well under
    `MaxFetchedTxBytes` → normal path, matched tx persisted

**Done when.** All watcher unit tests green; allocation
hot-path measured (no `new` per matched tx in steady state — record
in evidence if tractable); `MaxFetchedTxBytes` knob documented in
operator-facing config notes.

## S5 — `PerSessionFrameDispatcher` refactor + `P2pMempoolIngestRunner`

`depends_on = S0, S2, S3, S4`.

**Audit W2 H1 fix:** the previous draft had
`P2pMempoolIngestRunner` reading `session.IncomingMessages`
directly to await `tx` frames. That races
`TxRelayCoordinator.WatchSessionAsync` (Gate 3) which already owns
the same channel reader per session — `ChannelReader<T>` is
single-consumer per item, so each `tx` frame would go to whichever
loop wins the race. This slice introduces a per-session dispatcher
in Consigliere that takes over ownership of `session.IncomingMessages`
and routes frames to all interested parties. No new `PeerSession`
callbacks needed — the Wave 1 S0 contract freeze stays intact.

### S5.1 — `PerSessionFrameDispatcher`

Add `src/Dxs.Consigliere/Services/P2p/PerSessionFrameDispatcher.cs`
(new):

- One dispatcher instance per `PeerSession`. Owns the single
  consumer of `session.IncomingMessages.ReadAllAsync(...)`.
- Subscribers register a handler keyed by frame `Command`
  (`P2pCommands.GetData`, `P2pCommands.Tx`, `P2pCommands.Inv`,
  `P2pCommands.Reject`, …). Subscribers may register multiple
  handlers per command; the dispatcher fan-outs each frame to all
  matching handlers sequentially.
- API surface:

```csharp
public sealed class PerSessionFrameDispatcher
{
    public PerSessionFrameDispatcher(PeerSession session, ILogger logger);

    // Audit followup new-M1: subscriberTag is required and used in
    // error log attribution + race-regression test assertions.
    // Each invocation runs inside per-subscriber try/catch (see
    // failure-isolation block below); a throwing handler does not
    // stop RunAsync nor prevent later subscribers from firing.
    public IDisposable Subscribe(
        string command,
        string subscriberTag,
        Func<InboundFrame, Task> handler);

    public Task RunAsync(CancellationToken ct); // single reader loop
}
```

- Owner (`PerSessionDispatcherRegistry` — also new in this slice)
  creates one dispatcher per Ready session and starts its
  `RunAsync` task; tears down on session completion.

#### Subscriber failure-isolation semantics (audit W2 followup new-M1)

The dispatcher is the shared delivery path for `TxRelayCoordinator`
AND `MempoolWatcher` (and future consumers). One handler throwing
must not break the others or stop `RunAsync` — that would just
swap the channel-race risk for a cross-subscriber coupling risk.

Required dispatcher behaviour:

- **Per-subscriber try/catch.** The dispatcher wraps every
  `handler(frame)` invocation in `try { await handler(frame); }
  catch (Exception ex) { logger.LogWarning(ex, ...); }`. The
  exception is logged with the handler's command + a stable
  subscriber-id (a short tag passed at `Subscribe()` time, e.g.
  `"tx-relay"`, `"mempool-watcher.tx"`).
- **A failed handler does not stop `RunAsync`.** The outer read
  loop continues to the next frame. The only conditions that end
  `RunAsync` are: the channel completing (session disconnect) or
  `ct` being cancelled.
- **A failed handler does not block later subscribers.** Fan-out
  is sequential and each subscriber invocation is independent;
  the next subscriber gets called regardless of the prior one's
  outcome.
- **Sequential fan-out is the policy.** Slow handlers do apply
  backpressure to all subscribers on the same session (handler N+1
  doesn't start until handler N completes), which is acceptable
  for W2's tx-frame-handler workload — handlers are expected to
  schedule async work (journal append, telemetry) and return
  quickly. The runner's per-txid handler is one-shot and
  unsubscribes after fire; it should not retain the dispatcher
  for slow work.
- **`Subscribe()` returns an `IDisposable`** whose `Dispose()` is
  idempotent and thread-safe; un-subscription mid-frame is OK
  (the in-flight invocation completes; subsequent frames skip it).
- **Subscribe-time tag.** Update API: `Subscribe(string command,
  string subscriberTag, Func<InboundFrame, Task> handler)`. The
  tag is used in error logs and in race-regression test
  assertions to verify the right handler fired.

### S5.2 — `TxRelayCoordinator` migrates to use the dispatcher

`TxRelayCoordinator.WatchSessionAsync` currently reads
`session.IncomingMessages.ReadAllAsync(...)` directly. Refactor:

- Constructor takes `PerSessionDispatcherRegistry`.
- Instead of starting its own per-session read loop, `AnnounceAsync`
  subscribes handlers via the registry:
  - `P2pCommands.GetData` handler → existing serve-tx logic
  - `P2pCommands.Inv` handler → existing relay-back accounting
  - `P2pCommands.Reject` handler → existing reject logging
- Behavioural parity: no change in what `TxRelayCoordinator` does;
  only **how** it receives frames changes.

### S5.3 — `P2pMempoolIngestRunner`

Add `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs`
(new):

- `IHostedService`. On start, wait for
  `RavenWatchlistLoader.IsLoaded` (matcher warm before any tx
  flow); periodic reconcile (every 5 s) attaches `OnInvReceived`
  to each Ready peer that's not already wired — same pattern as
  `HeadersChainService.Reconcile`. Inv handling uses the **frozen
  S0 callback** (not the dispatcher) because callbacks are
  additive and don't conflict with channel consumers.
- For each inv item with `InvType.Tx`:
  1. Pass to `watcher.OnInv(txid, session)` which dedupes and
     decides whether to fetch.
  2. If fetch needed: subscribe a one-shot `P2pCommands.Tx`
     handler via the dispatcher registry **keyed by txid** (the
     handler filters frames whose parsed tx-hash matches the
     requested txid), then call `session.SendGetDataAsync(...)`.
  3. When the matching `tx` frame arrives, dispatcher fans it to
     the one-shot handler → `watcher.RecordTx(rawHex)` → match →
     journal append (on hit) or recorder increment (on miss).
  4. Handler unsubscribes after fire OR after a `MempoolWatcherOptions.GetDataTimeoutMs`
     timeout (records timeout via recorder).
- DI registration in `BsvP2pSetup`.

### Owned paths (S5)

- `src/Dxs.Consigliere/Services/P2p/PerSessionFrameDispatcher.cs` (new)
- `src/Dxs.Consigliere/Services/P2p/PerSessionDispatcherRegistry.cs` (new)
- `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs` (refactor
  to use the registry — behavioural parity)
- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs` (new)
- `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` (extend — register
  `WatchlistMatcher`, `RavenWatchlistLoader`, `MempoolWatcher`,
  `SourceObservationRecorder`, `PerSessionDispatcherRegistry`,
  `P2pMempoolIngestRunner`)

**Out of scope.** Bootstrap from non-P2P sources. Peer scoring
(W6). Adding new `PeerSession` callbacks (forbidden by W1 S0
freeze).

### Validation (S5)

- `tests/Dxs.Consigliere.Tests/P2p/PerSessionFrameDispatcherTests.cs`
  (new):
  - one subscriber on `P2pCommands.Tx` receives the frame
  - two subscribers on the same command both receive the frame
    (proves fan-out, not channel race)
  - dispose handle stops delivery to that subscriber, other
    subscribers continue
  - **throwing subscriber is isolated** (audit followup new-M1):
    subscriber A throws on first frame; subscriber B is also
    registered on the same command. Send two frames. Assert:
    A got frame 1 (and threw); B got frame 1; both got frame 2;
    `RunAsync` is still alive; error log emitted once with
    subscriber-id `A`.
  - **slow subscriber doesn't crash** but does apply per-session
    backpressure: subscriber A awaits a `TaskCompletionSource`
    before returning. Send frame 1. Subscriber B does not receive
    frame 2 until A's task completes. (Documents the sequential
    fan-out policy; not a defect.)
- **Race regression test** (`TxRelayCoordinator` + mempool
  runner on the same session, runtime-gated):
  - drive a tx via `TxRelayCoordinator.AnnounceAsync` (Gate 3
    path) — relay handlers receive `getdata` + relay-back frames
  - simultaneously drive an inbound `inv(MSG_TX) → tx` for a
    different txid via the mempool runner
  - assert: relay coordinator sees its getdata and relay-back;
    mempool watcher sees the inbound tx. Neither path starves
    the other across 1 K-event fixture.
- `tests/Dxs.Consigliere.Tests/P2p/P2pMempoolIngestRunnerTests.cs`
  (runtime-gated; uses `MiniBsvServer` from `tests/Shared/`):
  - fake peer sends `inv(MSG_TX, txid)` → service issues `getdata`
  - fake peer responds with `tx` → journal recorder sees one
    observation with `source = "p2p"`
  - 1 K-tx fixture run → no exceptions; observation count ≥ 95 %
    of inv count (rate-limit + timeout headroom)
  - getdata-timeout case: peer never replies with the tx → the
    one-shot handler unsubscribes, recorder logs timeout, no leak

**Done when.** All dispatcher, race-regression, and runner tests
green; `TxRelayCoordinator` Gate-3 broadcast tests still green
after the refactor (parity).

## S6 — Bitails / JungleBus source-tag regression pin

`depends_on = S0`.

**Audit W2 M2 reconciliation:** inspection of
`src/Dxs.Consigliere/BackgroundTasks/Realtime/BitailsRealtimeIngestRunner.cs`
(lines ~149-153, 175-180, 191) and
`src/Dxs.Consigliere/BackgroundTasks/Realtime/JungleBusRealtimeIngestRunner.cs`
(lines ~46-50) shows both runners already construct `TxMessage`
with `TxObservationSource.Bitails` / `TxObservationSource.JungleBus`
respectively and route through the existing `TxObservationJournalWriter.AppendAsync(TxMessage)`
overload. The original draft considered migrating them to the new
source-neutral overload from S0; that would either be churn for
no behaviour change OR drift the dedupe-fingerprint / payload-
reference semantics (the existing `TxMessage` path normalizes
both). **W2 keeps the existing runner path unchanged.** S6 is
therefore a **regression-pin** slice only — no production-code
edits to the runners.

**Behaviour.**

- No edits to `BitailsRealtimeIngestRunner.cs` or
  `JungleBusRealtimeIngestRunner.cs`.
- Add regression-test assertions to the existing runner test
  files that every captured `TxMessage` has the expected
  `Source` constant. This catches a future refactor that might
  drop the tag.

**Owned paths.**

- `tests/Dxs.Consigliere.Tests/BackgroundTasks/Realtime/BitailsRealtimeIngestRunnerTests.cs`
  (extend with source-tag assertion)
- `tests/Dxs.Consigliere.Tests/BackgroundTasks/Realtime/JungleBusRealtimeIngestRunnerTests.cs`
  (extend with source-tag assertion)

**Out of scope.** Any migration to the new journal overload.
That can be revisited in a future wave if the two overloads need
consolidation; W2 doesn't need it for the parent program's
`SeenBySources` accumulation requirement (the existing
`TxMessage` path already populates `TxObservation.Source`
correctly, which is what the projection rebuilder reads).

**Validation.**

- existing runner tests still green
- new regression-test assertion: every captured `TxMessage.Source`
  is the expected constant

**Done when.** Both runner test suites green with the tagging
assertions in place. PR diff for S6 touches only the two test
files.

## S7 — Watchlist correctness fixture suite + microbenchmark

`depends_on = S0, S1, S2, S3`.

**Intent.** First-class validation of the watchlist matcher across
the scenarios called out by the program §Mandatory second slice
(parent `slices.md` §Wave 2).

**Fixtures.**

- address-output (P2PKH): tx pays a watched address → match
- address-input (spending tx): scriptSig contains `<sig> <pubkey>`;
  matcher computes `HASH160(pubkey)` and matches against watched
  hash → match (per S1 H3 fix — input matching is pubkey-derived,
  not locking-script-shaped)
- STAS token output: tx output declares watched `TokenId` → match
- DSTAS token output: same with DSTAS encoding → match
- delete-during-observation: address watched, observed, then
  removed mid-stream via Raven Changes API (S3) → next tx with
  that address is `None`
- 8-byte prefix collision: two distinct hash160 sharing the
  prefix; exactly one is watched; the unwatched tx returns `None`

### Reproducibility block (audit W2 M1)

The 500 K-load + p99 ≤ 100 ns targets are sensitive to runtime,
hardware, BenchmarkDotNet job shape, and GC mode. The benchmark
must lock these down so results are comparable across operators.

**Required runtime / harness assumptions:**

| Knob | Value |
|---|---|
| .NET SDK | 9.0.x (matches solution `TargetFramework=net9.0`) |
| Build configuration | `Release` |
| OS reference class | Linux x64 (Ubuntu 24.04 LTS) or macOS arm64 (M-series); host metadata recorded in the report |
| CPU reference class | DigitalOcean Premium AMD (1 vCPU, 2 GiB RAM) — same as existing Gate 2 soak runbook |
| BenchmarkDotNet job | `[SimpleJob(RuntimeMoniker.Net90, warmupCount: 5, iterationCount: 15)]` |
| GC mode | server GC, concurrent (default for benchmarks) |
| Process isolation | BenchmarkDotNet `InProcessNoEmitToolchain` not used — out-of-proc jobs only |
| Corpus seed | hard-coded `Random(seed: 42)` for the 500 K address generation so the corpus is byte-identical across runs |
| Corpus shape | 500 K addresses uniformly random over the hash160 space; 5 K watched-token entries with random `TokenId` 32-byte hashes |

**Measurement phases (split per audit W2 M1):**

- **Load benchmark** measures only the in-memory matcher
  construction from a pre-materialized list of (address, hash160)
  tuples — does NOT include Raven I/O. Raven-I/O latency is
  measured separately by S3's `RavenWatchlistLoader` integration
  test, which records its own wall-clock in
  `evidence/watchlist-load-bench.md` (Raven I/O + matcher build
  combined).
- **Lookup hot-path benchmark** measures the `Match(parsedTx)`
  call on a parsed tx (matcher work only — script parsing is
  excluded by feeding a pre-parsed `ParsedTx`).

**Pass thresholds (canonical):**

- matcher construction (in-memory only): wall-clock ≤ 2 s on the
  reference CPU class
- matcher lookup hot path: p99 ≤ 100 ns (linear-interpolation
  quantile, BenchmarkDotNet `NoiseThreshold` cleared)
- below the reference CPU class: the absolute thresholds are
  best-effort. The report records the ratio against the
  reference (e.g. "0.6× reference" for a slower CPU) and a
  qualitative pass when within 2× of the reference.

**Owned paths.**

- `tests/Dxs.Bsv.Tests/P2p/Observer/WatchlistFixtureSuiteTests.cs`
  (extends `WatchlistMatcherTests` with the full fixture suite)
- `tests/Dxs.Consigliere.Benchmarks/WatchlistMatcherBench.cs` (new —
  BenchmarkDotNet microbenchmark; mirrors existing bench csproj
  pattern)
- `tests/Dxs.Consigliere.Benchmarks/WatchlistCorpus.cs` (new —
  deterministic 500 K corpus generator seeded `Random(42)`; emits
  the same byte stream on every run for cross-host comparability)

**Out of scope.** Multi-tenant watchlist scaling. Bloom-filter
comparison.

**Validation.**

- every fixture scenario green
- microbenchmark report `evidence/watchlist-bench.md` records:
  - host metadata (OS, arch, CPU model, .NET runtime version)
  - corpus seed + size
  - construction wall-clock
  - lookup p50 / p95 / p99 latency
  - pass verdict against the reference thresholds

**Done when.** Fixture suite + bench produce green results; bench
report committed with all required fields populated.

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
6. Record evidence in `evidence/live-validation.md` per the
   **required-fields schema** below.

### Required evidence schema (audit W2 M5)

The evidence file must contain enough correlation data to verify
the full path `inv → getdata → tx → match → journal → projection →
hub` from the file alone, without re-reading logs:

| Field | Source | Why |
|---|---|---|
| `txid` | observed mempool tx | identifies the validation subject |
| `watched_address` | `WatchingAddress.Address` | identifies the matched entry |
| `commit_sha` | `git rev-parse HEAD` at the run | reproducibility anchor |
| `node_config_excerpt` | `Consigliere:Broadcast:P2p:*` + `Consigliere:Watchlist:*` | reproducibility of runtime knobs |
| `peer_count_at_inv` | `BsvP2pHealth.PoolSize` snapshot | proves pool was healthy |
| `peer_endpoint_inv_source` | log line `[P2pMempoolIngestRunner] inv(MSG_TX,<txid>) from <ip:port>` | proves the source peer |
| `inv_arrival_ts_utc_ms` | log timestamp on the inv line | left-edge of the latency calculation |
| `getdata_ts_utc_ms` | log line `[P2pMempoolIngestRunner] getdata(<txid>) → <peer>` | proves we asked for the payload |
| `tx_receipt_ts_utc_ms` | log line `[PerSessionFrameDispatcher] tx <txid> from <peer>` | proves payload arrived |
| `match_ts_utc_ms` | log line `[MempoolWatcher] matched <txid> → <watched_address>` | proves matcher hit |
| `journal_append_ts_utc_ms` | log line `[TxObservationJournalWriter] appended <txid> source=p2p` | proves write to journal |
| `projection_doc_id` | `TxLifecycleProjectionDocument.Id` | identifies the projection record |
| `projection_last_seq` | projection document `@change-vector` or sequence | proves freshness |
| `hub_fire_ts_utc_ms` | client-side SignalR log of `OnTransactionFound` | proves the hub event |
| `inv_to_hub_delta_ms` | `hub_fire_ts_utc_ms - inv_arrival_ts_utc_ms` | the headline latency number |
| `payload_available` | bool — `TxLifecycleProjectionDocument.PayloadAvailable` | proves raw bytes persisted |
| `raw_payload_reference` | `RawTransactionPayloadReference.Id` or null | proves storage path |
| `seen_by_sources` | `TxLifecycleProjectionDocument.SeenBySources` final array | proves the projection state |
| `bitails_also_observed` | bool — `"bitails" ∈ SeenBySources` | proves W2 doesn't break the existing runner |
| `junglebus_also_observed` | bool — `"junglebus" ∈ SeenBySources` | proves W2 doesn't break the other existing runner |

**Pass condition:** `inv_to_hub_delta_ms ≤ 2000` AND
`seen_by_sources` contains `"p2p"` AND `payload_available == true`.

**Owned paths.**

- `docs/stream-tasks/bsv-mempool-observer-wave/evidence/live-validation.md`
  (new — written by the operator at the time of the run, populated
  per the schema above)

**Out of scope.** A standalone soak harness for S8 unless the
in-process Consigliere run can't produce the evidence cleanly
(then optionally fall back to `tests/Spikes/P2p/MempoolWatcherSoak/`).

**Validation.**

- `evidence/live-validation.md` exists with **all** required fields
  populated. Missing-field policy: a field may be recorded as
  `n/a` only if accompanied by a one-line justification (e.g.
  `bitails_also_observed: n/a — Bitails runner disabled this run`).
- pass condition verified.

**Done when.** Live evidence file committed with the full schema,
OR the wave's closeout explicitly notes S8 deferred to a separate
operator session with a reason and a date for the follow-up.

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
