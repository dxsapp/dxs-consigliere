# Consigliere Thin-Node Observer Program — Wave Decomposition

Revised 2026-05-16 per audit A1.

This file describes each wave at the level needed for the program-level
audit and for ordering. Per-slice decomposition lives in each child wave
package's `slices.md`.

## Program Overview

Six sequential waves grow the existing thin-node broadcaster (Gate 1–3)
into a full observer + reorg-aware ingestion + unified broadcast +
production-grade operations. The existing journal-based ingestion
pipeline (`TxObservationJournalWriter` →
`TxLifecycleProjectionDocument`) is the integration spine: every wave
either feeds it (W1, W2), reacts to it (W3, W4), unifies the write-side
that produces it (W5), or hardens the operational layer around it (W6).

Wave 1 opens with a **contract-freeze slice** that pre-declares every
hub event and `PeerSession` extension point downstream waves require,
so subsequent waves only implement against frozen surfaces. Wave 2
opens with a **journal-contract extension slice** that adds
`TxObservationSource.P2p` and a source-neutral append overload before
any P2P observation code lands.

## Wave-by-wave decomposition

### Wave 1 — `bsv-headers-chain-wave`

**Intent.** Pre-declare program-wide hub and session contracts, then
track the active BSV chain tip and the last ~200 headers purely from
P2P (`inv(MSG_BLOCK)` and `headers`). Expose new-tip events through
`WalletHub`. Drop block-body fetching into the existing provider
clients (Bitails / JungleBus) so the body is independently validated
against the header.

**Owned paths.**
- `src/Dxs.Bsv/P2p/Chain/` (new)
- `src/Dxs.Bsv/P2p/Session/PeerSession.cs` — add **the complete**
  set of observation callbacks and the telemetry sink per Core Rule §7
  in `master.md`:
  - `OnHeadersReceived(IReadOnlyList<BlockHeader>)` (W1 uses)
  - `OnInvReceived(InvMessage)` — single typed callback for both tx
    and block inv items; consumers filter by `InvType`. (Replaces
    earlier inconsistent `OnBlockInvReceived` / `OnInvReceived(tx)`
    split flagged by audit A2.)
  - `OnRejectReceived(RejectMessage)` (W2 reject-class quorum;
    W6 reject-rate scoring)
  - `PeerTelemetry` snapshot type + `IPeerTelemetrySink` interface
    (W4 reads aggregates, W6 implements production sink).
  Bodies may stub to no-ops; signatures, dispatch, and telemetry
  field set are locked in this wave.
- `src/Dxs.Consigliere/Services/P2p/HeadersChainService.cs` (new)
- `src/Dxs.Consigliere/Data/P2p/BlockHeaderStore.cs` (new)
- `src/Dxs.Consigliere/Data/Models/P2p/BlockHeaderDocument.cs` (new)
- `src/Dxs.Consigliere/WebSockets/IWalletHub.cs` — add **all** hub
  events for W1, W2, W3, W5: `OnNewBlock`, `OnReorg`, finalised
  `Broadcast` receipt signature documented in the comments. W2/W3/W5
  do not add hub methods; they implement against the frozen surface.
- `src/Dxs.Consigliere/WebSockets/WalletHub.cs` — add subscription
  group names for `block:tip`, `block:reorg`, plus `Broadcast` updated
  signature stub.
- `src/Dxs.Consigliere/Controllers/AdminP2pController.cs` (+ /headers)
- `tests/Dxs.Bsv.Tests/P2p/Chain/` (new)

**Out of scope.** Reorg-recovery (orphan rescan, tx revert) — that is
Wave 3. Wave 1 only **detects** the divergence by storing competing
header tips; recovery is built later. Token watchlist matching is W2.

**Validation signal.**
- Initial header sync from Bitails REST + transition to P2P-only mode
  completes within 10 s of startup on a fresh VPS.
- Over a **24 h soak harness** described below: every block observed
  by WhatsOnChain on mainnet also produces a `WalletHub.OnNewBlock`
  event with p95 lag ≤ 2 s.
- Admin endpoint `GET /api/admin/p2p/headers/tip` returns a hash that
  matches the WhatsOnChain block explorer for the same height.

