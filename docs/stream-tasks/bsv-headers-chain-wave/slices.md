# Wave 1 Slices — `bsv-headers-chain-wave`

Eight slices. S0 is the **prerequisite contract-freeze slice** — every
other slice has `depends_on = S0`. S1-S7 are ownership-safe and several
can run in parallel after S0 closes (see dependency graph at the end).

## S0 — Program-Wide Contract Freeze (prerequisite)

**Intent.** Pre-declare every hub event, subscription group,
`PeerSession` callback, and `PeerTelemetry` field that **any** wave in
this program will need. Bodies are stubs / no-ops where downstream
waves own implementation. This is the audit-A2 N2 fix: a single
locked surface, so W2-W6 never touch `PeerSession.cs` or
`IWalletHub.cs` to add new entries.

**Owned paths.**

- `src/Dxs.Bsv/P2p/Session/PeerSession.cs` — add observation callback
  properties (signatures only; existing receive loop dispatches to
  them after frame parse):
  - `Action<IReadOnlyList<BlockHeader>>? OnHeadersReceived`
  - `Action<InvMessage>? OnInvReceived` — **single** typed callback;
    consumers filter by `InvVector.Type`. Block-inv and tx-inv go
    through the same path.
  - `Action<RejectMessage>? OnRejectReceived`
- `src/Dxs.Bsv/P2p/Chain/PeerTelemetry.cs` (new) — snapshot record
  with the full field set from program `master.md` §Core Rules §7:
  `BytesIn`, `BytesOut`, `LastRecvUtc`, `LastSendUtc`,
  `PingRttP50Ms`, `PingRttP95Ms`, `GetDataServedCount`,
  `RejectReceivedCount`, `LastDisconnectReason`.
- `src/Dxs.Bsv/P2p/Chain/IPeerTelemetrySink.cs` (new) — interface:
  - `void RecordBytesIn(int n)` / `void RecordBytesOut(int n)`
  - `void RecordPingRtt(TimeSpan rtt)`
  - `void RecordGetDataServed()`
  - `void RecordRejectReceived(RejectClass cls)`
  - `void RecordDisconnect(DisconnectReason reason)`
  - `PeerTelemetry Snapshot()`
- `src/Dxs.Bsv/P2p/Chain/NullPeerTelemetrySink.cs` (new) — no-op
  implementation used until W6 ships the production sink.
- `PeerSession.cs` — add `IPeerTelemetrySink Telemetry { get; }`
  property; ctor takes a sink (default `NullPeerTelemetrySink`).
  Send/receive loops call `RecordBytesIn` / `RecordBytesOut` on each
  frame so W4 can read aggregates from the existing path without
  re-instrumenting. Ping reply handler calls `RecordPingRtt`.
- `src/Dxs.Consigliere/WebSockets/IWalletHub.cs` — add:
  - `Task OnNewBlock(BlockTipDto tip)` (consumed by W1)
  - `Task OnReorg(ReorgEventDto reorg)` (signature only; body in W3)
- `src/Dxs.Consigliere/WebSockets/BlockTipDto.cs` (new) — record:
  `{ string Hash, long Height, long TimestampMs, string PrevHash, int HeaderSize }`.
- `src/Dxs.Consigliere/WebSockets/ReorgEventDto.cs` (new) — record:
  `{ string CommonAncestorHash, long CommonAncestorHeight, string[] OrphanedHashes, string NewTipHash, long NewTipHeight, bool DegradedState }`.
- `src/Dxs.Consigliere/WebSockets/WalletHub.cs` — add subscription
  groups: `block:tip`, `block:reorg`. `SubscribeToBlockTip()` /
  `SubscribeToReorg()` server methods exposed via `IWalletServer`.
- `src/Dxs.Consigliere/WebSockets/IWalletServer.cs` — add
  `SubscribeToBlockTip` and `SubscribeToReorg` declarations.
- `src/Dxs.Consigliere/WebSockets/BroadcastReceiptDto.cs` — add a
  comment block marking the shape as **frozen for W5**; no field
  changes.

**Exact tasks.**

1. Add the three new callback properties on `PeerSession`. Wire the
   receive loop so `headers` frames parse to `HeadersMessage` and
   invoke `OnHeadersReceived?.Invoke(headersMsg.Headers)`; `inv`
   frames parse to `InvMessage` and invoke `OnInvReceived?.Invoke(inv)`;
   `reject` frames parse to `RejectMessage` and invoke
   `OnRejectReceived?.Invoke(rejectMsg)`.
