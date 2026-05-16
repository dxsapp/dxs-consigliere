---
created: 2026-05-16
type: wave
parent: consigliere-thin-node-observer-program
status: draft (awaiting wave-level Codex audit)
---

# Wave 1 — BSV Headers Chain + Program-Wide Contract Freeze

## Goal

Stand up a P2P-driven header-chain tracker inside Consigliere and, in
the same wave, **freeze every program-wide hub event, `PeerSession`
extension point, and `PeerTelemetry` field that downstream waves
(W2-W6) will consume.** End state:

- Active mainnet chain tip and the trailing ≤200 headers are tracked
  purely from BSV P2P (`inv(MSG_BLOCK)` + `headers` / `getheaders`).
- Headers persist in RavenDB as `BlockHeaderDocument`.
- New tip notifications surface through `IWalletHub.OnNewBlock` to
  SignalR clients.
- All hub event signatures, subscription-group names, and
  `PeerSession` callback / telemetry surfaces required by W2 through
  W6 are pre-declared in this wave so no downstream wave needs to
  touch `PeerSession.cs` or `IWalletHub.cs` to add new surfaces.
- A 24 h soak harness (`HeadersSoakRecorder`) demonstrates p95 lag
  ≤ 2 s versus WhatsOnChain's chain-info endpoint.

This wave intentionally does **not** ship reorg recovery, mempool
observation, source metrics, broadcast unification, or peer scoring.
Those live in W3, W2, W4, W5, W6 respectively. Wave 1 only **detects**
divergence by storing competing tips — recovery is W3.

## Product Decision

Headers chain is internal Consigliere infrastructure. The public
SignalR surface gains exactly two new bits:

- `IWalletHub.OnNewBlock(BlockTipDto)` — fires on every accepted new
  tip.
- `IWalletHub.OnReorg(ReorgEventDto)` — signature frozen here; **body
  remains a stub no-op until W3**. This is per program Core Rule §7
  ("contract freeze in Wave 1") to prevent silent hub additions later.

