# Wave 1 Slices — `bsv-headers-chain-wave`

Revised 2026-05-17 per wave audit A1.

Eight slices. S0 is the **prerequisite contract-freeze slice** — every
other slice has `depends_on` that includes S0 explicitly. S1-S7 are
ownership-safe and several can run in parallel after S0 closes (see
dependency graph at the end).

## S0 — Program-Wide Contract Freeze (prerequisite)

**Intent.** Pre-declare every hub event, subscription group,
`PeerSession` callback, send-helper, and `PeerTelemetry` field that
**any** wave in this program will need. Bodies are stubs / no-ops
where downstream waves own implementation. This is the audit-A2 N2
fix expanded per wave audit A1 H1/H2: a single locked surface, so
W2-W6 never touch `PeerSession.cs` or `IWalletHub.cs` to add new
entries.

### S0.1 — `PeerSession` callbacks (additive dispatch)

Add to `src/Dxs.Bsv/P2p/Session/PeerSession.cs`:

- `Action<IReadOnlyList<BlockHeader>>? OnHeadersReceived`
- `Action<InvMessage>? OnInvReceived` — **single** typed callback;
  consumers filter by `InvVector.Type`. Block-inv and tx-inv go
  through the same path. Replaces the earlier inconsistent
  `OnBlockInvReceived` / `OnInvReceived(tx)` split.
- `Action<RejectMessage>? OnRejectReceived`

**Invariant (audit A1 H4).** Callback dispatch is **additive**, not
substitutive. The existing public `ChannelReader<InboundFrame>
IncomingMessages` channel continues to surface `headers`, `inv`, and
`reject` frames. Existing consumers (notably `TxRelayCoordinator`'s
`getdata` / relay-back / `reject` loop in
`src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs` lines
~93-169) must keep receiving those frames after S0 lands. New
consumers register a callback **or** keep reading the channel, never
both for the same logical purpose. Migration of `TxRelayCoordinator`
away from `IncomingMessages` is out of scope for W1.

### S0.2 — `PeerSession` send helpers

Add to `src/Dxs.Bsv/P2p/Session/PeerSession.cs`:

- `Task SendGetHeadersAsync(GetHeadersMessage msg, CancellationToken ct)`
  — wraps `SendAsync(P2pCommands.GetHeaders, msg.Serialize(), ct)`.
  Required by S3 (audit A1 H2). The wave's "consumed surface" claim
  for `SendGetHeadersAsync` is satisfied **only** by adding this
  helper in S0; do not assume it already exists.

### S0.3 — `PeerTelemetry` snapshot

Add `src/Dxs.Bsv/P2p/Chain/PeerTelemetry.cs` (new) — record with
the full field set required by W4 source metrics and W6 peer
scoring (audit A1 H1):

- `long BytesIn`
- `long BytesOut`
- `DateTimeOffset? LastRecvUtc`
- `DateTimeOffset? LastSendUtc`
- `double PingRttP50Ms`
- `double PingRttP95Ms`
- `int PingSampleCount`
- `long GetDataRequestedCount` — peer asked us for tx/blocks
- `long GetDataServedCount` — we replied with payload
- `double GetDataServeP50Ms` — latency between `getdata` receipt and
  `tx` send for that hash
- `double GetDataServeP95Ms`
- `long RelayBackInvCount` — peers re-announcing our broadcast back
- `IReadOnlyDictionary<RejectClass, long> RejectByClass` — preserved
  per class, never collapsed to a single counter
- `long ProtocolViolationCount` — frames that failed decode or
  violated protocol invariants for this session
- `DisconnectReason? LastDisconnectReason`

### S0.4 — `IPeerTelemetrySink` interface

Add `src/Dxs.Bsv/P2p/Chain/IPeerTelemetrySink.cs` (new). The sink
takes rich events, not pre-aggregated scalars, so W6's production
sink can compute its own aggregates without re-touching
`PeerSession`:

- `void RecordBytesIn(int n)` / `void RecordBytesOut(int n)`
- `void RecordPingRtt(TimeSpan rtt)`
- `void RecordGetDataRequested(InvType type, ReadOnlySpan<byte> hash)`
- `void RecordGetDataServed(InvType type, ReadOnlySpan<byte> hash, TimeSpan serveLatency)`
- `void RecordRelayBackInv(ReadOnlySpan<byte> txid)`
- `void RecordRejectReceived(RejectClass cls)` — class preserved
- `void RecordProtocolViolation(string reason)`
- `void RecordDisconnect(DisconnectReason reason)`
- `PeerTelemetry Snapshot()`

