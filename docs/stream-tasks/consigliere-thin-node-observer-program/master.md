---
created: 2026-05-16
type: program
status: draft (awaiting program-level audit)
---

# Consigliere Thin-Node Observer Program

## Goal

Turn the existing P2P broadcaster (Gate 1–3, already landed and producing
real BSV mainnet broadcasts) into a full bidirectional thin-node engine
inside Consigliere — so the product observes transactions and blocks
straight from the BSV P2P network, not only through Bitails/JungleBus
HTTP polls. End state: Consigliere ingests via P2P first, Bitails and
JungleBus stay alive as configurable redundant sources whose health is
measurable and visible in the admin panel.

Business outcome: Consigliere becomes a self-sufficient BSV indexer for
managed-scope addresses and tokens, with operator-grade observability of
how each ingest source contributes, and without an attached `bitcoin-sv`
node.

## Product Decision

**The thin-node observer is internal Consigliere infrastructure, not a
public surface.** Businesses consuming Consigliere keep using the
existing REST/SignalR API contracts (`api/address/*`, `api/token/*`,
`api/tx/*`, `WalletHub.*`). Source of truth for transactions stays
`TxLifecycleProjectionDocument` driven by the existing
`TxObservationJournalWriter`. P2P observer is one more source feeding
that pipeline.

`tools/BsvBroadcastNode` (the standalone HTTP broadcaster) stays as a
dev tool only. It is not advertised to product consumers.

## Scope

In scope:
- block-header chain state via P2P with provider-validated content
- mempool transaction observation via P2P with watchlist matching
- reorg detection and orphan recovery
- per-source metrics surfaced in admin UI
- unification of broadcast under a single `Broadcast` method
  (P2P primary, no legacy HTTP-provider fallback)
- ownership-zone discipline so wave executions don't fight over the
  same files

Out of scope:
- Postgres migration (deferred — separate program when this is done)
- bloom-filter peer-side filtering (BIP37 `filterload`); we do
  client-side watchlist matching with `HashSet<ulong>` instead
- full historical chain sync; we only track the active tip and a
  shallow reorg-recovery window (≤200 headers back)
- multi-tenant watchlists; one Consigliere instance = one watchlist
- breaking REST/SignalR public surface contracts beyond the single
  `Broadcast` method change

## Core Rules

1. **Source-agnostic persistence.** P2P observer must append to the
   existing `TxObservationJournalWriter` with source tag `"p2p"`; it does
   not create a parallel transaction store.
2. **`TxLifecycleProjectionDocument` is the canonical observed-tx view.**
   `SeenBySources` accumulates source tags. Quorum-based state
   transitions read from this projection.
3. **HashSet-based watchlist matching, not bloom filter.**
   `WatchingAddress.Hash160[0..8]` as `ulong` keys, full hash160 verify
   on positive match. Hot reload via Raven Subscription API.
4. **Blocks: P2P signal, provider content.** P2P `inv(MSG_BLOCK)` and
   `headers` give us the tip notification; full block content is fetched
   from Bitails / JungleBus REST. Provider response is validated against
   the P2P-tracked header (hash + prev + height).
5. **Bitails / JungleBus realtime stay alive.** Both subscribe-side
   runners remain enabled by default. Per-source metrics show whether
   either is still pulling weight. Operator config can turn either off.
6. **No legacy HTTP-provider broadcast.** `IBroadcastService` collapses
   to a single P2P-first `Submit/Broadcast` flow. Bitails/WoC/bitcoind
   broadcast paths are removed (this is vnext — no backwards-compat).
7. **Stop-and-audit per wave.** Each child wave gets its own Codex
   audit before execution and `audits/A1.md` after closeout. No wave
   leaves `done` without an audit pass.
8. **One commit per slice when practical.** Final wave closeout commit
   may bundle small follow-ups but should reference the slice ledger.

## Ownership Zones

Touch these source paths during this program. Each child wave declares
which zones it actually modifies — no wave touches a zone owned by
another in-flight wave.

- `bsv-p2p-codec` — `src/Dxs.Bsv/P2p/{Codec,Messages,FrameCodec,P2pNetwork,P2pAddress,P2pCommands,P2pDecodeException,Frame}.cs`
- `bsv-p2p-session` — `src/Dxs.Bsv/P2p/Session/`
- `bsv-p2p-pool` — `src/Dxs.Bsv/P2p/Pool/`
- `bsv-p2p-chain` — `src/Dxs.Bsv/P2p/Chain/` (new; Phase 1)
- `bsv-p2p-observer` — `src/Dxs.Bsv/P2p/Observer/` (new; Phase 2)
- `consigliere-p2p-services` — `src/Dxs.Consigliere/Services/P2p/`
- `consigliere-p2p-data` — `src/Dxs.Consigliere/Data/{P2p,Models/P2p}/`
- `consigliere-p2p-tasks` — `src/Dxs.Consigliere/BackgroundTasks/P2p/`
- `consigliere-p2p-realtime` — `src/Dxs.Consigliere/BackgroundTasks/Realtime/` (Bitails/JBus ingest paths — adjusted for source tags)
- `consigliere-broadcast` — `src/Dxs.Consigliere/Services/{IBroadcastService.cs,Impl/BroadcastService.cs}`
- `consigliere-hub-public` — `src/Dxs.Consigliere/WebSockets/{IWalletHub.cs,WalletHub.cs,BroadcastReceiptDto.cs}`
- `consigliere-admin-api` — `src/Dxs.Consigliere/Controllers/AdminP2pController.cs` + new admin controllers
- `consigliere-tx-projection` — `src/Dxs.Consigliere/Services/Impl/Tx*Projection*.cs` and related
- `admin-ui` — `src/admin-ui/` (admin SPA pages added late, Phase 4)
- `dxs-bsv-tests` — `tests/Dxs.Bsv.Tests/P2p/`
- `consigliere-tests` — `tests/Dxs.Consigliere.Tests/`

