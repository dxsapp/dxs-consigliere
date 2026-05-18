---
created: 2026-05-16
revised: 2026-05-16 (post audit A1)
type: program
status: draft (revised per audit A1 — awaiting next-round program audit OR direct Wave 1 audit)
---

# Consigliere Thin-Node Observer Program

## Goal

Turn the existing P2P broadcaster (Gate 1–3, already landed and producing
real BSV mainnet broadcasts) into a full bidirectional thin-node engine
inside Consigliere — so the product observes transactions and blocks
straight from the BSV P2P network, not only through Bitails/JungleBus
HTTP polls. End state: Consigliere ingests via P2P first, Bitails and
JungleBus stay alive as configurable redundant sources whose health is
measurable and visible in the admin panel, and the deployment is
operator-grade (peer scoring, alerts, runbook, public API docs).

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
- reorg detection and orphan recovery via existing `Reorged` lifecycle
  state (see Core Rule §2)
- per-source metrics surfaced in admin UI
- unification of broadcast under a single `Broadcast` method
  (P2P primary, no legacy HTTP-provider fallback)
- production-operations layer: peer scoring + rotation, alerts,
  inbound listener decision for Consigliere, public-API change notes
- ownership-zone discipline so wave executions don't fight over the
  same files

Out of scope:
- Postgres migration (deferred — separate program when this is done)
- bloom-filter peer-side filtering (BIP37 `filterload`); we do
  client-side watchlist matching with `HashSet<ulong>` + full-hash160
  verify, sized for ≤500 K addresses (open question on harder ceiling
  noted in §Open Questions)
- full historical chain sync; we only track the active tip and a
  shallow reorg-recovery window (≤200 headers back) with an explicit
  degraded-state fallback for deeper divergence
- multi-tenant watchlists; one Consigliere instance = one watchlist
- breaking REST/SignalR public surface contracts beyond the single
  explicitly-versioned `Broadcast` method change

## Core Rules

1. **Source-agnostic persistence — but make the contract first.**
   P2P observer appends to `TxObservationJournalWriter`. The journal
   currently accepts `TxMessage` and `TxObservationSource` only knows
   `node` / `junglebus` / `bitails`. Wave 2's first slice extends the
   contract to accept a source-neutral observation DTO and adds
   `TxObservationSource.P2p`. No P2P observation code lands before
   that prerequisite closes.
2. **`TxLifecycleProjectionDocument` is the canonical observed-tx view.**
   `SeenBySources` accumulates source tags. Quorum-based state
   transitions read from this projection. Reorg semantics reuse the
   existing `Reorged` lifecycle state already produced by
   `TxLifecycleProjectionRebuilder` on `BlockDisconnected` observations;
   Wave 3 does not invent a new state machine. **However**, Wave 3
   actively re-broadcasts every orphaned tx for which we hold raw
   bytes (own outgoing tx via `OutgoingTransactionStore`, observed
   watchlist tx via `RawTransactionPayloadStore`) so they re-enter
   the network mempool quickly. This is how we close the loop on the
   user requirement: "tx should be back in mempool after reorg". The
   `Reorged` state is the immediate ledger reading; the subsequent
   re-broadcast plus the next mempool observation flip the projection
   forward to `SeenInMempool` again.