### S0.5 — `NullPeerTelemetrySink` default

Add `src/Dxs.Bsv/P2p/Chain/NullPeerTelemetrySink.cs` (new) — every
method is a no-op, `Snapshot()` returns an empty `PeerTelemetry`.
Used until W6 ships the production sink.

### S0.6 — `PeerSession` telemetry wiring

- Add `IPeerTelemetrySink Telemetry { get; }` property to
  `PeerSession`. Ctor takes the sink (default
  `NullPeerTelemetrySink.Instance`).
- Send loop calls `RecordBytesOut` on each frame.
- Receive loop calls `RecordBytesIn` on each frame; failed decodes
  call `RecordProtocolViolation(reason)`.
- Ping reply handler calls `RecordPingRtt`.
- `End` / disconnect path calls `RecordDisconnect`.
- Inbound `getdata` (handled today by `TxRelayCoordinator`) is not
  re-instrumented in S0 — the relay coordinator will gain a small
  hook in S0.7 below so it can call the sink for served/requested
  events without W6 re-touching it.

### S0.7 — Relay-side telemetry hook

`TxRelayCoordinator.cs` already records the served-at timestamp
internally (lines ~109-152). S0 adds a single dependency hook so it
can emit telemetry against the same sink:

- Constructor of `TxRelayCoordinator` gains an optional
  `IPeerTelemetryRegistry registry` argument. The registry maps
  `IPEndPoint → IPeerTelemetrySink` (one sink per active session)
  and is populated by `PeerManager` when a session reaches `Ready`.
- On `getdata` received for a known broadcast hash, the coordinator
  resolves the sink and calls `RecordGetDataRequested` then, after
  reply, `RecordGetDataServed`.
- On a relay-back `inv` for one of our broadcast txids, the
  coordinator calls `RecordRelayBackInv`.

The registry interface:

```csharp
public interface IPeerTelemetryRegistry
{
    IPeerTelemetrySink For(IPEndPoint endpoint);
}
```

Default implementation returns `NullPeerTelemetrySink.Instance`; W6
swaps in a real one.

### S0.8 — Hub events and DTOs

- `src/Dxs.Consigliere/WebSockets/IWalletHub.cs` — add:
  - `Task OnNewBlock(BlockTipDto tip)` (W1 emits)
  - `Task OnReorg(ReorgEventDto reorg)` (signature frozen here;
    body remains a no-op until W3)
- `src/Dxs.Consigliere/WebSockets/BlockTipDto.cs` (new) — record:
  `{ string Hash, long Height, long TimestampMs, string PrevHash, int HeaderSize }`.
- `src/Dxs.Consigliere/WebSockets/ReorgEventDto.cs` (new) — record:
  `{ string CommonAncestorHash, long CommonAncestorHeight, string[] OrphanedHashes, string NewTipHash, long NewTipHeight, bool DegradedState }`.
- `src/Dxs.Consigliere/WebSockets/WalletHub.cs` — `OnNewBlock` body
  is `Clients.Group("block:tip").OnNewBlock(tip)`; `OnReorg` body
  is `Task.CompletedTask` until W3 replaces it.
- `src/Dxs.Consigliere/WebSockets/IWalletServer.cs` — add
  `SubscribeToBlockTip()` and `SubscribeToReorg()` server methods.
- `src/Dxs.Consigliere/WebSockets/WalletHub.cs` — implement those
  two server methods (add caller `Context.ConnectionId` to the
  named group).

### S0.9 — `BroadcastReceiptDto` shape freeze

- `src/Dxs.Consigliere/WebSockets/BroadcastReceiptDto.cs` — add a
  XML-doc-style comment block marking the DTO as **frozen for W5**:
  property names, types, and arity must not change. No field edits
  here; the freeze is the comment plus the manifest below.
- **S0 does not change the existing `IWalletServer.Broadcast` /
  `BroadcastTracked` server methods.** Per audit A1 M3: W5 is
  explicitly authorised to collapse those two server methods into a
  single `Broadcast(hex) → BroadcastReceiptDto`. The DTO is frozen;
  the server method signatures are **not** frozen and remain in W5's
  scope. The handoff table in `master.md` records this explicitly.

