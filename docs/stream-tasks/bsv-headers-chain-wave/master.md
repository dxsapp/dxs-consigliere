---
created: 2026-05-16
revised: 2026-05-17 (post wave audit A1)
type: wave
parent: consigliere-thin-node-observer-program
status: draft (awaiting follow-up wave audit OR direct S0 audit)
---

# Wave 1 — BSV Headers Chain + Program-Wide Contract Freeze

## Goal

Stand up a P2P-driven header-chain tracker inside Consigliere and, in
the same wave, **freeze every program-wide hub event, server method,
`PeerSession` callback, send-helper, and `PeerTelemetry` field that
downstream waves (W2-W6) will consume.** End state:

- Active mainnet chain tip and the trailing ≤200 headers are tracked
  purely from BSV P2P (`inv(MSG_BLOCK)` + `headers` / `getheaders`).
- Headers persist in RavenDB as `BlockHeaderDocument`.
- New tip notifications surface through `IWalletHub.OnNewBlock` to
  SignalR clients.
- All hub event signatures, subscription-group names, `PeerSession`
  callback / send-helper / telemetry surfaces, and the W5
  `BroadcastReceiptDto` shape required by W2 through W6 are
  pre-declared in this wave. No downstream wave needs to touch
  `PeerSession.cs` or `IWalletHub.cs` to add new surfaces.
- A 24 h soak harness (`HeadersSoakRecorder`) demonstrates p95 lag
  ≤ 2 s versus WhatsOnChain's chain-info endpoint, with the schema
  and reproducibility rules locked in `slices.md` §S7.

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
headers only.

## Scope

In scope:

- Pure-data `BlockHeader` parsing and chain-rule validation
  (`prev_block_hash`, sequential heights, PoW header hash).
- `BlockHeaderDocument` Raven model + `BlockHeaderStore`.
- `HeadersChainService` hosted service that drives initial sync,
  `getheaders` requests against current peers, and tip-extension on
  inbound `headers` / `inv(MSG_BLOCK)` events.
- `PeerSession` extension: the full **program-wide** observation
  callback set + send helper `SendGetHeadersAsync` + telemetry sink
  (signatures frozen, bodies stubbed where downstream waves own them).
- `IWalletHub.OnNewBlock` event + subscription group `block:tip`.
- `IWalletHub.OnReorg` signature stub + subscription group
  `block:reorg` (body delivered in W3).
- `BroadcastReceiptDto` DTO shape locked for W5 — comment + manifest.
  Server method signatures `IWalletServer.Broadcast` /
  `BroadcastTracked` are **not** frozen in W1; W5 may collapse them.
- `AdminP2pController` extension: `GET /api/admin/p2p/headers/tip`,
  `GET /api/admin/p2p/headers/recent?count=N`.
- Initial-sync seeding from Bitails REST (`/v1/bsv/main/chain/info`
  + a small recent-headers fetch) toggled by config; pure-P2P
  cold-start path is the default once a height is known.
- `HeadersSoakRecorder` recorder under `tests/Spikes/P2p/` for the
  24 h validation harness, with JSONL schema + reproducibility rules
  defined in `slices.md` §S7.
- **Public-API manifest** (`tests/Dxs.Consigliere.Tests/P2p/ContractFreeze/manifest.json`)
  and approval test, replacing the weaker reflection-existence check.

Out of scope:

- Reorg replay / `BlockDisconnected` journal events (W3).
- Block-body fetch and validation (W3 fetches under reorg).
- Mempool tx observation (W2).
- Watchlist matcher and `RavenWatchlistLoader` (W2).
- Source metrics recorder (W4).
- Broadcast unification body changes (W5; only DTO shape locked here).
- Peer scoring loop and alerts (W6).
- Migration of `TxRelayCoordinator` away from `IncomingMessages`
  — additive dispatch only; channel reads continue to work in W1.
- Historical full-chain sync. We track at most a few hundred recent
  headers.

## Core Rules

1. **Contract freeze is mandatory and lands first.** S0 closes before
   any other Wave 1 slice opens. All later W1 slices have `depends_on`
   that includes S0 explicitly (see §Slice Ledger). This is the
   prerequisite-slice gate the program-level `launch-prompt.md`
   requires for Waves 1 and 2.