3. **HashSet-based watchlist matching, not bloom filter.**
   Watchlist matcher computes `Address.Hash160[0..8]` as `ulong` keys at
   load time from `WatchingAddress.Address` (the model stores
   `{Name, Address}`, no precomputed Hash160). Full hash160 verify on
   positive match. Hot reload via Raven Subscription API. Tokens are
   matched by parsing outputs against `WatchingToken.TokenId`; if any
   token is watched, P2P observer parses every tx — same limitation as
   today's Bitails realtime scope provider.
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
7. **Hub & PeerSession contract freeze in Wave 1.** Wave 1's first
   slice pre-declares the **complete** set of hub events,
   `PeerSession` extension points, and per-peer telemetry surface that
   every downstream wave needs. Bodies may stub; the surfaces themselves
   are frozen. This enumeration replaces any earlier draft list.
   - **Hub events on `IWalletHub`** (consumed by Waves 1–5):
     - `OnNewBlock(BlockTipDto)` (W1)
     - `OnReorg(ReorgEventDto)` (W3)
     - existing `OnTransactionFound` / `OnTransactionDeleted` reused
       by W2/W3 unchanged
     - `Broadcast(hex) → BroadcastReceiptDto` DTO signature finalised
       (hub method itself changes in W5; DTO is stable across waves)
   - **`PeerSession` observation callbacks** (typed DTOs only, no
     bare hash overloads):
     - `OnHeadersReceived(IReadOnlyList<BlockHeader>)` (W1)
     - `OnInvReceived(InvMessage)` — single callback for both tx and
       block inv items; consumers filter by `InvType`. Replaces the
       earlier inconsistent `OnBlockInvReceived` / `OnInvReceived(tx)`
       split.
     - `OnRejectReceived(RejectMessage)` (W2 reject-class quorum;
       W6 reject-rate scoring)
   - **`PeerSession` telemetry surface** (consumed by W4 source
     metrics and W6 peer scoring — frozen here so neither wave needs
     to touch `PeerSession.cs` later):
     - `PeerTelemetry` snapshot type exposing: bytes-in, bytes-out,
       last-recv-utc, last-send-utc, ping-rtt-p50/p95-ms,
       getdata-served-count, reject-received-count,
       last-disconnect-reason.
     - `IPeerTelemetrySink` interface — `PeerSession` writes scalar
       updates on every relevant event (ping reply, frame processed,
       reject received, disconnect). W4 reads aggregates; W6
       implements the production sink + alert poller.

   If a child wave needs a callback or telemetry field not on this
   list, it must open an explicit contract-freeze amendment slice in
   Wave 1's package before touching `PeerSession.cs` — no silent
   additions.
8. **Stop-and-audit per wave.** Each child wave gets its own Codex
   audit before execution and `audits/A1.md` after closeout. No wave
   leaves `done` without an audit pass.
9. **One commit per slice when practical.** Final wave closeout commit
   may bundle small follow-ups but should reference the slice ledger.

## Ownership Zones

Program zones (local to this work) map to the repo's higher-level zone
catalog in `docs/repository-zones/zone-catalog.md`. **All repo-zone
names below are exact catalog entries.** Each child wave declares which
**program** zones it modifies and lists the **repo** zones it crosses,
so the existing handoff contracts apply.

