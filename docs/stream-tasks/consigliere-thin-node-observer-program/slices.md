# Consigliere Thin-Node Observer Program — Wave Decomposition

This file describes each wave at the level needed for the program-level
audit and for ordering. Per-slice decomposition lives in each child wave
package's `slices.md`.

## Program Overview

Five sequential waves grow the existing thin-node broadcaster (Gate 1–3)
into a full observer + reorg-aware ingestion + unified broadcast. The
existing journal-based ingestion pipeline (`TxObservationJournalWriter`
→ `TxLifecycleProjectionDocument`) is the integration spine: every wave
either feeds it (W1, W2), reacts to it (W3, W4), or unifies the
write-side that produces it (W5).

## Wave-by-wave decomposition

### Wave 1 — `bsv-headers-chain-wave`

**Intent.** Track the active BSV chain tip and the last ~200 headers
purely from P2P (`inv(MSG_BLOCK)` and `headers`). Expose new-tip events
through `WalletHub`. Drop block-body fetching into the existing provider
clients (Bitails / JungleBus) so the body is independently validated
against the header.

**Owned paths.**
- `src/Dxs.Bsv/P2p/Chain/` (new)
- `src/Dxs.Bsv/P2p/Session/PeerSession.cs` — add `OnHeadersReceived`,
  `OnBlockInvReceived` callbacks
- `src/Dxs.Consigliere/Services/P2p/HeadersChainService.cs` (new)
- `src/Dxs.Consigliere/Data/P2p/BlockHeaderStore.cs` (new)
- `src/Dxs.Consigliere/Data/Models/P2p/BlockHeaderDocument.cs` (new)
- `src/Dxs.Consigliere/WebSockets/IWalletHub.cs` (+ `OnNewBlock` event)
- `src/Dxs.Consigliere/WebSockets/WalletHub.cs` (+ subscribe method)
- `src/Dxs.Consigliere/Controllers/AdminP2pController.cs` (+ /headers)
- `tests/Dxs.Bsv.Tests/P2p/Chain/` (new)

**Out of scope.** Reorg-recovery (orphan rescan, tx revert) — that is
Wave 3. Wave 1 only **detects** the divergence by storing competing
header tips; recovery is built later.

**Validation signal.** From the existing pruned VPS, broadcaster syncs
~200 headers within 10s on startup; produces a `WalletHub.OnNewBlock`
event within ~1s of every real BSV mainnet block.

**Completion signal.** All ledger items closed; `dotnet test` green;
admin endpoint shows current tip matching WhatsOnChain block explorer.

### Wave 2 — `bsv-mempool-observer-wave`

**Intent.** Make the P2P pool an ingest source equal to Bitails/JBus
realtime. Every `inv(MSG_TX)` from a connected peer triggers a `getdata`
fetch (rate-limited, deduplicated) and append-to-journal with
`SeenBySources += "p2p"`. Watchlist matcher filters which tx we
actually serialize raw — un-watched tx are seen but not stored raw
(saves Raven space).

**Owned paths.**
- `src/Dxs.Bsv/P2p/Observer/` (new) — `MempoolWatcher`, `WatchlistMatcher`,
  `SourceObservationRecorder`, `TxScriptParser` (reuse parts of
  `Dxs.Bsv.Script`)
- `src/Dxs.Bsv/P2p/Session/PeerSession.cs` — add `OnInvReceived(InvMessage)`
  callback (parallel to `OnAddrReceived`); `Wave 1` already touches it,
  this slice extends
- `src/Dxs.Consigliere/Services/P2p/P2pMempoolIngestRunner.cs` (new) —
  `IHostedService` that owns the `MempoolWatcher` and routes parsed
  observations through `TxObservationJournalWriter`
- `src/Dxs.Consigliere/Services/P2p/RavenWatchlistLoader.cs` (new) —
  initial load + Raven Subscription hot-reload of `WatchingAddress` /
  `WatchingToken` into the in-memory `HashSet<ulong>`
- `src/Dxs.Consigliere/BackgroundTasks/Realtime/*` — Bitails/JBus
  runners get a tiny adjustment to tag their observations
  `"bitails"`/`"junglebus"` so source attribution is consistent before
  Wave 4 metrics land

**Out of scope.** Per-source metrics dashboard (W4). Reorg state
transitions (W3). Watchlist UI management (already exists in
`AdminTrackedController` — we just read from there).

**Validation signal.** After watchlist of 1 known address is loaded,
sending a real BSV mainnet tx that pays that address results in a
`WalletHub.OnTransactionFound` event within ~2s, raw bytes persisted
via `RawTransactionPayloadStore`, projection document shows
`SeenBySources` containing `"p2p"` plus either `"bitails"` or
`"junglebus"` (race).

### Wave 3 — `reorg-handling-wave`