2. **No new hub events, server methods, or `PeerSession` callbacks /
   send helpers / telemetry fields land outside S0.** If a later
   slice (or any downstream wave) needs a surface not declared in S0,
   it must open an explicit contract-freeze amendment slice in this
   package before touching the frozen files.
3. **Callback dispatch is additive.** New `PeerSession` callbacks
   (`OnHeadersReceived`, `OnInvReceived`, `OnRejectReceived`) **do
   not** remove `headers` / `inv` / `reject` from the existing
   `IncomingMessages` channel. Existing consumers (notably
   `TxRelayCoordinator`) keep reading the channel until W2/W3
   intentionally migrate them. A regression test in S0 asserts both
   the callback fires and the channel still delivers the frame.
4. **Frozen surfaces have empty bodies where downstream waves own
   the implementation.** `OnReorg` is declared on `IWalletHub` but
   never emitted in W1; W3 wires the emitter. `IPeerTelemetrySink`
   has a `NullPeerTelemetrySink` default constructed by every
   `PeerSession` in W1; W6 swaps the construction site to a
   production sink. No registry indirection — `TxRelayCoordinator`
   calls `session.Telemetry.<...>` directly on the session that
   emitted the frame (audit A1-followup new-H1). Build stays green
   at every wave closure without violating the freeze.
5. **Headers store is append-and-prune.** We retain the last 200
   headers by height (configurable). Older headers are pruned by a
   background pass — provider clients (Bitails/JungleBus) cover
   anything older when W3 asks for it.
6. **P2P is the authoritative chain signal.** Bitails initial-sync
   is a bootstrap convenience only. After bootstrap, the chain tip
   advances purely on P2P signal. An explicit
   `HeadersChainOptions.SeedFromBitails = false` flag turns the
   bootstrap off for pure-P2P-mode operators.
7. **PoW header validation enforced.** Every accepted header has its
   double-SHA-256 hash recomputed and checked against the target
   bits encoded in the header. Mainnet difficulty rules apply.
8. **Stop-and-audit at slice S0.** S0 produces its own slice-level
   audit (`audits/S0-A1.md`) before main slices open. Subsequent
   slices (S1-S7) run under wave-level audit only.
9. **Contract drift is detected by an approval-style manifest test,
   not by a reflection-existence smoke test** (audit A1 M1). The
   manifest covers names, parameter types, return types, DTO
   constructor/property names and types, and asserts no extra members
   are added silently.

## Ownership Zones

Program zones touched in this wave (per program `master.md` Ownership
Zones):

| Program zone | Repo zone | Files (new unless noted) |
|---|---|---|
| `bsv-p2p-session` | `bsv-protocol-core` | `src/Dxs.Bsv/P2p/Session/PeerSession.cs` (extend: callbacks, `SendGetHeadersAsync`, `Telemetry` property) |
| `bsv-p2p-chain` (new) | `bsv-protocol-core` | `src/Dxs.Bsv/P2p/Chain/{HeadersChain.cs, BlockHeaderHasher.cs, HeadersChainOptions.cs, IPeerTelemetrySink.cs, NullPeerTelemetrySink.cs, PeerTelemetry.cs}` |
| `consigliere-p2p-services` | `indexer-ingest-orchestration` | `src/Dxs.Consigliere/Services/P2p/{HeadersChainService.cs, HeadersChainBootstrapper.cs, HubNewBlockNotifier.cs, INewBlockNotifier.cs, TxRelayCoordinator.cs}` (last one: minor telemetry-hook only) |
| `consigliere-p2p-data` | `indexer-state-and-storage` | `src/Dxs.Consigliere/Data/{P2p/BlockHeaderStore.cs, Models/P2p/BlockHeaderDocument.cs}` |
| `consigliere-hub-public` | `public-api-and-realtime` | `src/Dxs.Consigliere/WebSockets/{IWalletHub.cs, IWalletServer.cs, WalletHub.cs, BlockTipDto.cs (new), ReorgEventDto.cs (new), BroadcastReceiptDto.cs (comment only)}` |
| `consigliere-admin-api` | `public-api-and-realtime` | `src/Dxs.Consigliere/Controllers/AdminP2pController.cs` (extend) |
| `program-tests` | `verification-and-conformance` | `tests/Dxs.Bsv.Tests/P2p/{Chain/,Session/}`, `tests/Dxs.Consigliere.Tests/P2p/{ContractFreeze/,HubNewBlockTests.cs,BlockHeaderStoreTests.cs,HeadersChainServiceTests.cs}` |
| `program-docs` | `repo-governance` | `docs/stream-tasks/bsv-headers-chain-wave/` |