| Program zone | Repo zone (catalog name) | Files |
|---|---|---|
| `bsv-p2p-codec` | `bsv-protocol-core` (per precedence rule §2 in catalog) | `src/Dxs.Bsv/P2p/{Codec,Messages,FrameCodec,P2pNetwork,P2pAddress,P2pCommands,P2pDecodeException,Frame}.cs` |
| `bsv-p2p-session` | `bsv-protocol-core` (precedence rule §2) | `src/Dxs.Bsv/P2p/Session/` |
| `bsv-p2p-pool` | `bsv-protocol-core` (precedence rule §2) | `src/Dxs.Bsv/P2p/Pool/` |
| `bsv-p2p-chain` (new; Wave 1) | `bsv-protocol-core` | `src/Dxs.Bsv/P2p/Chain/` |
| `bsv-p2p-observer` (new; Wave 2) | `bsv-protocol-core` | `src/Dxs.Bsv/P2p/Observer/` |
| `consigliere-p2p-services` | `indexer-ingest-orchestration` | `src/Dxs.Consigliere/Services/P2p/` |
| `consigliere-p2p-data` | `indexer-state-and-storage` | `src/Dxs.Consigliere/Data/{P2p,Models/P2p}/` |
| `consigliere-p2p-tasks` | `indexer-ingest-orchestration` | `src/Dxs.Consigliere/BackgroundTasks/P2p/` |
| `consigliere-p2p-realtime` | `indexer-ingest-orchestration` | `src/Dxs.Consigliere/BackgroundTasks/Realtime/` |
| `consigliere-broadcast` | `public-api-and-realtime` (`BroadcastService.cs` is explicitly listed there) | `src/Dxs.Consigliere/Services/{IBroadcastService.cs,Impl/BroadcastService.cs}` |
| `consigliere-hub-public` | `public-api-and-realtime` | `src/Dxs.Consigliere/WebSockets/{IWalletHub.cs,WalletHub.cs,BroadcastReceiptDto.cs}` |
| `consigliere-admin-api` | `public-api-and-realtime` | `src/Dxs.Consigliere/Controllers/AdminP2pController.cs` + new admin controllers |
| `consigliere-tx-projection` | `indexer-state-and-storage` | `src/Dxs.Consigliere/Data/Transactions/TxLifecycleProjection*.cs` and journal |
| `consigliere-bitcoind-service` (W5 only) | `indexer-ingest-orchestration` (`BitcoindService.cs` is explicitly listed there) | `src/Dxs.Consigliere/Services/Impl/BitcoindService.cs` (broadcast removal touches this) |
| `external-broadcast-clients` (W5 only) | `external-chain-adapters` | `src/Dxs.Infrastructure/Bitails/BitailsRestApiClient.cs` and `src/Dxs.Infrastructure/WoC/WhatsOnChainRestApiClient.cs` (broadcast endpoint removal) |
| `consigliere-bootstrap` (W6 only) | `service-bootstrap-and-ops` | DI registration for new hosted services in `Setup/BsvP2pSetup.cs` and any `Configs/*.cs` additions |
| `admin-ui-pages` | not in current catalog — `src/admin-ui/**` is a separate SPA project; treat as its own out-of-catalog zone with the same handoff conventions | `src/admin-ui/` |
| `program-tests` | `verification-and-conformance` (catalog row "tests/**") | `tests/Dxs.Bsv.Tests/P2p/`, `tests/Dxs.Consigliere.Tests/` |
| `program-docs` | `repo-governance` (catalog row "docs/**") | `docs/platform-api/` (change notes, runbook) and `docs/stream-tasks/<wave-slug>/` |

**Note on the `src/admin-ui/` gap.** The repo catalog does not list a
zone for the admin SPA. This program treats `src/admin-ui/` as its
own out-of-catalog zone for the duration of Wave 4 and Wave 6 admin
pages. Adding `admin-ui` to the catalog is governance work (separate
change, owned by `repo-governance`); not blocking for this program but
should be raised post-Wave 6.

Handoff requirements (per `docs/repository-zones/handoff-contract.md`):

- **W1** crosses `bsv-protocol-core` (codec + session + chain), `indexer-state-and-storage` (block header documents), `indexer-ingest-orchestration` (hosted service for headers chain), `public-api-and-realtime` (new hub events). Handoff facts: frozen hub event signatures, frozen `PeerSession` extension surface, block-header document schema.
- **W2** crosses `bsv-protocol-core` (observer logic + new session callback bodies), `indexer-state-and-storage` (`TxObservationSource.P2p` enum + journal extension), `indexer-ingest-orchestration` (new ingest runner), `public-api-and-realtime` (source-tag in existing hub events). Handoff facts: source enum addition, journal append overload signature, runner DI registration.
- **W3** crosses `bsv-protocol-core` (reorg detector), `indexer-state-and-storage` (projection rebuilder coverage verification; rescan-state document if `DegradedReorgState` needs persistence), `indexer-ingest-orchestration` (rescan runner), `public-api-and-realtime` (already-frozen `OnReorg` hub event).
- **W4** crosses `indexer-state-and-storage` (metrics counter document), `indexer-ingest-orchestration` (metrics recorder + Bitails/JBus source-tag adjustments), `public-api-and-realtime` (admin endpoint), out-of-catalog `admin-ui`.
- **W5** crosses `public-api-and-realtime` (single `Broadcast` method, `BroadcastService` rework), `indexer-ingest-orchestration` (`BitcoindService.Broadcast` removal), `external-chain-adapters` (broadcast endpoints removed from `BitailsRestApiClient` / `WhatsOnChainRestApiClient`).
- **W6** crosses `bsv-protocol-core` (peer-scoring fields on `PeerRecord`, scoring loop in `PeerManager`), `indexer-ingest-orchestration` (alert poller hosted service, optional inbound listener service), `public-api-and-realtime` (alerts admin endpoint), `service-bootstrap-and-ops` (DI wiring for the new services and any new configs), out-of-catalog `admin-ui`, `repo-governance` (`docs/platform-api/` runbook + change notes).