**Intent.** When the chain tip from Wave 1 diverges from prior history,
detect the common ancestor, emit a reorg event, rescan orphaned blocks
through the provider, and append journal entries that revert orphaned
tx back to mempool state (`SeenInMempool=true`, `BlockHash=null`,
`BlockHeight=null`). Projection picks it up. Hub fires
`OnTransactionDeleted` for each affected tx and `OnReorg` once.

**Owned paths.**
- `src/Dxs.Bsv/P2p/Chain/ReorgDetector.cs` (new; algorithm lives here)
- `src/Dxs.Consigliere/Services/P2p/ReorgHandlerService.cs` (new) — owns
  the rescan loop and journal-append translation
- `src/Dxs.Consigliere/WebSockets/IWalletHub.cs` (+ `OnReorg`)
- `src/Dxs.Consigliere/WebSockets/WalletHub.cs` (+ subscribe)
- existing `Tx*Projection*` may need a tiny change to recompute on
  reverted entries — flagged before slice work begins

**Out of scope.** Wallet/account-level balance recompute (already part
of projection downstream). Heavy historical rescan beyond reorg window
(≤200 blocks back as per program scope).

**Validation signal.** Forced-fork integration test (loopback peer
serves competing header chain) triggers reorg events without
duplicating tx documents. Real-mainnet validation deferred until a
genuine reorg occurs (rare; relies on manual observation of admin
panel logs over time).

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

**Validation signal.** With all three sources running, the dashboard
shows non-zero `first-seen` percentages for each that should
plausibly fire; lag histogram looks sane (P2P likely fastest under
typical conditions but real numbers tell us).

### Wave 5 — `broadcast-unification-wave`

**Intent.** Collapse `Broadcast(hex)` and `BroadcastTracked(hex)` into
a single hub method `Broadcast(hex) → BroadcastReceiptDto`. Internally
unified through P2P-first `SubmitAsync`. Delete the HTTP-provider
broadcast paths (`BitcoindService.Broadcast`,
`BitailsRestApiClient.Broadcast`, `WhatsOnChainRestApiClient.BroadcastAsync`).

**Owned paths.**
- `src/Dxs.Consigliere/WebSockets/{IWalletHub.cs,WalletHub.cs}`
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
the table; old records remain accessible.

**Validation signal.** SignalR `Broadcast(hex)` produces a receipt
with `txid` and `state` immediately; raw tx confirmed mined within
the usual ~10min mainnet block time during a smoke test.

## Dependency chain

```
                          ┌──────────────────────┐
                          │ Wave 5: Broadcast    │
                          │ unification          │
                          │ (independent)        │
                          └──────────────────────┘

  ┌────────────────────┐
  │ Wave 1: Headers    │────────┐
  │ chain              │        │
  └────────┬───────────┘        │
           │                    │
           │                    ▼
           │           ┌──────────────────┐
           │           │ Wave 3: Reorg    │
           │           │ handling         │
           │           └──────────────────┘
           │                    ▲
           │                    │
           ▼                    │
  ┌────────────────────┐        │
  │ Wave 2: Mempool    │────────┘
  │ observer           │
  └────────┬───────────┘
           │
           ▼
  ┌────────────────────┐
  │ Wave 4: Source     │
  │ metrics + admin    │
  └────────────────────┘
```

Default order under the user's stop-and-audit rule:
**W1 → W2 → W3 → W4 → W5** (W5 can move earlier if convenient — it has
no hard dependency — but we keep it last to avoid touching the
broadcast surface mid-observation development).

## Validation matrix (program-level)

| signal | wave | how validated |
|---|---|---|
| P2P-only header sync to mainnet tip | W1 | admin endpoint `GET /api/admin/p2p/headers/tip` matches WhatsOnChain |
| P2P observed tx with raw bytes for watchlist match | W2 | send tx to a watched address, observe `OnTransactionFound` with raw in the event |
| Reorg handling without tx duplication | W3 | loopback-peer fork test in `Dxs.Bsv.Tests` |
| Per-source metrics present | W4 | admin SPA page shows three sources with non-zero counters |
| Single `Broadcast` path | W5 | grep confirms `BitcoindService.Broadcast`, `BitailsRestApiClient.Broadcast`, `WhatsOnChainRestApiClient.BroadcastAsync` deleted; integration test broadcasts a real mainnet tx |
| Build green | every wave | `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors |
| Tests green | every wave | `dotnet test` (both test projects) — no new failures |

## Closeout and audit rules

- Each wave: produce its own `audits/A1.md` after execution. If A1
  surfaces residuals that need a fix-and-re-audit pass, open `A2.md` —
  otherwise stop at A1.
- Each wave: produce `evidence/closeout.md` recording delivery hashes
  and behavioural summary.
- Program closeout: only when all five waves are `done` (or
  intentionally `not_opened` with rationale). Program `evidence/closeout.md`
  summarises end-state across waves.
- Stop condition between waves: human review + Codex wave-level audit
  pass required before opening the next wave.