Out-of-catalog ad-hoc:

| Ad-hoc zone | Purpose | Files |
|---|---|---|
| `headers-soak-spike` | Throwaway 24 h recorder for the p95-lag validation harness | `tests/Spikes/P2p/HeadersSoakRecorder/` |

### Handoff Facts → Consumers (audit A1 M3)

Per-wave consumer table. Each row names the **exact** type/member,
which downstream wave consumes it, what changes are still allowed
after W1, and what is forbidden without a contract-freeze amendment
slice in this package.

| Wave | Consumes | Allowed change | Forbidden without amendment |
|---|---|---|---|
| W2 | `PeerSession.OnInvReceived(InvMessage)` callback | implement filter logic on `InvType.Tx` in observer | rename callback; change parameter type; add second InvReceived callback |
| W2 | `PeerSession.OnRejectReceived(RejectMessage)` | use for reject-class quorum on observed tx | rename; change parameter type |
| W2 | `BlockHeaderStore.GetTipAsync` / `GetByHashAsync` | read for tx-confirm checks | change return type or method names |
| W2 | `IWalletHub.OnTransactionFound` (already exists) | unchanged | n/a |
| W3 | `PeerSession.OnInvReceived(InvMessage)` filtered on `InvType.Block` | use for reorg detection | same as W2 |
| W3 | `PeerSession.OnHeadersReceived` | use for reorg-detector input | rename; change parameter type |
| W3 | `IWalletHub.OnReorg(ReorgEventDto)` | implement body | change DTO field set; rename event |
| W3 | `BlockHeaderStore.RecentAsync`, `GetByHashAsync`, `PruneBelowAsync` | reorg ancestor walks | change method names / signatures |
| W4 | `IPeerTelemetrySink.Snapshot()` + `PeerTelemetry` fields | implement production sink reading aggregates per session | add new fields without amendment; rename existing fields |
| W5 | `BroadcastReceiptDto` shape | unchanged — frozen | rename properties; change types; add/remove properties |
| W5 | `IWalletServer.Broadcast(...)`, `BroadcastTracked(...)` | **may collapse into single `Broadcast(hex) → BroadcastReceiptDto`** — this is W5's authorised change | n/a (W5 owns these server methods) |
| W6 | `IPeerTelemetrySink` rich events (`RecordGetDataRequested`, `RecordGetDataServed`, `RecordRelayBackInv`, `RecordRejectReceived(cls)`, `RecordProtocolViolation`, `RecordDisconnect`) | implement production sink + scoring; swap construction site inside `PeerManager` (W6 owns that file) | change interface; collapse `RejectByClass` to a single counter |
| W6 | `BlockTipDto` / `ReorgEventDto` shapes for admin panel | unchanged — frozen | rename properties; add/remove properties |
| W6 | `HeadersChainOptions` config keys | surface to admin UI | rename keys; change defaults silently |

Out of band: W5 must produce public-API change notes for the
collapsed server methods (handled by W6 per program package).

## Slice Ledger

Status vocabulary: `not_opened`, `todo`, `in_progress`, `blocked`,
`done`, `stale`.