## Program Waves

Sequential execution. Each wave produces its own
`docs/stream-tasks/<wave-slug>/{master.md,slices.md,launch-prompt.md}`
when its turn comes. The program package stays open across all waves.

### Wave 1 — `bsv-headers-chain-wave`
**Contract-freeze slice + headers chain tracker.** First slice declares
all hub events / subscription groups / `PeerSession` extension points
for the whole program (per Core Rule §7). Then headers chain via P2P,
persisted to RavenDB. New-block events exposed through
`WalletHub.OnNewBlock`. Provider-side block content fetch hooked to
header-tip event. Initial seed via Bitails REST by default; P2P-only
via config.
Touches: `bsv-p2p-session` (callbacks), `bsv-p2p-chain` (new),
`consigliere-p2p-services`, `consigliere-p2p-data`, `consigliere-hub-public`
(contract freeze + new event), `dxs-bsv-tests`.

### Wave 2 — `bsv-mempool-observer-wave`
**Journal contract extension slice + P2P observer.** First slice extends
the journal: `TxObservationSource.P2p` constant, source-neutral
`AppendAsync(TxObservation, raw?, source)` overload, projection rebuild
test proving `SeenBySources` accumulates `p2p`. Then `MempoolWatcher`
with deduplication, watchlist matcher (`HashSet<ulong>` + Raven
Subscription hot reload), getdata fetch policy. Bitails/JungleBus
runners get tagged so observations carry their source identity
consistently. **Watchlist correctness/scale validation** is a first-class
slice: address-output, address-input, STAS/DSTAS token output, deletes,
500 K-address load benchmark.
Touches: `bsv-p2p-session` (inv-tx callback, frozen surface from W1),
`bsv-p2p-observer` (new), `consigliere-p2p-services`,
`consigliere-p2p-tasks`, `consigliere-tx-projection` (source-enum
extension + write-path), `consigliere-p2p-realtime` (Bitails/JBus
source-tag adjustments), `dxs-bsv-tests`.

### Wave 3 — `reorg-handling-wave`
**Reorg detection over the headers chain + journal replay using
existing `Reorged` state + active re-broadcast of orphaned tx.**

No new lifecycle state is invented; the rebuilder already moves tx
from confirmed to `Reorged` on `BlockDisconnected` observations (see
`TxLifecycleProjectionRebuilder.cs` lines ~198–227). Wave 3 generates
the right `BlockDisconnected` observations from P2P-detected reorgs,
fetches orphaned block bodies from a provider, validates the content
against the orphaned header, and emits `WalletHub.OnReorg` + per-tx
`OnTransactionDeleted`.

After the projection has been moved to `Reorged`, Wave 3 fires a
**re-broadcast pass**: for every affected txid, look up raw bytes
(first `OutgoingTransactionStore` for our own tx, then
`RawTransactionPayloadStore` for observed watchlist tx with stored
raw), and announce via `inv` to all ready peers. This pushes the tx
back into the network mempool — peers that purged it on reorg will
`getdata` and re-accept, then re-relay. Subsequent mempool
observations flip the projection forward to `SeenInMempool` again,
closing the user requirement that orphaned tx end up back in mempool.