## Program Waves

Sequential execution. Each wave produces its own
`docs/stream-tasks/<wave-slug>/{master.md,slices.md,launch-prompt.md}`
when its turn comes. The program package stays open across all waves.

### Wave 1 — `bsv-headers-chain-wave`
Headers chain tracker via P2P, persisted to RavenDB. New-block events
exposed through `WalletHub.OnNewBlock`. Provider-side block content fetch
hooked to header-tip event. Initial seed via Bitails REST (configurable
to P2P-only).
Touches: `bsv-p2p-session` (callbacks), `bsv-p2p-chain` (new),
`consigliere-p2p-services`, `consigliere-p2p-data`, `consigliere-hub-public`,
`dxs-bsv-tests`.

### Wave 2 — `bsv-mempool-observer-wave`
P2P observer plugged into the existing journal as a new source. Watchlist
matching via `HashSet<ulong>`. Hot reload of `WatchingAddress`/`WatchingToken`.
Source-stats recorder records every source-observation before dedupe.
Touches: `bsv-p2p-session` (inv-tx callback), `bsv-p2p-observer` (new),
`consigliere-p2p-services` (journal source runner), `consigliere-p2p-tasks`,
`consigliere-tx-projection` (source tag), `dxs-bsv-tests`.

### Wave 3 — `reorg-handling-wave`
Reorg detection algorithm using the headers chain from Wave 1. On reorg,
orphaned tx documents get reverted to mempool state via journal append.
`WalletHub.OnReorg` and `OnTransactionDeleted` events. Rescan of orphaned
blocks via provider fetch.
Touches: `bsv-p2p-chain` (reorg detector), `consigliere-p2p-services`,
`consigliere-p2p-data`, `consigliere-hub-public`, `consigliere-tx-projection`.

### Wave 4 — `observation-source-metrics-wave`
Source metrics (P2P / Bitails / JungleBus) for every observation: first-seen
counts, lag histograms, only-saw counters. Admin endpoints exposing the
metrics. Admin SPA page for P2P + observation health.
Touches: `consigliere-p2p-services` (metrics recorder), `consigliere-admin-api`,
`consigliere-p2p-realtime` (Bitails/JBus tag with source), `admin-ui`.

### Wave 5 — `broadcast-unification-wave`
Collapse `Broadcast(hex)` and `BroadcastTracked(hex)` into a single
`Broadcast(hex) → BroadcastReceiptDto`. Remove legacy HTTP-provider
broadcast paths (`BitcoindService.Broadcast`, Bitails/WoC broadcast
clients). Backward compatibility intentionally not preserved (vnext).
Touches: `consigliere-broadcast`, `consigliere-hub-public`,
`bsv-p2p-pool` minor wiring, possibly `Dxs.Infrastructure` cleanup.

## Cross-Wave Dependency Rules

- Wave 1 must close before Wave 3 (reorg needs headers chain).
- Wave 2 must close before Wave 3 (reorg revert touches observed tx).
- Wave 2 must close before Wave 4 (metrics need source observations).
- Wave 1 and Wave 5 can run in parallel **only if** different agents work
  on different zones. We default to strict sequential execution per the
  user's stop-and-audit rule.
- All waves share `consigliere-hub-public`. Hub event additions happen
  sequentially across waves. No wave silently changes a hub event added
  by another.

## Program Ledger

| wave | slug | status | depends_on | done_when | audit |
|---|---|---|---|---|---|
| 1 | `bsv-headers-chain-wave` | not_opened | — | headers tracked, new-block event emitted, reorg framework wired (detection itself in W3) | A1 |
| 2 | `bsv-mempool-observer-wave` | not_opened | — | tx observed via P2P, journal source=p2p, watchlist match working, hot reload working | A1 |
| 3 | `reorg-handling-wave` | not_opened | W1, W2 | reorg event emitted, orphan tx reverted, rescan implemented | A1 |
| 4 | `observation-source-metrics-wave` | not_opened | W2 | per-source metrics persisted, admin endpoints + UI page live | A1 |
| 5 | `broadcast-unification-wave` | not_opened | — | single Broadcast method, legacy HTTP-provider paths removed | A1 |

Status vocabulary: `not_opened`, `todo`, `in_progress`, `blocked`, `done`, `stale`.

## Definition of Done

Program is `done` when:
- all five waves are `done` or intentionally `not_opened`
- `tests/Dxs.Bsv.Tests` passes; `tests/Dxs.Consigliere.Tests` passes (any
  delta vs baseline counted as residuals)
- a real BSV mainnet transaction broadcast survives the full lifecycle:
  Submitted → PeerAcked → MempoolSeen → Mined → Confirmed, with events
  visible via SignalR
- the admin panel shows healthy P2P pool, recent headers tip, mempool
  observation rate, per-source metrics
- closeout evidence in `evidence/closeout.md` lists end-state metrics,
  delivery hashes, residuals, and operator-facing changes

## Delivery Notes

Commit hashes recorded here as waves close.

- Wave 1 delivery: (pending)
- Wave 2 delivery: (pending)
- Wave 3 delivery: (pending)
- Wave 4 delivery: (pending)
- Wave 5 delivery: (pending)
- Program closeout commit: (pending)