2. Add `PeerTelemetry` record, `IPeerTelemetrySink` interface, and
   `NullPeerTelemetrySink` no-op implementation under
   `src/Dxs.Bsv/P2p/Chain/`.
3. Add `IPeerTelemetrySink Telemetry` property to `PeerSession`. Ctor
   accepts the sink. Existing call sites (`PeerManager`,
   `BsvP2pHostedService`) pass `NullPeerTelemetrySink.Instance` for
   now. Call `RecordBytesIn` / `RecordBytesOut` from the send/receive
   loops; `RecordPingRtt` from the existing pong handler.
4. Add `BlockTipDto`, `ReorgEventDto` records.
5. Add `OnNewBlock` and `OnReorg` to `IWalletHub` with no-op stubs in
   `WalletHub.cs` (group-broadcast for `OnNewBlock`; `OnReorg` body
   is `Task.CompletedTask` until W3).
6. Add subscription methods `SubscribeToBlockTip` and
   `SubscribeToReorg` on `IWalletServer` + implementations in
   `WalletHub.cs` that add the caller's connection ID to the
   corresponding group.
7. Add the frozen-shape comment on `BroadcastReceiptDto`.

**Out of scope (S0).** Any header chain logic. Any document or store.
Any business behaviour. S0 only ships type signatures + dispatchers
+ default sinks.

**Validation.**

- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- `dotnet test` baseline green (no new failures vs the pre-S0
  baseline; record the baseline counts in S0's evidence).
- Reflection-based smoke test: a small xUnit test in
  `tests/Dxs.Consigliere.Tests/P2p/ContractFreezeTests.cs` asserts
  the existence of every declared member on `IWalletHub`,
  `PeerSession`, and `IPeerTelemetrySink` via `typeof(...)
  .GetMember(...)`. This catches accidental rename / removal in
  later waves.
- Static check: grep for `OnBlockInvReceived` or `OnInvReceived(tx)`
  patterns — must return zero hits (audit A2 N2 reconciliation).

**Done when.** All declared surfaces compile, build is green, tests
green, contract-freeze reflection test green, slice-level audit
`audits/S0-A1.md` returns APPROVE.

## S1 — Headers Chain Pure Logic

**Intent.** Pure object that parses, validates, and links
`BlockHeader` records into an in-memory chain. No I/O, no Raven, no
hub. Just the algorithm.

**Owned paths.**

- `src/Dxs.Bsv/P2p/Chain/BlockHeaderHasher.cs` (new) — static helper:
  `byte[] Hash(BlockHeader header)` returns double-SHA-256 of the
  80-byte header.
- `src/Dxs.Bsv/P2p/Chain/HeadersChain.cs` (new) — in-memory chain
  state. API surface:
  - `TryExtend(BlockHeader hdr, out ExtendResult result)`:
    - returns `Extended` when `hdr.prev_block == current_tip`,
      header hash meets target bits, and the resulting height is one
      above current tip;
    - returns `AlreadyKnown` if header is the current tip or older
      member with matching hash;
    - returns `Fork` if `hdr.prev_block` matches a non-tip ancestor
      in the retained window — caller stores it as a competing tip
      candidate (recovery deferred to W3);
    - returns `Orphan` if `hdr.prev_block` is unknown (parent below
      retained window or in an unrelated chain) — caller asks
      provider/peer for missing headers in S3;
    - returns `Invalid(reason)` on PoW failure or malformed header.
  - `BlockHeader Tip { get; }`, `long TipHeight { get; }`.
  - `IReadOnlyList<BlockHeader> RecentHeaders(int n)`.
  - `void LoadFromStore(IReadOnlyList<(BlockHeader hdr, long height)> persisted)` — replay-from-Raven on startup.
- `src/Dxs.Bsv/P2p/Chain/HeadersChainOptions.cs` (new) — config
  record: `RetainedHeaderCount` (default 200),
  `GetHeadersIntervalMs` (default 30_000), `SeedFromBitails` (default
  true), `BootstrapTimeoutMs` (default 10_000).

**Out of scope.** Persistence (S2), hosted-service wiring (S3),
hub events (S5), admin endpoints (S6).

**Validation.**

- Unit tests in `tests/Dxs.Bsv.Tests/P2p/Chain/HeadersChainTests.cs`:
  - extends a clean tip with a valid header;
  - rejects header whose `prev_block` doesn't match tip (fork or
    orphan path);
  - rejects header whose hash fails the target check;
  - returns `AlreadyKnown` on duplicate of tip and of an ancestor;
  - returns `Fork` when prev matches an ancestor still in the
    retained window;
  - returns `Orphan` when prev is unknown;
  - `LoadFromStore` populates state without revalidating ancestors;
  - `RecentHeaders(n)` returns headers in correct height order.