If raw bytes are not available for an orphaned tx (observed-but-not-
persisted), Wave 3 logs a warning, marks the tx with
`RebroadcastSkippedReason = "no_raw"`, and relies on natural
re-propagation from other peers (best-effort).

Beyond the ≤200-block window → explicit `DegradedReorgState` requiring
manual operator action (alert in W6).

Validation includes: 1-deep, 2-deep, N-deep loopback fork tests; deep
reorg beyond window (degraded state asserted); provider returning
mismatching block body for orphaned hash (refuse, alert); idempotency
of repeated `BlockDisconnected` events; **re-broadcast test: tx
inserted as own-outgoing + confirmed, fork orphaning the block,
assert announce(inv) was sent on each ready peer for that txid, and
mock-peer's getdata returns same raw bytes**; same with observed-tx
where raw was persisted in `RawTransactionPayloadStore`; same where
raw is missing (warning + `RebroadcastSkippedReason` set).

Touches: `bsv-p2p-chain` (reorg detector), `consigliere-p2p-services`
(rescan + observation translator + re-broadcast loop),
`consigliere-p2p-data` (`RebroadcastSkippedReason` field on
projection or sidecar doc — decide at wave-open), `consigliere-hub-public`
(already-frozen `OnReorg`), `consigliere-tx-projection` (verify
rebuilder coverage; minor extension only if `DegradedReorgState`
requires a new event type).

### Wave 4 — `observation-source-metrics-wave`
Per-source stats recorded **before** dedupe: first-seen counts, lag
histograms, only-saw counters. Persisted in Raven (rolling window) +
exposed by admin endpoint and a new admin SPA page. Validation uses
fixture-injected observations to confirm counters match exactly (no
"plausibly fires" language).
Touches: `consigliere-p2p-services` (metrics recorder),
`consigliere-admin-api`, `consigliere-p2p-realtime` (already source-tagged
in W2), `admin-ui`.

### Wave 5 — `broadcast-unification-wave`
Collapse `Broadcast(hex)` and `BroadcastTracked(hex)` into a single
`Broadcast(hex) → BroadcastReceiptDto`. Remove legacy HTTP-provider
broadcast paths (`BitcoindService.Broadcast`, Bitails/WoC broadcast
clients). Wave 5 explicitly **depends on W2** for the source attribution
needed inside the unified broadcast lifecycle, and **depends on W4** so
operator metrics are present at the moment the HTTP fallback disappears
(no surprises if P2P pool dips). Backward compatibility for clients
intentionally not preserved (vnext); change documented in W6's public
API change notes.
Touches: `consigliere-broadcast`, `consigliere-hub-public` (already-frozen
`Broadcast` signature), `bsv-p2p-pool` (minor wiring),
`Dxs.Infrastructure` (cleanup of unused clients).

### Wave 6 — `production-ops-wave`
Operator-grade hardening that the goal statement requires but earlier
waves intentionally defer: peer scoring + rotation in `PeerManager`;
critical alerts (pool size, relay-back rate, reorg depth, source-first
dropout); inbound listener decision for Consigliere (opt-in or off);
operator runbook covering new admin pages; public-API change notes for
the `Broadcast` contract; soak documentation referencing the existing
`thin-node-gate2-soak-runbook.md`.
Touches: `bsv-p2p-pool` (scoring), `consigliere-p2p-services` (alert
poller, optional inbound runner), `consigliere-admin-api` (alert
endpoint), `admin-ui` (alerts panel), `docs/platform-api/` (change notes
+ runbook), `consigliere-tests`.

## Cross-Wave Dependency Rules

- Wave 1 must close before Wave 3 (reorg needs headers chain).
- Wave 1 must close before Wave 2 (contract freeze first).
- Wave 2 must close before Wave 3 (reorg revert touches observed tx
  source attribution).