**Benchmark harness for the p95-lag claim.** A `HeadersSoakRecorder`
hosted service runs alongside the headers chain service during the
24 h soak. For each `OnNewBlock` it timestamps `now_recv_utc` and the
block's header `timestamp` field. In parallel, the harness polls
WhatsOnChain's `/v1/bsv/main/chain/info` endpoint at 1 s cadence; on
each height change it records `now_explorer_utc`. End of soak: lag per
block = `now_recv_utc - now_explorer_utc`; p95 is computed over the
soak window. Hardware/runtime assumption is the same fresh
DigitalOcean droplet class used in the existing Gate 2 soak runbook
(`docs/platform-api/thin-node-gate2-soak-runbook.md`). The harness
itself is a throwaway recorder under `tests/Spikes/P2p/` and does not
ship in the wave's production artifacts.

**Completion signal.** Contract-freeze slice closed; all ledger items
closed; `dotnet test` green; admin endpoint matches WhatsOnChain tip
across the soak window.

### Wave 2 — `bsv-mempool-observer-wave`

**Intent.** Make the P2P pool an ingest source equal to Bitails/JBus
realtime. Every `inv(MSG_TX)` from a connected peer triggers a `getdata`
fetch (rate-limited, deduplicated) and append-to-journal with
`SeenBySources += "p2p"`. Watchlist matcher filters which tx we
serialize raw — un-watched tx are seen but not stored raw (saves Raven
space).

**Mandatory first slice — Journal contract extension.**
The existing `TxObservationJournalWriter.AppendAsync(TxMessage)` is
coupled to Bitails ingest shape (`TxObservationJournalWriter.cs`
lines 21–35). `TxObservationSource` constants are only `Node`,
`JungleBus`, `Bitails` (`TxObservation.cs` lines 12–17). Before any
observer code lands:
1. Add `TxObservationSource.P2p` constant.
2. Add `AppendAsync(TxObservation observation, RawTransactionPayloadReference? payload, string source)` overload (or a source-neutral DTO).
3. Add a projection rebuild test that proves `SeenBySources` includes
   `p2p` after replaying a journal that contains a P2P observation
   followed by a Bitails observation for the same txid.

**Mandatory second slice — Watchlist correctness + scale.**
`WatchingAddress` stores `{Name, Address}` only — no precomputed
`Hash160` (`WatchingAddress.cs` lines 3–8). Existing matching uses
`TransactionFilterWatchSet` which parses full tx (lines 49–94). The
Bitails realtime scope provider falls back to all-tx when tokens are
present (`BitailsRealtimeSubscriptionScopeProvider.cs` lines 15–39).
Validation covers:
- address-output match (P2PKH)
- address-input match (spending tx)
- token-output match (STAS / DSTAS)
- removal of address while observer running (no false matches on
  next tx)
- 500 K-address load benchmark (≤ 2 s startup load measured wall-clock
  from `RavenWatchlistLoader` start to `WatchlistMatcher.Loaded = true`).
  Hot-path lookup latency target: **p99 ≤ 100 ns** in a BenchmarkDotNet
  microbenchmark over the matcher only (no parsing). The earlier
  `≤ 50 ns hot path` figure in draft text was a single-operation
  mean estimate — superseded; canonical target is p99 ≤ 100 ns,
  reconciled with the program validation matrix (`Validation matrix`
  row "Watchlist scale").
- collision behaviour on `HashSet<ulong>` 8-byte prefix (full hash160
  verify catches; no false positive escape)

**Owned paths.**
- `src/Dxs.Bsv/P2p/Observer/` (new) — `MempoolWatcher`,
  `WatchlistMatcher`, `TxScriptParser` (reuse parts of `Dxs.Bsv.Script`)
- `src/Dxs.Bsv/P2p/Session/PeerSession.cs` — implement
  `OnInvReceived` body (surface already frozen in W1)
- `src/Dxs.Consigliere/Services/P2p/SourceObservationRecorder.cs`
  (new) — Consigliere-orchestration counter for "bitails" /
  "junglebus" / "p2p" source tags (audit W2 M3 reconciliation;
  moved here from `src/Dxs.Bsv/P2p/Observer/` because source
  labels are Consigliere-level, not BSV-protocol-level)