- Branch coverage: every `ExtendResult` value exercised at least
  once.

**Done when.** All `HeadersChain` paths covered; `BlockHeaderHasher`
roundtrips against three known mainnet header bytes (use fixture from
existing `tests/Dxs.Bsv.Tests/` block fixtures if available, else
canned constants).

## S2 — `BlockHeaderDocument` + `BlockHeaderStore`

**Intent.** Raven persistence for accepted headers. Single document
per header keyed by hash. Index on height for tip / range queries.

**Owned paths.**

- `src/Dxs.Consigliere/Data/Models/P2p/BlockHeaderDocument.cs` (new):
  `{ string Id /* "block-headers/<hash-hex>" */, string Hash,
   long Height, string PrevHash, long TimestampMs, byte[] HeaderBytes80 }`.
- `src/Dxs.Consigliere/Data/P2p/BlockHeaderStore.cs` (new):
  - `Task SaveAsync(BlockHeaderDocument doc, CancellationToken)`
  - `Task<BlockHeaderDocument?> GetByHashAsync(string hash, CancellationToken)`
  - `Task<BlockHeaderDocument?> GetTipAsync(CancellationToken)` —
    by height descending, take 1.
  - `Task<IReadOnlyList<BlockHeaderDocument>> RecentAsync(int count, CancellationToken)`
  - `Task PruneBelowAsync(long minHeight, CancellationToken)` —
    deletes headers with `Height < minHeight`.
- Static Raven index (auto-generated abstract class is fine) on
  `Height` descending. If the existing repo uses a project-wide
  convention for auto-indexes, follow that — don't introduce a new
  pattern.

**Out of scope.** Caching / read-through layer. The store goes
straight to Raven for now; a cache wrapper can land later if soak
data shows latency.

**Validation.**

- Integration test against an embedded RavenDB instance
  (`tests/Dxs.Consigliere.Tests/P2p/BlockHeaderStoreTests.cs`):
  - save + get-by-hash round-trip;
  - `GetTipAsync` returns highest-height doc;
  - `RecentAsync(50)` returns up to 50 docs, ordered tip-first;
  - `PruneBelowAsync` deletes only docs strictly below the cutoff.

**Done when.** All tests green. No new failures elsewhere.

## S3 — `HeadersChainService` Hosted Service

**Intent.** Wire the pure `HeadersChain` to the live P2P pool. On
startup: load persisted headers into `HeadersChain.LoadFromStore`. On
`OnInvReceived(InvMessage)` from any peer with `MSG_BLOCK` items:
send `getheaders` on a ready peer. On `OnHeadersReceived` from any
peer: feed headers to `HeadersChain.TryExtend` one at a time; for
each `Extended`, persist via `BlockHeaderStore.SaveAsync` and raise
the new-block event via an internal `INewBlockNotifier` (consumed by
S5). Periodic timer (`HeadersChainOptions.GetHeadersIntervalMs`)
sends a baseline `getheaders` to detect missed tips.

**Owned paths.**

- `src/Dxs.Consigliere/Services/P2p/HeadersChainService.cs` (new) —
  `PeriodicTask` or `BackgroundService` depending on repo
  convention. Subscribes to `PeerSession.OnHeadersReceived` /
  `OnInvReceived` for every peer that comes ready (use
  `PeerManager` event or scan ready peers each tick — pick the
  cheaper one in code review).
- `src/Dxs.Consigliere/Services/P2p/INewBlockNotifier.cs` (new) —
  one-liner interface: `Task NotifyAsync(BlockTipDto tip)`.
- `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` (extend) — register
  `HeadersChain`, `HeadersChainOptions`, `BlockHeaderStore`,
  `HeadersChainService`, `INewBlockNotifier` (impl from S5).