- Wave 2 must close before Wave 4 (metrics need source observations).
- Wave 2 must close before Wave 5 (broadcast unification reuses
  source-tagged journal).
- Wave 4 must close before Wave 5 (metrics in place before HTTP
  fallback is removed).
- **Wave 5 must close before Wave 6.** Committed order is W5 → W6.
  Rationale: W6 publishes the public `Broadcast` change notes, which
  can only be finalised after W5 actually lands the new signature; W6
  also depends on the source-metrics dashboard from W4 being able to
  show three sources during the cut-over.
  - Note on operator safety: peer scoring and pool alerts are
    *useful* before W5 ships, but the current peer pool already
    persists per-peer attempt history via `PeerRecord`
    (`src/Dxs.Bsv/P2p/Pool/PeerRecord.cs`), so cold-start operators
    can still recover from a bad pool by restarting before W6. The
    program accepts that small risk in exchange for a clean
    dependency chain. Operators who need W6 earlier can run it
    in parallel with W5 *only* if they accept that W6's change-notes
    slice will be re-opened once the W5 signature is final.
- All hub event additions and `PeerSession` extension points are
  pre-declared in Wave 1's contract-freeze slice. Subsequent waves only
  implement against the frozen surface — they do not add new ones
  silently.

## Program Ledger

| wave | slug | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|---|
| 1 | `bsv-headers-chain-wave` | bsv-protocol-core | done | — | tip sync ≤10s; OnNewBlock fired per mainnet block within p95 lag (target ≤2s, measured over 24h soak) | contract-freeze slice closed; headers persisted; new-block event live; admin endpoint matches WoC tip | A2-followup-2 APPROVE |
| 2 | `bsv-mempool-observer-wave` | indexer-ingest-orchestration | done | W1 | journal accepts `p2p` source; watchlist load benchmark ≥500 K addresses ≤2s; observed-tx event with `SeenBySources` containing `p2p` | journal contract extended; observer live; watchlist hot reload + scale test green | CLOSED — A1 closed (3 rounds), A2 closed, A2-followup APPROVE WITH CHANGES (new-L1 closed) |
| 3 | `reorg-handling-wave` | indexer-state-and-storage | done | W1, W2 | 1/2/N-deep fork tests; deep-reorg-beyond-window asserts degraded state; mismatched-body provider rejected (N/A — S2 deferred); idempotent disconnect events; low-difficulty fork rejected by chainwork (A2 C1); fork tip promoted to active in-memory AND durably (A2 C2 + A2-followup N1); restart-safe via persistent active-tip pointer + walk-back (A2-followup-2 N1); coinbase explicit-skip (A2 H2 + A2-followup N2) | reorg detector live; `Reorged` state produced via existing rebuilder; `OnReorg` event fired AFTER rebuilder runs (Core Rule §9 enforced strictly via shared sequence counter); active tip swapped via `HeadersChain.PromoteFork` + durable `SetActiveTipAsync`; orphaned tx re-announced via `TxRelayCoordinator.AnnounceAsync` with explicit coinbase exclusion via `ICoinbaseProbe`; concurrent header arrivals serialized via `SemaphoreSlim`; partial-failure rollback wired. **CLOSED**: A2 + A2-followup + A2-followup-2 + A2-followup-3 all folded | CLOSED — A2-followup-3 APPROVE WITH CHANGES |
| 4 | `observation-source-metrics-wave` | indexer-ingest-orchestration | done | W2, W3 | fixture-injected observations match counter values exactly; lag histogram bucket counts deterministic across all 6 buckets; per-source InvObserved + first-seen + only-saw counts pinned by `SourceMetricsEndToEndFixtureTests` | `SourceMetricsAggregator` hosted service live; `AdminMetricsController` (`GET /api/admin/metrics/sources`) exposes latest snapshot + `?lastN=N` history; tracker hooked at `TxObservationJournalWriter` tail (both overloads); SPA page deferred (operator-deferred per W2 / W3 pattern). Implementation closed pending Codex A2 audit. | A2 pending |
| 5 | `broadcast-unification-wave` | indexer-write-path | not_opened | W2, W4 | grep shows no legacy broadcast HTTP-provider paths; real mainnet tx confirmed via single `Broadcast` method end-to-end | single `Broadcast` method; HTTP-provider clients removed | A1 |
| 6 | `production-ops-wave` | indexer-ingest-orchestration | not_opened | W2 (W5 recommended) | alert fires when pool drops below threshold in fixture; peer rotation evicts low-scoring peer in fixture; runbook reviewed | peer scoring live; alerts wired; runbook + change notes published | A1 |