Direct dependency edges encoded everywhere (audit A1 H3 fix).

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S0 | program-wide contract freeze (`bsv-p2p-session` + `bsv-p2p-chain` + `consigliere-hub-public`) | not_opened | — | new files compile; `dotnet build` green; `dotnet test` baseline green; manifest approval test green; additive-dispatch regression green; grep checks green | every callback / send-helper / event / DTO / interface listed in `slices.md` §S0 exists with correct signature; stub-no-op bodies present; `IncomingMessages` still delivers `inv`/`reject`/`headers`; PR diff touches only the frozen-surface files (plus tests + manifest) | slice-A1 |
| S1 | `bsv-p2p-chain` | not_opened | S0 | pure unit tests on header parsing, double-SHA, prev-hash linking, PoW target check | `HeadersChain` pure object validates and links headers; rejects forks by signaling caller; 100% branch coverage on validation | wave-A2 |
| S2 | `consigliere-p2p-data` | not_opened | S0 | Raven integration test saves + retrieves headers by height and by hash; prune-to-N test | `BlockHeaderDocument` + `BlockHeaderStore` saves, queries, and prunes; no leakage of older docs beyond `RetainedHeaderCount` | wave-A2 |
| S3 | `consigliere-p2p-services` | not_opened | S0, S1, S2 | hosted-service test wires `HeadersChain` + `BlockHeaderStore`; reacts to `OnHeadersReceived` and `OnInvReceived(InvMessage)` with `MSG_BLOCK` type by calling `SendGetHeadersAsync` on a ready peer | tip advances on inbound `headers`; `block:tip` group notified; competing tips stored side-by-side without errors (recovery deferred to W3) | wave-A2 |
| S4 | `consigliere-p2p-services` (Bitails bootstrap) | not_opened | S0, S1, S2, S3 | integration test against canned Bitails JSON: bootstrap produces non-empty store + correct tip height; `SeedFromBitails=false` skips fetch entirely | initial-sync produces a usable tip within 10 s on fresh start; pure-P2P mode runs cleanly with no Bitails calls | wave-A2 |
| S5 | `consigliere-hub-public` (events live) | not_opened | S0, S3 | SignalR test client subscribes to `block:tip`, asserts payload shape; subscription to `block:reorg` succeeds with no-op handler | `OnNewBlock` fires on tip advance with correct `BlockTipDto`; `OnReorg` registered but never invoked in W1 | wave-A2 |
| S6 | `consigliere-admin-api` | not_opened | S0, S3 | controller test asserts JSON shape against a seeded chain; auth wired identically to existing `AdminP2pController` endpoints | `GET /api/admin/p2p/headers/tip` and `/recent` return live data; auth and error responses match repo convention | wave-A2 |
| S7 | `headers-soak-spike` | not_opened | S0, S5, S6 | the spike runs 24 h on a fresh VPS per the JSONL schema and reproducibility rules in `slices.md` §S7; recorder dumps a JSON timeline; offline analysis script computes p95 lag and writes `evidence/headers-soak.md`; admin endpoint tip matches WhatsOnChain at end | p95 lag ≤ 2 s confirmed across ≥ 128 joined blocks; missed-block ratio < 5 %; HTTP-error coverage documented | wave-A2 |

Slice S0 (contract freeze) is the **prerequisite slice** required by
the program launch prompt. Its slice-level audit (`slice-A1`) lands
in `audits/S0-A1.md` before S1-S7 open. The wave-level audit on this
package (this revision is audit A1) becomes A2 after S1-S7 close.

## Definition of Done

- All slices `done` (or intentionally deferred with rationale in
  delivery notes).
- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- `dotnet test` returns no new failures vs the baseline immediately
  before Wave 1 started.
- Manifest approval test green (no contract drift).
- Additive-dispatch regression green (`IncomingMessages` still
  delivers after callbacks added).
- `GET /api/admin/p2p/headers/tip` matches WhatsOnChain's
  `chain/info` tip hash at the time of validation.
- 24 h `HeadersSoakRecorder` evidence with p95 lag ≤ 2 s recorded in
  `evidence/headers-soak.md` per the schema in `slices.md` §S7.
- Wave-level Codex audit at `audits/wave1-audit-A2.md` returns
  APPROVE (or APPROVE WITH CHANGES that are addressed in-wave).
- `evidence/closeout.md` lists delivery hashes per slice and end-state
  metrics.

## Delivery Notes

Commit hashes recorded here as slices close.

- Wave package created: `1c25852` (initial draft, pre-audit)
- Wave audit A1 (Codex GPT-5, MAJOR REVISION REQUIRED):
  `audits/wave1-audit-A1.md`
- Wave revision per audit A1: `<hash-pending>` (this commit)
- Slice S0 audit A1: (pending)
- Slice S0 delivery: (pending)
- Slice S1 delivery: (pending)
- Slice S2 delivery: (pending)
- Slice S3 delivery: (pending)
- Slice S4 delivery: (pending)
- Slice S5 delivery: (pending)
- Slice S6 delivery: (pending)
- Slice S7 delivery: (pending)
- Wave audit A2 (post-execution): (pending)
- Wave closeout commit: (pending)