- `src/Dxs.Consigliere/Services/P2p/PerSessionFrameDispatcher.cs`
  + `PerSessionDispatcherRegistry.cs` (new) — owns the single
  consumer of `PeerSession.IncomingMessages` and fans frames to
  multiple subscribers; `TxRelayCoordinator` refactors to consume
  via this. Audit W2 H1 fix.
- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs` (new) —
  `IHostedService`, parallel to `BitailsRealtimeIngestRunner`
- `src/Dxs.Consigliere/Services/P2p/RavenWatchlistLoader.cs` (new) —
  initial load + **Raven Changes API** hot-reload of
  `WatchingAddress` / `WatchingToken` into the in-memory matcher
  (Raven Subscription API was the initial draft; replaced with
  Changes API per audit W2 H2 — matches the existing
  `StasAttributesChangeObserverTask` pattern)
- `src/Dxs.Bsv/BitcoinMonitor/Models/TxObservation.cs` — add `P2p`
  source constant
- `src/Dxs.Consigliere/BackgroundTasks/TxObservationJournalWriter.cs`
  — add source-neutral append overload
- `src/Dxs.Consigliere/BackgroundTasks/Realtime/*` — Bitails/JBus
  runner production code stays **unchanged** in W2; both runners
  already construct `TxMessage` with the correct
  `TxObservationSource.Bitails` / `TxObservationSource.JungleBus`
  constants. W2 only adds source-tag regression assertions to
  the existing runner test files (audit W2 M2 reconciliation)

**Out of scope.** Per-source metrics dashboard (W4). Reorg state
transitions (W3). Watchlist UI management (already exists in
`AdminTrackedController` — we just read from there).

**Validation signal.**
- Fixture journal replay shows `SeenBySources` containing `p2p` plus
  either `bitails` or `junglebus` (race acceptable, both seen counted).
- Real mainnet: tx paying a watched address produces
  `WalletHub.OnTransactionFound` within 2 s, raw bytes persisted via
  `RawTransactionPayloadStore`.
- Watchlist scale benchmark: 500 K addresses load within 2 s; lookup
  pass-rate is 100 % across a fixture of 10 K txs touching a random
  10 % of the watchlist.

### Wave 3 — `reorg-handling-wave`

**Intent.** When the chain tip from Wave 1 diverges from prior history,
detect the common ancestor, emit a reorg event, rescan orphaned blocks
through the provider, append `BlockDisconnected` observations so the
existing rebuilder moves affected tx into the `Reorged` lifecycle
state, **and actively re-broadcast every affected tx for which we
hold raw bytes** so the tx re-enters the network mempool. The
subsequent mempool observation flips the projection forward to
`SeenInMempool`.

**Critical change vs original draft.** Wave 3 reuses the existing
projection semantics. `TxLifecycleProjectionRebuilder.cs` already
handles `BlockDisconnected` events (lines ~198–227), producing
`LifecycleStatus = Reorged`, `SeenInMempool = null`, `BlockHash = null`,
`BlockHeight = null`. Wave 3's job is to **emit the right journal events**
from a P2P-detected reorg, not invent new lifecycle states.

**Beyond-window divergence.** When a fork goes deeper than the retained
header window (default 200 blocks), the rebuilder cannot replay enough
context. Wave 3 introduces an explicit `DegradedReorgState` (admin
panel banner + alert in W6) that requires operator action. No silent
data loss.

**Owned paths.**
- `src/Dxs.Bsv/P2p/Chain/ReorgDetector.cs` (new; algorithm lives here)
- `src/Dxs.Consigliere/Services/P2p/ReorgHandlerService.cs` (new) — owns
  the rescan loop, provider-content fetch + header validation, journal
  translator, **post-reorg re-broadcast pass**
- `src/Dxs.Consigliere/WebSockets/WalletHub.cs` — implement `OnReorg`
  body (surface already frozen in W1)
- `src/Dxs.Consigliere/Data/Models/P2p/` — optional sidecar doc for
  `RebroadcastSkippedReason` if we don't extend the projection itself
- `tests/Dxs.Bsv.Tests/P2p/Chain/ReorgDetectorTests.cs` (new)
- `tests/Dxs.Consigliere.Tests/P2p/ReorgHandlerServiceTests.cs` (new)

**Re-broadcast pass design.** After the `BlockDisconnected` journal
events have been appended for an orphaned block (and the projection
has caught up to `Reorged` for all affected tx), the
`ReorgHandlerService` collects the affected txids and resolves raw
bytes in this order:
1. `OutgoingTransactionStore` — own outgoing tx have raw stored from
   the original submit. Always preferred when present.
2. `RawTransactionPayloadStore` — observed-watchlist tx with raw
   persisted by the journal (when matched, see Wave 2).
3. Otherwise, mark `RebroadcastSkippedReason = "no_raw"` and proceed.

For every tx with raw resolved, send `inv(MSG_TX, txid)` to all
`PeerSession`s in `Ready` state. The existing `TxRelayCoordinator`
serve-getdata path (Gate 3) handles the rest: peers `getdata`, we
hand them the raw, they accept and relay back. Each subsequent
`SeenInMempool` observation moves the projection forward as designed.

**Out of scope.** Wallet/account-level balance recompute (already part
of projection downstream). Heavy historical rescan beyond 200-block
window (degraded-state alert instead). Re-broadcast for tx where we
have no raw bytes — best-effort only, logged.

**Validation signal.**
- 1-deep fork test: orphan one block, expect each tx in the orphaned
  block to transition to `Reorged` exactly once (idempotent on repeated
  `BlockDisconnected` events).
- 2-deep fork test: same with two orphaned blocks.
- N-deep fork test where N ≤ 200: assert all affected tx reach
  `Reorged` within rescan completion; no duplicates.
- Deep-reorg test where N > 200: assert `DegradedReorgState` raised,
  no journal corruption, operator-facing error logged.
- Provider-mismatch test: provider returns a body whose merkle root
  disagrees with the orphaned header. Handler refuses, alerts, retains
  prior state.
- Replay test: replay journal from zero on a fresh Raven, observe
  identical end-state for affected tx.
- **Re-broadcast (own outgoing)**: insert a fixture own-outgoing tx
  whose state was advanced to `Mined`. Trigger 1-deep reorg orphaning
  that block. Assert `inv(MSG_TX, txid)` was sent on every ready
  `PeerSession`, the mock peer's `getdata` reply received the same
  raw bytes back, and no `RebroadcastSkippedReason` recorded.
- **Re-broadcast (observed watchlist)**: same, but the affected tx
  was observed via watchlist match with raw persisted to
  `RawTransactionPayloadStore`. Assert same announce-on-all-peers
  behaviour.
- **No-raw best-effort**: tx affected by reorg with no raw resolvable
  in either store. Assert `RebroadcastSkippedReason = "no_raw"`
  recorded; no announce attempted; warning logged.

### Wave 4 — `observation-source-metrics-wave`

**Intent.** Capture per-source stats every time **any** source sees a
tx (P2P, Bitails realtime, JungleBus realtime), before dedupe. Persist
as a small Raven counter document keyed by `(source, day)`. Admin API
exposes aggregated metrics; admin SPA shows the dashboard.

**Owned paths.**
- `src/Dxs.Consigliere/Services/P2p/SourceMetricsRecorder.cs` (new)
- `src/Dxs.Consigliere/Data/P2p/SourceMetricsStore.cs` (new)
- `src/Dxs.Consigliere/Data/Models/P2p/SourceMetricsDocument.cs` (new)
- `src/Dxs.Consigliere/Controllers/AdminObservationController.cs` (new)
- `src/admin-ui/src/...` — new page under admin SPA (sub-route TBD per
  existing admin-ui structure)

**Out of scope.** Long-term storage / retention policy. Initial release
keeps last 30 days rolling; older counters get aggregated nightly.

**Validation signal.**
- Fixture: inject 1 000 observations across three sources with known
  timing. Counter document values match injected fixture counts
  **exactly** (not "plausibly fire").
- Lag histogram bucket counts are deterministic for the fixture.
- Admin endpoint returns the same aggregates the fixture asserts on.

### Wave 5 — `broadcast-unification-wave`

**Intent.** Collapse `Broadcast(hex)` and `BroadcastTracked(hex)` into
a single hub method `Broadcast(hex) → BroadcastReceiptDto`. Internally
unified through P2P-first `SubmitAsync`. Delete the HTTP-provider
broadcast paths (`BitcoindService.Broadcast`,
`BitailsRestApiClient.Broadcast`, `WhatsOnChainRestApiClient.BroadcastAsync`).

**Hard depends on W2.** The unified broadcast lifecycle reads source
attribution from the journal projection. Without W2's journal contract
extension and `TxObservationSource.P2p`, the unified path has no clean
source attribution for transitions like `PeerAcked → MempoolSeen`.

**Hard depends on W4.** Removing HTTP fallback without operator
metrics in place is a regression risk. W4 must be live so the
operator sees source health when W5 turns off the legacy provider
broadcast.

**Owned paths.**
- `src/Dxs.Consigliere/WebSockets/{IWalletHub.cs,WalletHub.cs}` —
  implement against the frozen `Broadcast` signature from W1
- `src/Dxs.Consigliere/Services/{IBroadcastService.cs,Impl/BroadcastService.cs}`
- `src/Dxs.Consigliere/Services/Impl/BitcoindService.cs` — remove
  `Broadcast` method; the service may stay if it does anything else
  (audit per-slice)
- `src/Dxs.Infrastructure/Bitails/BitailsRestApiClient.cs` — remove
  broadcast endpoint usage
- `src/Dxs.Infrastructure/WoC/WhatsOnChainRestApiClient.cs` — same
- `src/Dxs.Consigliere/Data/Models/Broadcast.cs` — keep as historical
  audit, but stop new writes; document the freeze
- minor admin-ui adjustments if any UI references legacy broadcast

**Out of scope.** Persistent `Broadcast` document migration. We freeze
the table; old records remain accessible. Public-API change notes
themselves are produced in W6.

**Validation signal.**
- Grep across the solution returns zero hits for
  `BitcoindService.Broadcast`, `BitailsRestApiClient.Broadcast`,
  `WhatsOnChainRestApiClient.BroadcastAsync`.
- SignalR `Broadcast(hex)` produces a receipt with `txid` and `state`
  immediately; raw tx confirmed mined within usual mainnet block time
  during a smoke test.
- Admin source metrics still show observations during and after the
  smoke test (proves W4 stayed healthy during the cut-over).

### Wave 6 — `production-ops-wave`

**Intent.** Cover production-operations scope that earlier waves
intentionally defer: peer scoring + rotation, critical alerts, optional
inbound listener integration into Consigliere, operator runbook,
public-API change notes for `Broadcast`.

**Owned paths.**
- `src/Dxs.Bsv/P2p/Pool/PeerManager.cs` — peer scoring fields, rotation
  policy, eviction thresholds
- `src/Dxs.Bsv/P2p/Pool/PeerRecord.cs` — score, banscore, last-active
  fields (some already exist; consolidate)
- `src/Dxs.Consigliere/Services/P2p/PeerScoringService.cs` (new) —
  periodic scoring + rotation loop
- `src/Dxs.Consigliere/Services/P2p/AlertPollerService.cs` (new) —
  evaluates thresholds, emits alerts via configurable backend
- `src/Dxs.Consigliere/Services/P2p/InboundListenerService.cs` (new;
  opt-in) — wires the `tools/BsvBroadcastNode` listener pattern into
  the main Consigliere process
- `src/Dxs.Consigliere/Controllers/AdminAlertsController.cs` (new)
- `src/admin-ui/src/...` — alerts panel
- `docs/platform-api/broadcast-change-notes-vnext.md` (new) — public
  API change documentation
- `docs/platform-api/thin-node-operator-runbook.md` (new) — covers
  start-up, soak, alert response, reorg degraded-state recovery
- `tests/Dxs.Consigliere.Tests/P2p/PeerScoringServiceTests.cs` (new)

**Validation signal.**
- Peer scoring fixture: low-latency peer ranked above timeout-prone
  peer; rotation evicts the worst peer after N consecutive failures.
- Alert fixture: pool size drops below threshold → `AlertPoller`
  records an alert event in the admin endpoint within one tick.
- Inbound listener fixture: when enabled, accepts a loopback inbound
  handshake from a MiniBsvServer, recorded in admin endpoint.
- Runbook reviewed by operator (human signal).
- Change notes reviewed by API-consumer signal (or self-review with
  diff against current `WalletHub` API).

## Dependency chain

```
                              ┌────────────────────────┐
                              │ Wave 1: Headers chain  │
                              │ (incl. contract freeze)│
                              └──────┬─────────────────┘
                                     │
                          ┌──────────┘
                          │
                          ▼
                  ┌──────────────────┐
                  │ Wave 2: Mempool  │
                  │ observer (incl.  │
                  │ journal extn)    │
                  └─┬──────┬─────┬───┘
                    │      │     │
                    ▼      ▼     ▼
               ┌────────┐ ┌────────────┐ ┌──────────────┐
               │Wave 3: │ │Wave 4:     │ │Wave 6:       │
               │Reorg   │ │Source      │ │Production    │
               │handling│ │metrics+UI  │ │ops           │
               └────────┘ └─────┬──────┘ └──────────────┘
                                │
                                ▼
                        ┌───────────────────┐
                        │ Wave 5: Broadcast │
                        │ unification       │
                        └───────────────────┘
```

Default sequential order under the user's stop-and-audit rule:
**W1 → W2 → W3 → W4 → W5 → W6** (alternative: W6 before W5 if operator
wants alerts/peer scoring live before legacy broadcast removal — keep
default unless explicit request).

## Validation matrix (program-level)

| signal | wave | how validated |
|---|---|---|
| P2P-only header sync to mainnet tip | W1 | admin endpoint `GET /api/admin/p2p/headers/tip` matches WhatsOnChain over 24h soak |
| Journal accepts P2P source | W2 | unit test on `TxObservationSource.P2p` + projection rebuild shows `SeenBySources` containing `p2p` |
| Watchlist scale | W2 | 500 K-address load benchmark ≤ 2 s; lookup p99 ≤ 100 ns |
| P2P observed tx with raw bytes for watchlist match | W2 | send tx to watched address; observe `OnTransactionFound` with raw in the journal payload reference |
| Reorg handling produces `Reorged` state | W3 | 1/2/N-depth loopback fork tests in `Dxs.Bsv.Tests` |
| Re-broadcast after reorg flips affected tx back into mempool | W3 | fixture-injected orphan + assert `inv` announce on every ready peer for both own-outgoing and observed-watchlist tx; `RebroadcastSkippedReason` recorded for no-raw case |
| Deep reorg degraded state | W3 | beyond-window fork test asserts `DegradedReorgState` alert |
| Provider body validation | W3 | mismatch-merkle-root test refuses provider response |
| Per-source metrics exact | W4 | fixture-injected observations match counter values exactly |
| Single `Broadcast` path | W5 | grep confirms legacy paths deleted; integration test broadcasts real mainnet tx |
| Peer scoring + rotation | W6 | low-latency peer outranks timeout peer in fixture; rotation evicts worst peer |
| Alert firing | W6 | pool-size drop fixture triggers alert event |
| Build green | every wave | `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors |
| Tests green | every wave | `dotnet test` returns no new failures vs baseline (delta = residual) |

## Closeout and audit rules

- Each wave: produce its own `audits/A1.md` after execution. If A1
  surfaces residuals that need a fix-and-re-audit pass, open `A2.md` —
  otherwise stop at A1.
- Each wave: produce `evidence/closeout.md` recording delivery hashes
  and behavioural summary.
- Program closeout: only when all six waves are `done` (or
  intentionally `not_opened` with rationale). Program `evidence/closeout.md`
  summarises end-state across waves.
- Stop condition between waves: human review + Codex wave-level audit
  pass required before opening the next wave.