Status vocabulary: `not_opened`, `todo`, `in_progress`, `blocked`, `done`, `stale`.

## Definition of Done

Program is `done` when:
- all six waves are `done` or intentionally `not_opened`
- `tests/Dxs.Bsv.Tests` and `tests/Dxs.Consigliere.Tests` pass with no
  new failures vs baseline (delta counted as residual)
- a real BSV mainnet transaction broadcast survives the full lifecycle:
  Submitted → PeerAcked → MempoolSeen → Mined → Confirmed, with events
  visible via SignalR
- admin panel shows healthy P2P pool with non-trivial peer rotation,
  recent headers tip, mempool observation rate, per-source metrics,
  active alerts (if any)
- closeout evidence in `evidence/closeout.md` lists end-state metrics,
  delivery hashes, residuals, and operator-facing changes
- public API change notes for the `Broadcast` contract are published in
  `docs/platform-api/`

## Open Questions (answers locked in by program author)

1. **Watchlist size ceiling.** Program targets ≤500 K addresses (~12 MB
   `HashSet<ulong>` + Raven verify on hit). 1 M is the soft upper
   tested in W2's scale slice — beyond that we revisit bloom or
   per-tenant sharding in a follow-up.
2. **Post-reorg canonical state.** `Reorged`. Existing
   `TxLifecycleProjectionRebuilder.BlockDisconnected` already produces
   it. W3 does not invent a new state.
3. **Block-body providers under reorg.** Bitails/JungleBus serve
   orphaned bodies for at least 24 h after the reorg (verified at W3
   open; if not, W3 falls back to P2P `getdata(MSG_BLOCK)` for the
   orphan body, which is expensive but works).
4. **`Broadcast` return-type change compatibility.** No backwards-compat.
   This is vnext. Change notes in W6 list the new signature for
   consuming businesses.
5. **Alerting backend.** Metrics-first via existing logging + admin
   endpoints; alert delivery is webhook-pluggable in W6 (concrete
   backend selection deferred — operator-config decision, not program
   decision).
6. **Inbound listener in Consigliere.** Moved into W6 explicitly. The
   tool layer (`tools/BsvBroadcastNode/PeerNodeHost.cs`) keeps its
   listener for dev/test; integration into Consigliere is opt-in per
   operator deployment posture.

## Delivery Notes

Commit hashes recorded here as waves close.

- Program package created: `<hash-pending>` (this commit)
- Program audit A1 (Codex GPT-5, MAJOR REVISION REQUIRED): see
  `audits/program-audit-A1.md`