**Out of scope.** Reorg recovery (W3). Block-body fetch (W3 / W2).
Bitails bootstrap (that's S4, sequenced separately).

**Validation.**

- Integration test against a `MiniBsvServer`-style fake peer
  (`tests/Dxs.Consigliere.Tests/P2p/HeadersChainServiceTests.cs`):
  - fake peer sends `inv(MSG_BLOCK)` for a known header → service
    sends `getheaders` on that peer;
  - fake peer responds with `headers` containing one valid
    extension → store gains one document and
    `INewBlockNotifier.NotifyAsync` is invoked once with the
    correct `BlockTipDto`;
  - fake peer sends a competing tip (fork) → both header docs
    persisted, no exception, no notifier invoke (recovery is W3);
  - fake peer sends an orphan header → service requests preceding
    headers (or logs and skips, depending on operator decision —
    document the choice in S3's commit message).

**Done when.** Tip advances live in test fixture; `block:tip`-bound
notifier fires exactly once per accepted extension.

## S4 — Bitails Bootstrap Seed

**Intent.** On cold start (no persisted headers), fetch the current
chain-info from Bitails and seed a starting tip. After bootstrap,
the pure-P2P path takes over.

**Owned paths.**

- `src/Dxs.Consigliere/Services/P2p/HeadersChainBootstrapper.cs`
  (new) — invoked by `HeadersChainService` startup if store is
  empty and `HeadersChainOptions.SeedFromBitails == true`.
- Reuse existing `BitailsRestApiClient`. If a `/chain/info` method
  doesn't already exist, add the smallest possible addition there
  with handoff acknowledgement to `external-chain-adapters`
  (record in delivery notes).

**Out of scope.** Full historical sync. We seed exactly the current
tip and (optionally) the trailing `RetainedHeaderCount` headers if
Bitails exposes them cheaply; otherwise we seed just the tip and let
the P2P `getheaders` loop fill the trailing window.

**Validation.**

- Unit test with a canned Bitails JSON response: bootstrapper feeds
  one or more headers into the chain + store, tip height matches the
  canned response.
- Pure-P2P-mode test: with `SeedFromBitails=false` and an empty
  store, service starts without any Bitails HTTP calls (assert via
  mock client) and reaches tip via the fake peer's `getheaders`
  flow.

**Done when.** Bootstrap fills store within `BootstrapTimeoutMs`;
pure-P2P mode operates cleanly with no Bitails traffic.

## S5 — `OnNewBlock` Live + `OnReorg` Stubbed

**Intent.** Implement the `INewBlockNotifier` registered in S3 as a
SignalR-pump that calls `IWalletHub.OnNewBlock(tip)` against the
`block:tip` group. Confirm `OnReorg` is registered and reachable but
delivers nothing in W1.

**Owned paths.**

- `src/Dxs.Consigliere/Services/P2p/HubNewBlockNotifier.cs` (new) —
  `INewBlockNotifier` impl that takes `IHubContext<WalletHub,
  IWalletHub>` and broadcasts to the `block:tip` group.
- `src/Dxs.Consigliere/WebSockets/WalletHub.cs` (extend) — flesh out
  `SubscribeToBlockTip` body if not done in S0; ensure
  `SubscribeToReorg` still adds to `block:reorg` group with no
  immediate broadcast.

**Out of scope.** Reorg body (W3).

**Validation.**

- SignalR integration test
  (`tests/Dxs.Consigliere.Tests/P2p/HubNewBlockTests.cs`):
  - client subscribes to `block:tip`, fake peer feeds a header
    through the service, client receives one `OnNewBlock` call with
    the expected DTO;
  - client subscribes to `block:reorg`, no message ever arrives in
    W1 — assert silence for 2 s after a header-extend.

**Done when.** Live SignalR event arrives end-to-end in fixture;
reorg subscription is plumbed but quiet.

## S6 — Admin Endpoints

**Intent.** Operators can read the current tip and recent headers
through the admin API (and eventually the admin UI, but UI work for
this dashboard is out of scope for W1 — that lands in W4 alongside
the source-metrics page).

**Owned paths.**

- `src/Dxs.Consigliere/Controllers/AdminP2pController.cs` (extend):
  - `GET /api/admin/p2p/headers/tip` →
    `{ hash, height, timestampMs, prevHash }`
  - `GET /api/admin/p2p/headers/recent?count=N` →
    array (N capped at 200).

**Out of scope.** Admin UI page for headers (W4 or later). Reorg
state read API (W3).

**Validation.**

- Controller test with seeded `BlockHeaderStore`: tip endpoint
  matches highest-height doc; recent endpoint returns up to N most
  recent docs in height-descending order; `count` is clamped to
  `RetainedHeaderCount`.
- Auth behaviour identical to the existing `AdminP2pController`
  endpoints (re-uses the same `[Authorize]` attributes).

**Done when.** Endpoints respond with correct JSON shapes; auth
behaves as in the rest of `AdminP2pController`.

## S7 — `HeadersSoakRecorder` Harness + 24 h Soak

**Intent.** Validate the p95-lag claim on real mainnet. Throwaway
recorder under `tests/Spikes/P2p/HeadersSoakRecorder/` — does not
ship in production binaries.

**Owned paths.**

- `tests/Spikes/P2p/HeadersSoakRecorder/Program.cs` (new) — console
  app:
  - boots a minimal Consigliere headers-chain service against
    mainnet peers (reuses the `PeerManager` + `HeadersChainService`
    from this wave);
  - subscribes to `INewBlockNotifier` via a fake hub
    implementation that just writes `now_recv_utc + tip` to a
    rolling JSONL file;
  - polls `https://api.whatsonchain.com/v1/bsv/main/chain/info` at
    1 s cadence; on each height change appends `now_explorer_utc
    + height + best_block_hash` to the same JSONL.
- `tests/Spikes/P2p/HeadersSoakRecorder/analyze.fsx` (or `.ps1` /
  `.py` — pick the lightest tool already used in repo spikes) —
  reads the JSONL, joins by height, computes p50 / p95 / p99 lag.
- `docs/stream-tasks/bsv-headers-chain-wave/evidence/headers-soak.md`
  (new) — written at end of soak with the metrics and a short
  prose summary.

**Out of scope.** Production telemetry, alerting, anything that
ships outside `tests/Spikes/`.

**Validation.**

- Recorder runs ≥ 24 h on a fresh DigitalOcean droplet (same class
  as the Gate 2 soak runbook).
- Computed p95 lag over the soak window ≤ 2 s.
- Tip-hash recorded by the recorder matches WhatsOnChain at the end
  of the soak.

**Done when.** `evidence/headers-soak.md` exists with measured
numbers and a delivery hash; the recorder code is committed but
flagged in its README as a spike (no production references).

## Dependency Graph

```
                ┌────────────────────────────────────────────┐
                │ S0 — Program-Wide Contract Freeze          │
                │ (prerequisite; all others depend on this)  │
                └─────┬──────────────────────────────────────┘
                      │
        ┌─────────────┼─────────────────┐
        ▼             ▼                 ▼
   ┌─────────┐  ┌─────────────┐  ┌──────────────────┐
   │ S1      │  │ S2          │  │ S4 (depends only │
   │ Headers │  │ Header doc  │  │ on S2 for store; │
   │ chain   │  │ + store     │  │ Bitails seed)    │
   │ logic   │  │             │  │                  │
   └────┬────┘  └──────┬──────┘  └────────┬─────────┘
        │              │                  │
        └──────┬───────┘                  │
               ▼                          │
         ┌──────────────┐                 │
         │ S3           │ ◄───────────────┘
         │ Chain hosted │
         │ service      │
         └─────┬────────┘
               │
       ┌───────┼───────┐
       ▼       ▼       ▼
   ┌──────┐ ┌──────┐ ┌──────┐
   │ S5   │ │ S6   │ │ S7   │
   │ Hub  │ │ Admin│ │ Soak │
   │ event│ │ API  │ │ spike│
   └──────┘ └──────┘ └──────┘
```

Slices that can run in parallel after S0:

- S1 ⊥ S2 ⊥ S4 (Bitails part can begin once S2 is closed, but is
  independent of S1).
- S5, S6, S7 are independent of each other once S3 closes.

Default sequential order: S0 → S1 → S2 → S3 → S4 → S5 → S6 → S7,
because the operator (you) prefers strict stop-and-audit. Parallelism
is only an option if an audit pass is run per opened slice.

## Per-slice Audit Rules

- S0 receives its own slice-level audit at `audits/S0-A1.md` per the
  program's prerequisite-slice gate. **No main slice opens until S0's
  slice audit returns APPROVE.**
- S1-S7 are covered by the single wave-level audit at `audits/A1.md`
  after all slices are `done`.
- If any slice surfaces a residual that requires a fix-and-re-audit
  pass, open `audits/A2.md`.

## Validation Matrix (wave-level)

| signal | slice | how validated |
|---|---|---|
| Frozen contract surface present | S0 | reflection test asserts every member exists; grep proves no `OnBlockInvReceived` / `OnInvReceived(tx)` leftovers |
| Header validation correctness | S1 | `HeadersChainTests` covers every `ExtendResult` branch |
| Headers persistence | S2 | embedded-Raven integration test |
| Tip advances on live `headers` | S3 | hosted-service test with fake peer |
| Bootstrap path | S4 | canned Bitails JSON → seeded store; `SeedFromBitails=false` → no HTTP |
| `OnNewBlock` reaches SignalR clients | S5 | end-to-end SignalR test |
| Admin API responses | S6 | controller test against seeded store |
| Soak p95 ≤ 2 s | S7 | `evidence/headers-soak.md` records computed p95 over 24 h |
| Build green | every | `dotnet build Dxs.Consigliere.sln -c Release` 0 errors |
| Tests green | every | `dotnet test` no new failures vs baseline |