Block-body content is **not** fetched in this wave. The chain stores
headers only. When the wave is complete, downstream waves (W3's reorg
rescan, W2's tx-confirms-into-block check) can request block bodies
from Bitails/JungleBus using header hashes from the chain.

## Scope

In scope:

- Pure-data `BlockHeader` parsing and chain-rule validation
  (`prev_block_hash`, sequential heights, PoW header hash).
- `BlockHeaderDocument` Raven model + `BlockHeaderStore`.
- `HeadersChainService` hosted service that drives initial sync,
  `getheaders` requests against current peers, and tip-extension on
  inbound `headers` / `inv(MSG_BLOCK)` events.
- `PeerSession` extension: the full **program-wide** observation
  callback set + telemetry sink (signatures frozen, bodies stubbed
  where downstream waves own them).
- `IWalletHub.OnNewBlock` event + subscription group `block:tip`.
- `IWalletHub.OnReorg` signature stub + subscription group
  `block:reorg` (body delivered in W3).
- `BroadcastReceiptDto` shape locked for W5 — comments in code
  document this.
- `AdminP2pController` extension: `GET /api/admin/p2p/headers/tip`,
  `GET /api/admin/p2p/headers/recent?count=N`.
- Initial-sync seeding from Bitails REST (`/v1/bsv/main/chain/info`
  + a small recent-headers fetch) toggled by config; pure-P2P
  cold-start path is the default once a height is known.
- `HeadersSoakRecorder` recorder under `tests/Spikes/P2p/` for the
  24 h validation harness.

Out of scope:

- Reorg replay / `BlockDisconnected` journal events (W3).
- Block-body fetch and validation (W3 fetches under reorg; routine
  body fetching for confirmed-tx checks is a W2 add-on against
  the frozen header surface).
- Mempool tx observation (W2).
- Watchlist matcher and `RavenWatchlistLoader` (W2).
- Source metrics recorder (W4).
- Broadcast unification body changes (W5; only DTO shape locked here).
- Peer scoring loop and alerts (W6).
- Historical full-chain sync. We track at most a few hundred recent
  headers; deeper history relies on Bitails/JungleBus on demand.

## Core Rules

1. **Contract freeze is mandatory and lands first.** The first slice
   (S0) closes before any other Wave 1 slice opens. All later W1
   slices have `depends_on = s0-contract-freeze`. This is the
   prerequisite-slice gate the program-level `launch-prompt.md`
   requires for Waves 1 and 2.
2. **No new hub events or `PeerSession` callbacks land outside S0.**
   If a later Wave 1 slice (or any downstream wave) needs a surface
   not declared in S0, it must open an explicit contract-freeze
   amendment slice in this package before touching `PeerSession.cs`
   or `IWalletHub.cs`.
3. **Frozen surfaces have empty bodies where downstream waves own
   the implementation.** `OnReorg` returns no-op in W1; W3 implements
   it. `IPeerTelemetrySink` has a `NullPeerTelemetrySink` default
   registered in DI; W6 swaps in the production implementation. This
   keeps the build green at every wave closure without violating the
   freeze.
4. **Headers store is append-and-prune.** We retain the last 200
   headers by height (configurable). Older headers are pruned by a
   background pass — provider clients (Bitails/JungleBus) cover
   anything older when W3 asks for it.
5. **P2P is the authoritative chain signal.** Bitails initial-sync
   is a bootstrap convenience only. After bootstrap, the chain tip
   advances purely on P2P signal. An explicit
   `HeadersChainOptions.SeedFromBitails = false` flag turns the
   bootstrap off for pure-P2P-mode operators.
6. **PoW header validation enforced.** Every accepted header has its
   double-SHA-256 hash recomputed and checked against the target
   bits encoded in the header. Mainnet difficulty rules apply. We
   do **not** re-validate ancestors on receipt — we trust the
   persisted chain up to its current tip plus the new header.
7. **Stop-and-audit at slice S0.** S0 produces its own slice-level
   audit before main slices open. Subsequent slices (S1-S7) run
   under wave-level audit only.

## Ownership Zones

Program zones touched in this wave (per program `master.md` Ownership
Zones):

| Program zone | Repo zone | Files (new unless noted) |
|---|---|---|
| `bsv-p2p-session` | `bsv-protocol-core` | `src/Dxs.Bsv/P2p/Session/PeerSession.cs` (extend) |
| `bsv-p2p-chain` (new) | `bsv-protocol-core` | `src/Dxs.Bsv/P2p/Chain/{HeadersChain.cs, BlockHeaderHasher.cs, HeadersChainOptions.cs, IPeerTelemetrySink.cs, NullPeerTelemetrySink.cs, PeerTelemetry.cs}` |
| `consigliere-p2p-services` | `indexer-ingest-orchestration` | `src/Dxs.Consigliere/Services/P2p/HeadersChainService.cs` |
| `consigliere-p2p-data` | `indexer-state-and-storage` | `src/Dxs.Consigliere/Data/{P2p/BlockHeaderStore.cs, Models/P2p/BlockHeaderDocument.cs}` |
| `consigliere-hub-public` | `public-api-and-realtime` | `src/Dxs.Consigliere/WebSockets/{IWalletHub.cs, WalletHub.cs, BlockTipDto.cs (new), ReorgEventDto.cs (new), BroadcastReceiptDto.cs (comments)}` |
| `consigliere-admin-api` | `public-api-and-realtime` | `src/Dxs.Consigliere/Controllers/AdminP2pController.cs` (extend) |
| `program-tests` | `verification-and-conformance` | `tests/Dxs.Bsv.Tests/P2p/Chain/`, `tests/Dxs.Consigliere.Tests/P2p/` |
| `program-docs` | `repo-governance` | `docs/stream-tasks/bsv-headers-chain-wave/` |

Out-of-catalog ad-hoc:

| Ad-hoc zone | Purpose | Files |
|---|---|---|
| `headers-soak-spike` | Throwaway 24 h recorder for the p95-lag validation harness | `tests/Spikes/P2p/HeadersSoakRecorder/` |

Handoff facts produced by this wave (other waves consume these):

- Frozen `IWalletHub` event signatures (`OnNewBlock`, `OnReorg`,
  `BroadcastReceiptDto`).
- Frozen `PeerSession` callback set
  (`OnHeadersReceived`, `OnInvReceived(InvMessage)`,
  `OnRejectReceived`).
- Frozen `PeerTelemetry` field set + `IPeerTelemetrySink` interface.
- `BlockHeaderDocument` schema (W2 reads it for tx-confirm checks;
  W3 reads it for reorg ancestor walks).
- `HeadersChainOptions.SeedFromBitails`, `RetainedHeaderCount`,
  `GetHeadersIntervalMs` config keys (W6 may surface to admin UI).

Handoff requirements consumed by this wave:

- Existing `PeerSession` Gate 1-3 surface (`SendGetHeadersAsync`,
  `IncomingMessages` channel, `OnAddrReceived` callback,
  `Completion` task).
- Existing `PeerManager` ready-peer enumeration so the chain service
  can pick a target for `getheaders`.
- Existing `BitailsRestApiClient` for the bootstrap seed.

## Slice Ledger

Status vocabulary: `not_opened`, `todo`, `in_progress`, `blocked`,
`done`, `stale`.

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S0 | program-wide contract freeze (`bsv-p2p-session` + `consigliere-hub-public`) | not_opened | — | new files compile; `dotnet build` green; `dotnet test` baseline green; no body implementations beyond stubs / `Null*Sink` | every callback / event / DTO / interface listed in `master.md` §Core Rules §7 of program package exists with correct signature; stub-no-op bodies present; PR diff touches only the frozen-surface files | slice-A1 |
| S1 | `bsv-p2p-chain` | not_opened | S0 | pure unit tests on header parsing, double-SHA, prev-hash linking, PoW target check | `HeadersChain` pure object validates and links headers; rejects forks by signaling caller; 100% branch coverage on validation | wave-A1 |
| S2 | `consigliere-p2p-data` | not_opened | S0 | Raven integration test saves + retrieves headers by height and by hash; prune-to-N test | `BlockHeaderDocument` + `BlockHeaderStore` saves, queries, and prunes; no leakage of older docs beyond `RetainedHeaderCount` | wave-A1 |
| S3 | `consigliere-p2p-services` | not_opened | S1, S2 | hosted-service test wires `HeadersChain` + `BlockHeaderStore`; reacts to `OnHeadersReceived` (W1) and `OnInvReceived(InvMessage)` with `MSG_BLOCK` type by sending `getheaders` on a ready peer | tip advances on inbound `headers`; `block:tip` group notified; competing tips stored side-by-side without errors (recovery deferred to W3) | wave-A1 |
| S4 | `consigliere-p2p-services` (Bitails bootstrap) | not_opened | S2 | integration test against canned Bitails JSON: bootstrap produces non-empty store + correct tip height; `SeedFromBitails=false` skips fetch entirely | initial-sync produces a usable tip within 10 s on fresh start; pure-P2P mode runs cleanly with no Bitails calls | wave-A1 |
| S5 | `consigliere-hub-public` (events live) | not_opened | S3 | SignalR test client subscribes to `block:tip`, asserts payload shape; subscription to `block:reorg` succeeds with no-op handler | `OnNewBlock` fires on tip advance with correct `BlockTipDto`; `OnReorg` registered but never invoked in W1 | wave-A1 |
| S6 | `consigliere-admin-api` | not_opened | S3 | controller test asserts JSON shape against a seeded chain; auth wired identically to existing `AdminP2pController` endpoints | `GET /api/admin/p2p/headers/tip` and `/recent` return live data; auth and error responses match repo convention | wave-A1 |
| S7 | `headers-soak-spike` | not_opened | S5 | the spike runs 24 h on a fresh VPS; recorder dumps a JSON timeline; offline analysis script computes p95 lag and writes `evidence/headers-soak.md` | p95 lag ≤ 2 s confirmed across the soak window; admin endpoint matches WhatsOnChain over the same window | wave-A1 |

Slice S0 (contract freeze) is the **prerequisite slice** required by
the program launch prompt. Its slice-level audit (`slice-A1`) lands
in `audits/S0-A1.md` before S1-S7 open.

## Definition of Done

- All slices `done` (or intentionally deferred with rationale in
  delivery notes).
- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- `dotnet test` returns no new failures vs the baseline immediately
  before Wave 1 started.
- `GET /api/admin/p2p/headers/tip` matches WhatsOnChain's `chain/info`
  tip hash at the time of validation.
- 24 h `HeadersSoakRecorder` evidence with p95 lag ≤ 2 s recorded in
  `evidence/headers-soak.md`.
- Wave-level Codex audit at `audits/A1.md` returns APPROVE (or
  APPROVE WITH CHANGES that are addressed in-wave).
- `evidence/closeout.md` lists delivery hashes per slice and end-state
  metrics.

## Delivery Notes

Commit hashes recorded here as slices close.

- Wave package created: `<hash-pending>` (this commit)
- Wave audit A1: (pending)
- Slice S0 delivery: (pending)
- Slice S1 delivery: (pending)
- Slice S2 delivery: (pending)
- Slice S3 delivery: (pending)
- Slice S4 delivery: (pending)
- Slice S5 delivery: (pending)
- Slice S6 delivery: (pending)
- Slice S7 delivery: (pending)
- Wave closeout commit: (pending)