- Program revision per A1: `<hash-pending>` (this commit)
- Wave 1 delivery (`bsv-headers-chain-wave`): closed 2026-05-17 — final commit `f87da92` (full chain: c7b1428 S0, aafbec6 S1, 79aaaf6 S2, 332ca13 S3, 407e1fb S4, 7cbf2d7 S5, 8036bb7 S6, 8babc11 S7 scaffold, 557f8ad closeout, 4997122 A2 fix, 774aaa4 A2-followup fix, f87da92 ledger). Wave audit A2-followup-2 APPROVE.
- Wave 2 delivery (`bsv-mempool-observer-wave`): CLOSED. 9 slices delivered (S8 deferred per `evidence/live-validation.md`); chain `7344761` (package) → `9fbcaff` / `a44b689` / `ed91617` (pre-S0 audit revisions) → `2a81474` / `9a94a97` (S0) → `fdbc8cd` (S1) → `a3aface` (S2) → `09bfff0` (S3) → `075827f` (S4) → `3bcc6e3` (S5) → `9a258ab` (S6) → `992b1b4` (S7) → `1bd7ee2` (closeout) → `16515cf` (A2 revision: TxHashOrder display-order normalisation, runner uses S1 parser, TxRelayCoordinator depends on BsvP2pHealth, BroadcastServiceP2pWirerHost invoker, runner E2E tests) → this commit (A2-followup new-L1 fix: split rate-limit test into `ForgetsTxid_NotRetainedInDedupe` + `RetriesAfterWindowSlides`). Wave audit A2-followup APPROVE WITH CHANGES; new-L1 closed.
- Wave 3 delivery (`reorg-handling-wave`): **CLOSED 2026-05-18**. S0 + S1 + S3 + S4 + S5 + S6 delivered; S2 deferred mid-wave (P2P block-body fetch is GB-scale, projection `BlockHash` index used instead); S7 operator-deferred. Full audit chain folded: A2 → A2-followup → A2-followup-2 → A2-followup-3 APPROVE WITH CHANGES. Commit chain `92ead7d` (package) → `091299e` (S0) → `bfac702` (S1) → `9c64818` (S3+S4+S5+S6) → `1fab0ef` (closeout draft) → `e9d2691` (A2 prompt) → `0560714` (A2 revision: C1 chainwork + C2 PromoteFork + H1 IProjectionRebuilder + H2 ICoinbaseProbe + H3 SemaphoreSlim + M1 transactional + M3 strict-sequence + L1 DI assertions) → `5c82dfa` (A2-followup: N1 persistent active-tip pointer + N2 consensus-signature coinbase probe + H2 explicit tests + M1 durable-commit-last) → `14afd25` (A2-followup-2: N1 walk-back-via-PrevHash + M1 promote-fork-durable rollback + N3 spike + N4 doc + M2 closeout name) → this commit (A2-followup-3: N5 production-path regression tests + spike nullability). Test counts: `Dxs.Bsv.Tests` 220 (+13 from pre-W3 baseline), `Dxs.Consigliere.Tests` 349 passed (+34 from pre-W3 baseline). 3 pre-existing Raven embedded-runtime baseline failures unchanged. W4 (`observation-source-metrics-wave`) and W5 (`broadcast-unification-wave`) may now open per the dependency graph.
- Wave 4 delivery (`observation-source-metrics-wave`): A2 closed (8/8 findings folded). S0 + S1 + S2 + S3 + S4 + S5 + S6 + S7 delivered; S8 (SPA page) deferred per W2 / W3 pattern. Chain `26a823f` (package + A1 prompt) → `9fafd5a` (S0+S1+S2; 27 unit tests) → `7cc1385` (S3+S4+S5+S6+S7; 18 more tests; closeout + A2 prompt) → this commit (A2 revision: H1 TryAdd-pattern in tracker + concurrent regression test, H2 lock-aware eviction + race test, H3 source-observed-at timestamp + journal-writer hook tests, M1 immutable bucket bounds, M2 lastN clamp + AdminMetricsController tests, M3 ISnapshotPersistence abstraction + aggregator round-trip tests, L1 distinct-window DI flow-through assertion). Test counts: `Dxs.Bsv.Tests` 220 unchanged, `Dxs.Consigliere.Tests` 412 passed (+63 from W3 close). Metrics-filtered: 65/65 passed. Wave audit A2-followup pending.
- Wave 5 delivery: (pending)
- Wave 6 delivery: (pending)
- Program closeout commit: (pending)