### S0.10 — Public-API manifest (replaces reflection-only test)

Per audit A1 M1 the prior reflection-existence test was too weak.
Add a checked-in JSON manifest plus an approval-style test:

- `tests/Dxs.Consigliere.Tests/P2p/ContractFreeze/manifest.json`
  (new) — exact snapshot of:
  - every member on `IWalletHub` (name, return type, parameter
    types in order)
  - every server method on `IWalletServer` that S0 introduces
    (`SubscribeToBlockTip`, `SubscribeToReorg`)
  - every frozen-surface member on `PeerSession` (`OnHeadersReceived`,
    `OnInvReceived`, `OnRejectReceived`, `Telemetry`,
    `SendGetHeadersAsync`)
  - every member of `PeerTelemetry` and `IPeerTelemetrySink`
  - every property/constructor parameter of `BlockTipDto`,
    `ReorgEventDto`, `BroadcastReceiptDto`
- `tests/Dxs.Consigliere.Tests/P2p/ContractFreeze/ContractFreezeApprovalTests.cs`
  (new) — for each frozen type, reflects the current declared
  surface (sorted, normalised), serialises to JSON, and asserts
  byte-equal to `manifest.json`. Mismatch fails the test with a
  unified diff so reviewers see exactly what drifted.

This catches parameter type drift, return type drift, DTO
constructor/property drift, and unreviewed additions — none of
which the prior reflection-existence test would have caught.

### S0.11 — Additive-dispatch regression test

Per audit A1 H4, add a regression test that asserts the channel
still receives the frame even when a callback is registered:

- `tests/Dxs.Bsv.Tests/P2p/Session/PeerSessionAdditiveDispatchTests.cs`
  (new) — uses an in-memory pipe pair as transport (existing test
  helper from Gate 2 if present, else a small `MiniBsvServer`).
  Subscribes a callback, sends an `inv`, asserts (a) the callback
  fires once with the parsed `InvMessage` and (b) a frame with
  command `"inv"` is also written to `IncomingMessages`. Same
  assertion for `reject` and `headers`.

### Owned paths (S0)

- `src/Dxs.Bsv/P2p/Session/PeerSession.cs` (extend)
- `src/Dxs.Bsv/P2p/Chain/PeerTelemetry.cs` (new)
- `src/Dxs.Bsv/P2p/Chain/IPeerTelemetrySink.cs` (new)
- `src/Dxs.Bsv/P2p/Chain/NullPeerTelemetrySink.cs` (new)
- `src/Dxs.Bsv/P2p/Chain/IPeerTelemetryRegistry.cs` (new)
- `src/Dxs.Bsv/P2p/Chain/NullPeerTelemetryRegistry.cs` (new)
- `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs` (minor:
  optional registry ctor arg + four sink calls)
- `src/Dxs.Consigliere/WebSockets/IWalletHub.cs` (extend)
- `src/Dxs.Consigliere/WebSockets/IWalletServer.cs` (extend)
- `src/Dxs.Consigliere/WebSockets/WalletHub.cs` (extend)
- `src/Dxs.Consigliere/WebSockets/BlockTipDto.cs` (new)
- `src/Dxs.Consigliere/WebSockets/ReorgEventDto.cs` (new)
- `src/Dxs.Consigliere/WebSockets/BroadcastReceiptDto.cs` (comment
  only)
- `tests/Dxs.Consigliere.Tests/P2p/ContractFreeze/manifest.json` (new)
- `tests/Dxs.Consigliere.Tests/P2p/ContractFreeze/ContractFreezeApprovalTests.cs` (new)
- `tests/Dxs.Bsv.Tests/P2p/Session/PeerSessionAdditiveDispatchTests.cs` (new)

**Out of scope (S0).** Any header chain logic. Any document or
store. Any business behaviour. Migration of `TxRelayCoordinator`
away from `IncomingMessages`. S0 only ships type signatures +
dispatchers + default sinks + telemetry-hook plumbing.

### Validation (S0)

- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- `dotnet test` baseline green (no new failures vs the pre-S0
  baseline; record baseline counts in S0's evidence).
- `ContractFreezeApprovalTests` green — manifest matches reflected
  surface byte-for-byte.
- `PeerSessionAdditiveDispatchTests` green — callbacks fire **and**
  `IncomingMessages` still receives `inv`/`reject`/`headers`.
- Static check: grep across `src/` and `docs/` for
  `OnBlockInvReceived` or `OnInvReceived(tx)` patterns — must
  return zero hits.
- Static check: grep for `SendGetHeadersAsync` returns at least one
  match in `PeerSession.cs` (helper present).

**Done when.** All declared surfaces compile; build green; both new
tests green; reflection-grep checks green; slice-level audit
`audits/S0-A1.md` returns APPROVE.

## S1 — Headers Chain Pure Logic

`depends_on = S0`.

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

`depends_on = S0`.

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

**Out of scope.** Caching / read-through layer.

**Validation.**

- Integration test against an embedded RavenDB instance
  (`tests/Dxs.Consigliere.Tests/P2p/BlockHeaderStoreTests.cs`):
  - save + get-by-hash round-trip;
  - `GetTipAsync` returns highest-height doc;
  - `RecentAsync(50)` returns up to 50 docs, ordered tip-first;
  - `PruneBelowAsync` deletes only docs strictly below the cutoff.

**Done when.** All tests green.

## S3 — `HeadersChainService` Hosted Service

`depends_on = S0, S1, S2`.

**Intent.** Wire the pure `HeadersChain` to the live P2P pool. On
startup: load persisted headers into `HeadersChain.LoadFromStore`. On
`OnInvReceived(InvMessage)` from any peer with `MSG_BLOCK` items:
send `getheaders` on a ready peer (uses
`PeerSession.SendGetHeadersAsync` added in S0.2). On
`OnHeadersReceived` from any peer: feed headers to
`HeadersChain.TryExtend` one at a time; for each `Extended`, persist
via `BlockHeaderStore.SaveAsync` and raise the new-block event via
an internal `INewBlockNotifier` (consumed by S5). Periodic timer
(`HeadersChainOptions.GetHeadersIntervalMs`) sends a baseline
`getheaders` to detect missed tips.

**Owned paths.**

- `src/Dxs.Consigliere/Services/P2p/HeadersChainService.cs` (new) —
  `PeriodicTask` or `BackgroundService` depending on repo
  convention. Subscribes to `PeerSession.OnHeadersReceived` /
  `OnInvReceived` for every peer that reaches `Ready` (use
  `PeerManager` event or scan ready peers each tick — pick the
  cheaper one in code review).
- `src/Dxs.Consigliere/Services/P2p/INewBlockNotifier.cs` (new) —
  one-liner interface: `Task NotifyAsync(BlockTipDto tip)`.
- `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` (extend) — register
  `HeadersChain`, `HeadersChainOptions`, `BlockHeaderStore`,
  `HeadersChainService`, `INewBlockNotifier` (impl from S5).

**Out of scope.** Reorg recovery (W3). Block-body fetch (W3 / W2).
Bitails bootstrap (S4).

**Validation.**

- Integration test against a fake peer
  (`tests/Dxs.Consigliere.Tests/P2p/HeadersChainServiceTests.cs`):
  - fake peer sends `inv(MSG_BLOCK)` for a known header → service
    sends `getheaders` on that peer (assert via captured frame);
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

`depends_on = S0, S1, S2, S3`. (Audit A1 H3: invoked by
`HeadersChainService` startup, so S3 is a real edge.)

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
Bitails exposes them cheaply.

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

`depends_on = S0, S3`.

**Intent.** Implement the `INewBlockNotifier` registered in S3 as a
SignalR-pump that calls `IWalletHub.OnNewBlock(tip)` against the
`block:tip` group. Confirm `OnReorg` is registered and reachable but
delivers nothing in W1.

**Owned paths.**

- `src/Dxs.Consigliere/Services/P2p/HubNewBlockNotifier.cs` (new) —
  `INewBlockNotifier` impl that takes `IHubContext<WalletHub,
  IWalletHub>` and broadcasts to the `block:tip` group.

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

`depends_on = S0, S3`.

**Intent.** Operators can read the current tip and recent headers
through the admin API. Admin SPA page for headers is **out of
scope** for W1 — UI work lands in W4 or later.

**Owned paths.**

- `src/Dxs.Consigliere/Controllers/AdminP2pController.cs` (extend):
  - `GET /api/admin/p2p/headers/tip` →
    `{ hash, height, timestampMs, prevHash }`
  - `GET /api/admin/p2p/headers/recent?count=N` →
    array (N capped at 200).

**Out of scope.** Admin UI page for headers. Reorg state read API
(W3).

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

`depends_on = S0, S5, S6`. (Audit A1 H3: soak validates both the
hub event from S5 and the admin endpoint from S6.)

**Intent.** Validate the p95-lag claim on real mainnet. Throwaway
recorder under `tests/Spikes/P2p/HeadersSoakRecorder/` — does not
ship in production binaries.

**Owned paths.**

- `tests/Spikes/P2p/HeadersSoakRecorder/Program.cs` (new)
- `tests/Spikes/P2p/HeadersSoakRecorder/analyze.fsx` (or `.py`)
- `tests/Spikes/P2p/HeadersSoakRecorder/README.md` (new) — schema
  + reproducibility rules below
- `docs/stream-tasks/bsv-headers-chain-wave/evidence/headers-soak.md`
  (new, written at end of soak)

### Recorder behaviour

The recorder:

- Boots a minimal Consigliere headers-chain stack against mainnet
  peers (reuses `PeerManager` + `HeadersChainService`).
- Subscribes to `INewBlockNotifier` via a fake hub that writes a
  `p2p` record to a rolling JSONL file.
- Polls `https://api.whatsonchain.com/v1/bsv/main/chain/info` at
  1 s cadence; on each height change appends a `woc` record.

### JSONL schema (audit A1 M2)

Each line is one JSON object with these required fields:

```
{
  "type": "p2p" | "woc" | "http_error" | "decode_error",
  "ts_utc_ms": <int64>,           // UTC milliseconds since epoch
  "height": <int64 | null>,       // present for p2p, woc
  "tip_hash": <string | null>,    // hex, lowercase, no 0x prefix
  "prev_hash": <string | null>,
  "header_timestamp_ms": <int64 | null>, // p2p only
  "source_seq": <int64>,          // monotonic per-type sequence
  "extra": <object | null>        // free-form, for context only
}
```

For `http_error` records, `extra` carries `{ "status": ..., "url":
..., "reason": "..." }`. For `decode_error`, `extra` carries the
peer endpoint and decode reason.

### Reproducibility rules (audit A1 M2)

- **Clock.** Host must have NTP enabled; `chronyd` or `systemd-timesyncd`
  showing offset < 50 ms before soak start. Record `ts_utc_ms` from
  `DateTimeOffset.UtcNow` only.
- **Soak window.** ≥ 24 h continuous; min block count for a valid
  run is **128 blocks** (typical mainnet ≈ 144 blocks/day; below
  128 the run is reported as inconclusive rather than failed).
- **Join rule.** For each `woc` record with height H, find the
  `p2p` record with the same `tip_hash`. If no matching `p2p`
  record arrives within 600 s after the `woc` record, count the
  block as **missed**. If `tip_hash` differs at same H (orphaned
  during the soak), prefer the `tip_hash` that ended up canonical
  by end of soak; both losers are counted as `orphaned_during_soak`
  and excluded from lag stats.
- **Lag formula.** `lag_ms = p2p.ts_utc_ms - woc.ts_utc_ms` for
  the matched pair. Negative lag (P2P observed first) **counts as
  zero** for the p95 calculation; the raw value is preserved in
  the JSONL for review.
- **Missing samples.** If WhatsOnChain polling drops below 90 %
  uptime (measured by `http_error` density), the run is **invalid**
  and must be re-done.
- **HTTP errors.** Recorded but never excluded from the denominator
  silently. The end-of-soak report includes total error count and
  affected window.
- **Quantile algorithm.** Empirical p95 over `joined_blocks` using
  the linear-interpolation method `c = numpy.percentile(latencies,
  95, method='linear')`. Equivalent F# / C# implementations must
  match within 1 ms.
- **Pass condition.** `p95 ≤ 2000 ms` over at least 128 joined
  blocks within the soak window; `missed < 5 %` of total `woc`
  block-height changes.

### Validation (S7)

- Recorder runs ≥ 24 h on a fresh DigitalOcean droplet (same class
  as Gate 2 soak runbook).
- `evidence/headers-soak.md` records: host metadata, NTP offset
  before/after, total `woc` blocks, total `p2p` blocks, joined,
  missed, orphaned_during_soak, p50, p95, p99, HTTP error count,
  tip hash at end matched against WhatsOnChain at the same moment.
- Admin endpoint `GET /api/admin/p2p/headers/tip` queried at end
  of soak matches the recorder's tip and matches WhatsOnChain.

**Done when.** `evidence/headers-soak.md` exists with measured
numbers proving p95 ≤ 2 s, and the recorder code is committed but
flagged in its README as a spike.

## Dependency Graph

```
                ┌────────────────────────────────────────────┐
                │ S0 — Program-Wide Contract Freeze          │
                │ (prerequisite; all others depend on this)  │
                └─────┬──────────────────────────────────────┘
                      │
        ┌─────────────┼─────────────┐
        ▼             ▼             │
   ┌─────────┐  ┌─────────────┐     │
   │ S1      │  │ S2          │     │
   │ Headers │  │ Header doc  │     │
   │ chain   │  │ + store     │     │
   │ logic   │  │             │     │
   └────┬────┘  └──────┬──────┘     │
        │              │            │
        └──────┬───────┘            │
               ▼                    │
         ┌──────────────┐           │
         │ S3 (S0,S1,S2)│           │
         │ Chain hosted │           │
         │ service      │           │
         └─────┬────────┘           │
               │                    │
       ┌───────┼──────────┐         │
       ▼       ▼          ▼         │
   ┌──────┐ ┌──────┐  ┌────────┐    │
   │ S5   │ │ S6   │  │ S4     │ ◄──┘
   │ Hub  │ │ Admin│  │ Bitails│
   │ event│ │ API  │  │ boot   │
   └──┬───┘ └──┬───┘  └────────┘
      │        │
      └────┬───┘
           ▼
     ┌──────────────┐
     │ S7 (S0,S5,S6)│
     │ Soak spike   │
     └──────────────┘
```

Direct dependency edges encoded in the ledger (audit A1 H3 fix):

- S0: —
- S1: S0
- S2: S0
- S3: S0, S1, S2
- S4: S0, S1, S2, S3
- S5: S0, S3
- S6: S0, S3
- S7: S0, S5, S6

Default sequential order (strict stop-and-audit, operator preferred):
S0 → S1 → S2 → S3 → S4 → S5 → S6 → S7. Parallelism is only an
option if an audit pass is run per opened slice.

## Per-slice Audit Rules

- S0 receives its own slice-level audit at `audits/S0-A1.md` per
  the program's prerequisite-slice gate. **No main slice opens
  until S0's slice audit returns APPROVE.**
- S1-S7 are covered by the single wave-level audit at
  `audits/wave1-audit-A2.md` after all slices are `done` (A1 is
  this audit; A2 will be the post-execution audit).
- If any slice surfaces a residual that requires a fix-and-re-audit
  pass, open `audits/A<n+1>.md`.

## Validation Matrix (wave-level)

| signal | slice | how validated |
|---|---|---|
| Frozen contract surface present | S0 | approval test against `manifest.json` (byte-equal); grep proves no `OnBlockInvReceived` / `OnInvReceived(tx)` leftovers; grep confirms `SendGetHeadersAsync` present |
| Callback dispatch is additive | S0 | `PeerSessionAdditiveDispatchTests` asserts callback fires AND `IncomingMessages` still receives the frame |
| Telemetry sink covers W4 + W6 needs | S0 | manifest snapshot covers all listed `PeerTelemetry` fields and `IPeerTelemetrySink` methods; `RejectByClass` preserved as map |
| Header validation correctness | S1 | `HeadersChainTests` covers every `ExtendResult` branch |
| Headers persistence | S2 | embedded-Raven integration test |
| Tip advances on live `headers` | S3 | hosted-service test with fake peer; `SendGetHeadersAsync` invoked |
| Bootstrap path | S4 | canned Bitails JSON → seeded store; `SeedFromBitails=false` → no HTTP |
| `OnNewBlock` reaches SignalR clients | S5 | end-to-end SignalR test |
| Admin API responses | S6 | controller test against seeded store |
| Soak p95 ≤ 2 s | S7 | `evidence/headers-soak.md` records computed p95 over 24 h per JSONL schema; admin endpoint tip matches WhatsOnChain at end |
| Build green | every | `dotnet build Dxs.Consigliere.sln -c Release` 0 errors |
| Tests green | every | `dotnet test` no new failures vs baseline |
