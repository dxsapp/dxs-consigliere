# Consigliere Thin-Node Broadcaster — Design

Status: **Draft (post-audit revision)**
Owner: Consigliere runtime
Last updated: 2026-05-14
Supersedes: [p2p-broadcaster-design.md](./p2p-broadcaster-design.md) (kept for historical context — §15–§16 contain the spike journey that led here)
Audit: [thin-node-design-audit.md](./thin-node-design-audit.md) (GPT-5 Codex, MAJOR REVISION REQUIRED) — this revision addresses C1–C3, H1–H6, M1–M4, L1–L2

## 1. Motivation

Consigliere needs a reliable, low-cost, opensource-friendly broadcast channel
for user transactions on BSV mainnet. The previous design assumed P2P
broadcast as anonymous client; spike work proved this is structurally
blocked (peer banlists / whitelist policies). The fallback assumption
"just run a pruned bitcoin-sv node" carries unacceptable operational cost:

- High memory footprint (multi-GB)
- Slow crash recovery (full reindex on certain failure modes — hours to days)
- Operational complexity that a downstream opensource user should not have
  to bear

**Goal:** ship a Consigliere process that behaves as a fully participating
BSV P2P peer — performs handshake, joins addr-gossip, relays our own
outgoing transactions, but does **not** store the blockchain, does **not**
validate blocks, and does **not** serve historical data. The process should
restart in seconds and require no persistent state larger than a small
RavenDB collection.

We will call this the **thin-node broadcaster** or **thin-node** for short.

## 2. Non-goals

- Block storage, validation, or serving (we are not a node operator).
- Mining or block-template assembly.
- SPV (BIP37) bloom-filter relay to external SPV wallets.
- Replacement for the existing read-side ingestion path (Bitails / JungleBus
  websockets remain the primary observation channel).
- Signing transactions; the thin-node only relays pre-signed bytes.
- Testnet support in Phase 1 (same protocol; switch is network-magic + DNS-seeds; deferred).
- **No ARC, mAPI, or HTTP-provider broadcast fallback.** OpenSource mode is
  P2P-only; if peers fail, tx fails and surfaces as alert.
- **Phase 1 supports tx up to 2 MB** (legacy `LEGACY_MAX_PROTOCOL_PAYLOAD_LENGTH`).
  Multi-MB and `extmsg`-framed transactions are deferred to Phase 3.

## 3. Spike findings — what we know for sure

The spike series A–K (documented in [p2p-broadcaster-design.md §15–§16](./p2p-broadcaster-design.md))
established:

1. **Protocol works.** Python implementation handshakes successfully with a
   real `/Bitcoin SV:1.2.1/` mainnet peer in 27ms from a freshly-deployed
   VPS, exchanging `version → version → verack → verack`. Verified bytes
   match what `bitcoin-sv` master sends and Teranode emits. **Spike J/K
   acceptance rate was 1/14 from two different IPs.** Steady-state rate is
   unknown until a soak test is performed (see §5 acceptance gate).

2. **Exact `version` payload format for Phase 1 (104 bytes, no association ID):**

   | Offset | Size | Field | Value / encoding |
   |---:|---:|---|---|
   | 0  | 4  | `protocol_version` | `70016` (int32 LE) |
   | 4  | 8  | `services`         | `0x0000000000000025` (NODE_NETWORK + NODE_BLOOM + NODE_BITCOIN_CASH) |
   | 12 | 8  | `timestamp`        | current unix seconds (int64 LE) |
   | 20 | 26 | `addr_recv`        | `services=0x01` + IPv4-mapped IPv6 + port BE |
   | 46 | 26 | `addr_from`        | `services=0x25` + all-zero IPv6 + port 0 |
   | 72 | 8  | `nonce`            | random uint64 (must be unique per connect) |
   | 80 | 19 | `user_agent`       | varint(18) + `/ConsigliereThinNode:0.1.0/` (see §3.1) |
   | 99 | 4  | `start_height`     | `0` (int32 LE) acceptable |
   |103 | 1  | `relay`            | `0x01` (true) |

   **Association ID tail is OMITTED** by default in Phase 1 (matches `bsv-p2p`,
   matches Teranode's default `AllowBlockPriority=false` configuration,
   accepted by SV-Node since `LIMITED_BYTE_VEC` parsing only fires when
   bytes remain after `relay`, `net_processing.cpp:1757-1794`).
   - This is a **current-compatibility decision**, not a permanent claim.
   - Codec must **tolerantly parse** an incoming association ID if present.
   - Config exposes `AssociationIdMode = None|Echo|Generate` (default `None`)
     so future protocol shifts can be addressed without re-architecture.

3. **`protoconf` is sent by us immediately after our `verack`** (per
   SV-Node `net_processing.cpp:1821-1824` and Teranode `peer.go:2482-2489`).
   Payload: `compact_size(2) + max_recv_payload_length=2_097_152 (LE u32) +
   var_str("Default")`.

4. **Wire framing:** every message is a 24-byte header (`magic = 0xE3E1F3E8`
   LE → `e8 f3 e1 e3` on wire; 12-byte ASCII command NUL-padded;
   4-byte payload length LE; 4-byte checksum = first 4 bytes of
   `SHA256(SHA256(payload))`) followed by the payload. `extmsg` extended
   framing is out of scope for Phase 1.

5. **Handshake sequence (outbound):**
   - TCP connect with `TCP_NODELAY=1`.
   - Send our `version` immediately.
   - Read messages until we receive peer's `version` AND `verack`. Reply
     with our `verack` only after receiving their `version`, then send our
     `protoconf` immediately after our `verack`. Acknowledge `ping` with
     `pong` always; otherwise ignore.
   - Handshake must complete within 60s.

6. **Steady state:** the peer pings us roughly every 120s. We must reply
   `pong` with the same 8-byte nonce. No other periodic message is required.

7. **Misbehaving banscore is reactive.** Empty / silent responses to peer
   queries cost zero misbehavior. Specifically:
   - `getheaders` → reply with empty `headers` (varint 0). **Must parse
     the locator first** to avoid silent corruption (audit M1).
   - `getdata(MSG_BLOCK)` → reply `notfound`.
   - `getaddr` → reply empty `addr` (or silent — both acceptable).
   - `inv` → ignore.
   - Block-related messages → drop.

8. **Discovery works.** DNS seeds (`seed.bitcoinsv.io`,
   `seed.satoshisvision.network`, `seed.bitcoinseed.directory`) return live
   listening peers. Mid-term, addr-gossip populates a persistent peer cache.

### 3.1 User-agent

Default: `/ConsigliereThinNode:0.1.0/`. **Do not impersonate SV-Node by
default.** If acceptance rate is unacceptable with the honest UA, that is
a network-policy risk (escalate to operators), not a config workaround.
Spikes used `/Bitcoin SV:1.2.1/` as debugging shortcut; production must not.

### 3.2 Acceptance criteria / evidence gate (must pass before status leaves "Draft")

Before this design is approved for production rollout (gate 4 below), a
soak test on a fresh VPS IP must demonstrate:

| Metric | Threshold |
|---|---|
| Sustained outbound peers over 24h | ≥ 8 |
| Peer-acceptance rate on cold start | ≥ 30% of attempted handshakes (jitter-limited) |
| Successful test-tx submissions | ≥ 10 (real low-value OP_RETURN or testnet) |
| Time from `Submitted` to independent mempool sighting | p50 ≤ 30s, p99 ≤ 120s |
| Time from `Submitted` to confirmed | depends on mempool; record empirically |
| Peer pool diversity | ≥ 3 distinct ASNs / operator groups |

If these thresholds fail after 7 days of iteration, design pivots to
embedded-bitcoind sidecar (still in our control, not 3rd-party HTTP).

## 4. Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│  Public API:  WalletHub.BroadcastTracked(hex) → BroadcastReceipt         │
│  (legacy Broadcast deprecated, see §9)                                    │
└─────────────────────────────────────────┬────────────────────────────────┘
                                          ▼
                          ┌────────────────────────────────┐
                          │ IBroadcastService.SubmitAsync  │
                          └────────────────┬───────────────┘
                                           ▼
                          ┌────────────────────────────────┐
                          │ TxPolicyValidator              │  parse, size cap, fee floor
                          │  - reject early                 │  (terminal `PolicyInvalid`)
                          └────────────────┬───────────────┘
                                           ▼
                          ┌────────────────────────────────┐
                          │ OutgoingTransactionStore       │  RavenDB: outgoing-tx/{txid}
                          └────────────────┬───────────────┘
                                           ▼
                          ┌────────────────────────────────┐
                          │ TxRelayCoordinator             │
                          │  - announce(txid, peers)       │
                          │  - serve getdata → tx          │
                          │  - track relay-back inv        │
                          └────────────────┬───────────────┘
                                           ▼
            ┌──────────────────────────────────────────────────────┐
            │                  PeerManager                          │
            │  - pool of N outbound PeerSessions (default 8)        │
            │  - optional InboundListener on :8333 (Phase 2)        │
            │  - rate-limited bootstrap (small concurrency + jitter)│
            │  - per-/24 diversity; persisted negative cache        │
            │  - peer scoring (Phase 2)                             │
            └────┬─────────────────────────────────────────────┬───┘
                 │                                             │
                 ▼                                             ▼
       ┌─────────────────┐                          ┌──────────────────┐
       │ PeerDiscovery   │                          │ PeerRecordStore  │
       │  - DNS seeds    │                          │  RavenDB:        │
       │  - addr-gossip  │                          │  peer/{host:port}│
       └─────────────────┘                          └──────────────────┘
                 │
                 ▼
       ┌─────────────────┐
       │ PeerSession     │  (one per peer, long-lived TCP)
       │  ┌───────────┐  │
       │  │ Codec     │  │  → encode/decode P2P messages (basic header)
       │  │ Handshake │  │  → version/verack/protoconf flow
       │  │ Keepalive │  │  → ping/pong (120s typical interval)
       │  │ FeeFilter │  │  → respect peer.minFeePerKb when announcing
       │  │ Router    │  │  → dispatch to handlers; classify rejects
       │  └───────────┘  │
       └─────────────────┘
                 │
                 ▼
       BSV network (mainnet)


       ┌────────────────────────────────┐
       │ OutgoingTransactionMonitor     │   (PeriodicTask, 15s)
       │  - scan non-terminal documents │
       │  - advance state on signals    │
       │  - retry with backoff          │
       │  - reconcile with observer     │
       └────────────────────────────────┘

       ┌────────────────────────────────┐
       │ MempoolBlockObserver           │   (reuse existing ingestion)
       │  - subscribe to Bitails/JBus   │
       │  - emit MempoolSighting events │
       │  - emit Mined events           │
       │  - emit Reorg/Eviction events  │
       └────────────────────────────────┘
```

### 4.1 Component summary

| Component | Path (planned) | Role |
|---|---|---|
| `MessageCodec` | `src/Dxs.Bsv/P2p/MessageCodec.cs` | Encode/decode P2P messages. Built on `BitcoinStreamReader`/`BufferWriter`. Tolerant parse of optional fields (association ID, protoconf). |
| `PeerSession` | `src/Dxs.Bsv/P2p/PeerSession.cs` | One long-lived TCP. Handshake, ping/pong, dispatch, feefilter tracking. |
| `PeerManager` | `src/Dxs.Consigliere/Services/P2p/PeerManager.cs` | Pool of N outbound sessions, rate-limited bootstrap, scoring (Phase 2), inbound listener (Phase 2). |
| `PeerDiscovery` | `src/Dxs.Consigliere/Services/P2p/PeerDiscovery.cs` | DNS-seed resolution, addr-gossip absorption, persisted negative cache. |
| `TxRelayCoordinator` | `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs` | Receive broadcast requests; announce via `inv`; serve `getdata`; track relay-back. Respect per-peer feefilter. |
| `TxPolicyValidator` | `src/Dxs.Consigliere/Services/P2p/TxPolicyValidator.cs` | Parse tx, compute txid, check size ≤ 2 MB, fee floor, idempotency dedup. Reject early. |
| `OutgoingTransactionStore` | `src/Dxs.Consigliere/Data/Stores/OutgoingTransactionStore.cs` | RavenDB document store + queries. |
| `OutgoingTransactionMonitor` | `src/Dxs.Consigliere/BackgroundTasks/OutgoingTransactionMonitor.cs` | `PeriodicTask` driving state transitions, retry, observer reconciliation. |
| `MempoolBlockObserver` | (extends existing observer) | Mempool sightings, Mined, Eviction, Reorg events. |
| `BroadcastService` | (existing, extended) | Unified facade; new `SubmitAsync` flow. **Existing HTTP-provider path is preserved only for operator-explicit non-opensource deployments** (see §9). |

### 4.2 Zone catalog placement

Per audit L2: codec primitives belong in `bsv-protocol-core` zone, runtime
peer behavior in `bsv-runtime-ingest` or a new `bsv-p2p-runtime` zone.
Update `doc/repository-zones/` before implementation starts.

## 5. Protocol implementation

### 5.1 Message types we MUST handle

| Direction | Command | Action |
|---|---|---|
| recv `version` | parse → store peer info → reply `verack` (only after receiving theirs), then `protoconf` |
| recv `verack` | mark fully connected; transition session to `Ready` |
| recv `protoconf` | parse and **store `maxRecvPayloadLength`** for outbound message sizing |
| recv `authch` / `authresp` | ignore (no miner-ID auth) |
| recv `ping` | reply `pong` with same nonce **always**; mandatory for keepalive |
| recv `pong` | ignore |
| recv `feefilter` | **parse and store peer's min-fee-per-1000-bytes**; respect when announcing |
| recv `getheaders` | **parse locator first**; reply empty `headers` only on success; malformed → log + close (per audit M1) |
| recv `getblocks` | empty `inv` or silence |
| recv `getdata` | for items we have (our outgoing tx) → send `tx`; else accumulate into `notfound` |
| recv `inv` | record txid for relay-back ack tracking; do not request via `getdata` |
| recv `tx` | optional: forward txid to `MempoolBlockObserver` for ack tracking |
| recv `block` / `cmpctblock` / `blocktxn` / `merkleblock` / `headers` | drop (we never asked) |
| recv `addr` / `addrv2` | feed IPs to `PeerDiscovery` for pool replenishment |
| recv `getaddr` | reply empty `addr` |
| recv `sendheaders` / `sendcmpct` / `sendhdrsen` / `mempool` | accept silently |
| recv `reject` | **classify** by code + reason; route to `OutgoingTransaction.PeerAttempts[].RejectReason` |
| recv `notfound` | ignore |
| recv anything else | log and drop, **do not** disconnect |
| send `version` | first message after TCP connect |
| send `verack` | only after receiving peer's `version` |
| send `protoconf` | immediately after our `verack` |
| send `pong` | always echoing peer's `ping` nonce |
| send `inv(MSG_TX, txid)` | for each new outgoing transaction, only to peers whose feefilter accepts it |
| send `tx` | when a peer sends us `getdata(MSG_TX, txid)` for one of our tx, only if tx size ≤ peer's `maxRecvPayloadLength` |
| send `notfound` | for unhandled `getdata` items |
| send `headers` (empty) | on every successfully-parsed `getheaders` |
| send `addr` (empty) | on `getaddr` |

### 5.2 Reject code classification (Phase 1)

`reject` payload format (`net_processing.cpp:1475-1497`):
- var_str message (e.g. "tx")
- 1-byte reject code
- var_str reason
- 32-byte hash (for tx/block rejects)

Classification table:

| Reason substring | Class | Action |
|---|---|---|
| `bad-txns-inputs-missingorspent` / `missing-inputs` | **Conflicted** (likely double-spend) | terminal `ConflictRejected` after N=2 peers same class |
| `mandatory-script-verify-flag-failed` / `bad-txns-*` (signature / script) | **Invalid** | terminal `PolicyInvalid` after N=2 peers same class |
| `dust` / `bad-txns-too-small` / `min-fee-not-met` | **PolicyRejected** | terminal `PolicyInvalid` after N=2 peers same class |
| `txn-mempool-conflict` | **Conflicted** | terminal `ConflictRejected` after N=2 |
| `mempool-full` / `txn-already-known` | **Transient** | log, do not terminate; retry later |
| `insufficient-fee` (transient or terminal — context-sensitive) | **PolicyRejected** | record reason, retry once with same fee (peer policy may differ) |
| other | **Unknown** | record reason, continue with other peers; if all peers reject with the same Unknown, terminal `Failed` |

Quorum of N=2 peers same class prevents terminating on single-peer
policy idiosyncrasy.

### 5.3 What we explicitly skip (and why it's safe)

- **Bloom filters**: never send `filterload` / `filteradd` / `filterclear`.
- **Mempool sync request**: we don't send `mempool`.
- **`extmsg` extended framing**: Phase 1 caps tx at 2 MB, single basic header suffices. Phase 3 implements `extmsg`.

## 6. Peer pool management

### 6.1 Cold start (bootstrap, rate-limited)

On first launch with no `PeerRecord` documents in RavenDB:
1. Resolve all configured DNS seeds (`seed.bitcoinsv.io`,
   `seed.satoshisvision.network`, `seed.bitcoinseed.directory`).
2. Build candidate set, **shuffle, deduplicate by /24 subnet**.
3. **Rate-limited fanout**: up to 4 concurrent handshake attempts, with
   jittered backoff (`200ms ± 200ms`) between candidate slots.
4. Persist outcomes — both `PeerRecord(status=good)` and
   `PeerRecord(status=negative, retry_after=now+1h)` for failed ones.
5. Continue until target pool size N (default 8) is reached or candidate
   set is exhausted.
6. **Never blast more than 4 simultaneous handshakes from a single source IP.**

This addresses audit H3 (32-way fanout looked like scanning to operators).

### 6.2 Warm start

If `PeerRecord` documents exist:
1. Connect to up to N previously-good peers from store.
2. Refresh DNS seeds in background, merge new IPs.
3. Replenish from candidate set if any session drops.

### 6.3 Discovery via addr-gossip

`addr` / `addrv2` from connected peers → upsert `PeerRecord` with
`Source=addr-gossip`, `LastSeen=now`. Skip ASN-overlap with current pool.

### 6.4 Peer scoring (Phase 2)

Per-peer metrics in RavenDB: latency p50/p99, drop count, relay-back ratio,
reject count. Score determines pool retention. Bad peers evicted to cooldown.

### 6.5 Inbound listener (Phase 2, optional)

Bind `0.0.0.0:8333`. Disabled by default; enabled via config when operator
has a public IP and open port. Allows addr-gossip to propagate our IP.

## 7. Outgoing transaction lifecycle

### 7.1 States

```
                       ┌─────────────┐
                       │  Submitted  │  ← BroadcastTracked called
                       └──────┬──────┘
                              ▼
                       ┌─────────────┐
                       │  Validated  │  ← TxPolicyValidator OK
                       └──────┬──────┘
                              ▼
                       ┌─────────────┐
                       │ Dispatching │
                       └──────┬──────┘
                              │
              ┌───────────────┴────────────────┐
              │                                │
              ▼                                ▼
       ┌─────────────┐                  ┌─────────────┐
       │  PeerAcked  │                  │   Failed    │   (no peer reachable)
       │  (≥1 peer   │                  └─────────────┘
       │   sent OK)  │                   terminal ✗
       └──────┬──────┘
              │  (relay-back inv from ≥K peers, K=2)
              ▼
       ┌─────────────┐
       │ PeerRelayed │
       └──────┬──────┘
              │  (independent mempool sighting from observer)
              ▼
       ┌─────────────┐
       │ MempoolSeen │
       └──────┬──────┘
              │
   ┌──────────┼──────────┐
   │          │          │
   ▼          ▼          ▼
┌─────┐  ┌──────────┐ ┌──────────────────┐
│Mined│  │ Evicted  │ │ ObserverUnknown  │
└──┬──┘  │OrDropped │ │ (no signal for T)│
   │     └────┬─────┘ └─────────┬────────┘
   │          │                  │
   │  re-broadcast (Phase 2)     re-query observer / re-broadcast
   │
   ▼ (N confirmations)
┌──────────┐
│Confirmed │
└──────────┘
 terminal ✓


Terminal failure states (parallel to mainline):

┌────────────────────┐  reject-quorum: ≥2 peers, "bad-txns-*" or
│   PolicyInvalid    │  "mandatory-script-verify-flag-failed" or
└────────────────────┘  "dust" or "min-fee-not-met"
 terminal ✗

┌────────────────────┐  reject-quorum: ≥2 peers, "missing-inputs" or
│  ConflictRejected  │  "txn-mempool-conflict" (double-spend / dependency lost)
└────────────────────┘
 terminal ✗
```

### 7.2 Transitions and triggers

| From | To | Trigger |
|---|---|---|
| `Submitted` | `Validated` | `TxPolicyValidator` passes (size, fee, parse) |
| `Submitted` | `PolicyInvalid` | `TxPolicyValidator` rejects (size > 2 MB, fee < floor, unparseable) |
| `Validated` | `Dispatching` | Monitor pulls and assigns peers |
| `Dispatching` | `PeerAcked` | ≥1 peer accepted our `inv` and served `getdata` (or no `getdata` returned by feefilter timeout) |
| `Dispatching` | `Failed` | All sessions failed within timeout; no peer reachable |
| `PeerAcked` | `PeerRelayed` | ≥K peers (default 2) sent us `inv(txid)` after our announce |
| `PeerRelayed` | `MempoolSeen` | `MempoolBlockObserver` reports txid from independent feed |
| `MempoolSeen` | `Mined` | Block applied containing txid |
| `Mined` | `Confirmed` | Configured confirmation count reached |
| `MempoolSeen` | `EvictedOrDropped` | Observer no longer reports txid after T minutes (default 60) |
| `MempoolSeen` / `PeerAcked` | `ObserverUnknown` | No signal for T_unknown (default 240 min); requires manual or re-broadcast resolution |
| `EvictedOrDropped` | `Dispatching` | Re-broadcast attempt (Phase 2; may need fee bump — out of scope Phase 1) |
| `Mined` | `MempoolSeen` | Reorg: block containing tx orphaned; observer fires reverse event |
| Any non-terminal | `PolicyInvalid` | Reject quorum (≥N=2 peers same invalid-class reason) |
| Any non-terminal | `ConflictRejected` | Reject quorum (≥N=2 peers same conflicted-class reason) |

### 7.3 Retry policy

- `PeerAcked` stuck without `MempoolSeen` after 60s → re-announce to fresh peers (same bytes).
- `MempoolSeen` stuck without `Mined` after 24h → escalate to `ObserverUnknown`; surface alert.
- `EvictedOrDropped`: re-broadcast same bytes once. Further attempts require operator decision (Phase 2 may add fee bumping if needed).
- Total attempts capped at 5 with `[1m, 5m, 30m, 2h, 8h]` backoff.

### 7.4 Reorg handling

Reuse existing `MempoolBlockObserver` reorg events. `Mined → MempoolSeen`
on block-orphaned events. Monitor continues observing.

## 8. RavenDB schema

### 8.1 `OutgoingTransaction` (NEW)

ID pattern: `outgoing-tx/{txid}`. Collection: `OutgoingTransactions`.

```text
TxId               : string
RawHex             : string
ParsedSize         : int                       // bytes
DeclaredFee        : long                      // sat (from parse)
FeePerKb           : long                      // computed
State              : enum
CreatedAt, UpdatedAt
ClientId           : string?
ClientIp           : string?                   // for rate-limit attribution
PeerAttempts       : list of
  { PeerEndpoint, AnnouncedAt, GetDataServedAt?, RelayBackAt?,
    RejectCode?, RejectReason?, RejectClass? }
MempoolSeenAt      : DateTime?
MinedAt            : DateTime?
BlockHash          : string?
ConfirmationCount  : int
RetryCount         : int
NextRetryAt        : DateTime?
LastError          : string?
TerminalReason     : string?
```

Indexes:
- `OutgoingTransactions/ByState` — for periodic monitor scan
- `OutgoingTransactions/ByClient` — for per-client rate-limit metrics
- `OutgoingTransactions/RecentByCreatedAt` — for admin-ui list

### 8.2 `PeerRecord` (NEW)

ID pattern: `peer/{host}:{port}`. Collection: `Peers`.

```text
Host, Port
FirstSeen, LastSeen, LastConnectedAt
Source            : enum (DnsSeed, AddrGossip, Config, Inbound)
UserAgent
PeerVersion
PeerServices      : ulong
MeanLatencyMs
MaxRecvPayloadLength : long?                   // from peer's protoconf
MinFeePerKb       : long?                      // from peer's feefilter
SuccessCount, FailCount
RelayBackRatio
NegativeUntil?    : DateTime?                  // persisted cooldown
BanReason         : string?
```

Indexes:
- `Peers/Selectable` — "give me N good peers not in current pool, ASN-diverse"
- `Peers/ByNegativeUntil` — for cooldown cleanup

### 8.3 Migration of legacy `Broadcast` collection

The existing `Broadcast` document is retained **read-only**; no new writes
from the new path. A migration step (Phase 1 task) ports
historical `Broadcast` records into `OutgoingTransaction.PeerAttempts`
where possible (best-effort). Existing HTTP-provider audit history is
preserved.

This addresses audit H5 (two parallel audit models). After migration,
`Broadcast` collection becomes a historical archive; new code paths use
only `OutgoingTransaction`.

## 9. Public API

### 9.1 `WalletHub.Broadcast(hex) → bool` — DEPRECATED

Marked `[Obsolete]` with redirect to `BroadcastTracked`. Behavior preserved
in Phase 1 for backwards compatibility, but documented as legacy. Phase 2
removes it (after telemetry confirms no clients still using it).

Note: existing `BroadcastService.Broadcast` HTTP-provider flow is **gated
behind an operator-explicit feature flag** (`Consigliere:Broadcast:Legacy:Enabled`,
default `false`). OpenSource deployments never use this. Internal DXS
deployments may temporarily enable it during migration.

### 9.2 `WalletHub.BroadcastTracked(hex) → BroadcastReceipt` (NEW)

```csharp
public sealed record BroadcastReceipt(
    string TxId,
    BroadcastState State,
    DateTime At,
    string? Reason);
```

Flow:
1. Validate input size ≤ 2 MB at boundary.
2. Per-client rate limit check (configurable; default 10 tx/min/client).
3. Call `IBroadcastService.SubmitAsync(hex, ClientId, ClientIp)`.
4. Service runs `TxPolicyValidator` synchronously (size, fee, parse, dedup).
5. If `PolicyInvalid`, return receipt with terminal state immediately.
6. Otherwise persist `OutgoingTransaction(State=Validated)` and return
   `Submitted` receipt. State transitions stream via SignalR.

Auth model: **TBD in implementation.** Currently using existing WalletHub
auth (whatever it is). Discussion item: should `BroadcastTracked` require
explicit broadcast scope on the connection?

### 9.3 SignalR subscription

**Default: per-txid subscription via `SubscribeToBroadcast(txid)`.** Without
subscription, no events are emitted to the connection. This addresses
audit M4 (scope inconsistency) and prevents broadcast-flood DoS.

Server-side `SubscribeToBroadcast` enforces:
- The caller has broadcast scope (auth check).
- Per-connection subscription count cap.

## 10. Local policy validation (`TxPolicyValidator`)

Phase 1 enforces, before any P2P announce:

- **Size**: tx ≤ 2 MB (`LEGACY_MAX_PROTOCOL_PAYLOAD_LENGTH`). Reject `PolicyInvalid` if exceeded.
- **Parseable**: valid version, valid input/output count, well-formed scripts. Reject `PolicyInvalid` on parse error.
- **Fee floor**: `fee_per_1000_bytes >= config.MinFeePerKb` (default 0.5 sat/byte = 500 sat/KB).
- **Per-output dust check**: each output > dust threshold (configurable).
- **Idempotency**: if `OutgoingTransaction` exists for same `TxId`, return existing receipt (no duplicate flow).

Configuration:

```jsonc
"TxPolicy": {
  "MaxRawSize": 2097152,
  "MinFeePerKb": 500,
  "DustSatoshis": 1,
  "MaxClientRate": "10/min"
}
```

This addresses audit C2 (broadcast surface without abuse controls).

## 11. Configuration

Extends `Consigliere:Sources:Capabilities:Broadcast`:

```jsonc
"Consigliere": {
  "Broadcast": {
    "P2p": {
      "Enabled": true,
      "Network": "mainnet",
      "PoolSize": 8,
      "MinRelayBackPeers": 2,
      "RelayBackWindowMs": 30000,
      "PeerAckTimeoutMs": 8000,
      "HandshakeTimeoutMs": 15000,
      "BootstrapMaxConcurrency": 4,
      "BootstrapJitterMs": 200,
      "DnsSeeds": [
        "seed.bitcoinsv.io",
        "seed.satoshisvision.network",
        "seed.bitcoinseed.directory"
      ],
      "InitialPeers": [],
      "ListenInbound": false,
      "ListenPort": 8333,
      "MaxInboundConnections": 64,
      "UserAgent": "/ConsigliereThinNode:0.1.0/",
      "Services": "0x25",
      "AssociationIdMode": "None"
    },
    "TxPolicy": {
      "MaxRawSize": 2097152,
      "MinFeePerKb": 500,
      "DustSatoshis": 1,
      "MaxClientRate": "10/min"
    },
    "Retry": {
      "Backoff": ["1m", "5m", "30m", "2h", "8h"],
      "MaxAttempts": 5
    },
    "Confirmation": { "RequiredCount": 1 },
    "Reject": {
      "ClassificationQuorum": 2
    },
    "Legacy": {
      "Enabled": false
    }
  }
}
```

## 12. Failure modes

| Scenario | Behaviour |
|---|---|
| Cold start, all DNS seeds dead | Use `InitialPeers` from config. If empty, log + retry every 60s. |
| Pool drops below `MinPoolSize` | Replenish from candidate set + addr-gossip cache + DNS re-resolve. |
| Peer disconnects mid-broadcast | Reassign slot; if tx not served, re-announce on remaining peers. |
| Peer sends `reject` (invalid-class quorum reached) | Terminal `PolicyInvalid`. No further attempts. |
| Peer sends `reject` (conflicted-class quorum reached) | Terminal `ConflictRejected`. |
| Peer sends `reject` (transient) | Log; continue with other peers. |
| Duplicate broadcast of same `txid` | Idempotent: return existing receipt. |
| Process restart with in-flight broadcasts | Monitor scans non-terminal documents on startup; resumes from `Dispatching`. |
| Reorg orphans a Mined tx | Observer event reverses `Mined → MempoolSeen`. |
| Mempool eviction | `MempoolSeen → EvictedOrDropped`; re-broadcast once; surface alert. |
| Network-wide peer outage | Surface as critical metric `peer_pool_size < 1`; no fallback. |
| RavenDB data loss | In-flight raw hex and attempts are lost. **Recovery contract**: opensource deployments treat in-flight tx as best-effort. Operators wanting guarantees must journal raw submissions externally before calling `BroadcastTracked`. (Documented in operator guide.) |
| Our IP accumulates bans | Surface in metrics; mitigation in Phase 2 via peer rotation + addr-gossip diversity; or operator switches IP. |
| Tx exceeds 2 MB (Phase 1 cap) | `PolicyInvalid` at submission; no broadcast attempted. Phase 3 lifts cap via `extmsg`. |

## 13. Observability

### 13.1 Logs

Structured Serilog:
```csharp
logger.LogInformation("{@OutgoingTx}",
    new { TxId, State, PreviousState, Peer, ConfirmationCount });
```

### 13.2 Metrics

Counters:
- `outgoing_tx_submitted_total{client}`
- `outgoing_tx_validated_total`
- `outgoing_tx_policy_invalid_total{reason}`
- `outgoing_tx_peer_acked_total`
- `outgoing_tx_peer_relayed_total`
- `outgoing_tx_mempool_seen_total`
- `outgoing_tx_mined_total`
- `outgoing_tx_confirmed_total`
- `outgoing_tx_evicted_total`
- `outgoing_tx_observer_unknown_total`
- `outgoing_tx_conflict_rejected_total`
- `outgoing_tx_policy_invalid_at_peer_total{reason}`
- `outgoing_tx_failed_total`
- `peer_connect_attempts_total{result}`
- `peer_session_drops_total{reason}`
- `bootstrap_handshakes_total{result}`

Histograms:
- `time_to_peer_ack_ms`
- `time_to_relay_back_ms`
- `time_to_mempool_ms`
- `time_to_mined_ms`
- `peer_ping_rtt_ms`

Gauges:
- `peer_pool_size`
- `peer_pool_target`
- `outgoing_tx_pending_count{state}`
- `peer_pool_asn_diversity` — distinct ASNs in current pool
- `peer_pool_operator_diversity` — distinct subver-prefix groups

Critical alerts:
- `peer_pool_size < 1` for > 60s
- `outgoing_tx_failed_total` rate > 0
- `peer_pool_asn_diversity < 3`

### 13.3 admin-ui surfaces (Phase 3)

- **Peers** page
- **Outgoing transactions** page
- **First-run diagnostic** page: probes peer reachability without continuing
  to hammer the network; reports acceptance rate and recommends next step.

## 14. Implementation gates (replaces "phases")

Per audit H6, splitting the original Phase 1 into three gates that must
each pass before the next starts. Total estimate **3 weeks calendar**,
~2 weeks engineer time.

### Gate 1 — Conformance (1 week)

1. `MessageCodec`: encode/decode for `version`, `verack`, `ping`, `pong`,
   `inv`, `getdata`, `tx`, `notfound`, `reject`, `addr`, `headers`,
   `getheaders` (with locator parsing), `protoconf`, `feefilter`.
2. Tolerant parse of optional `version` association ID tail.
3. Byte-vector tests against captured frames from Spike E (real bitcoind
   bytes) + Teranode test corpus + `bsv-p2p` reference.
4. **No network code in Gate 1.** Pure codec + tests.

Exit criteria: all message types round-trip; reference vectors match;
malformed input handled with structured errors (no exceptions thrown into
network thread).

### Gate 2 — Network (1 week + 24h soak)

5. `PeerSession`: handshake, ping/pong, dispatch, feefilter tracking.
6. `PeerManager`: rate-limited bootstrap, pool maintenance.
7. `PeerDiscovery`: DNS-seed resolution, addr-gossip absorption.
8. **DISABLED-BY-DEFAULT** integration with Consigliere host: peer manager
   runs but `TxRelayCoordinator` and `BroadcastService` are not wired.
9. 24h soak test on fresh VPS IP. Collect:
   - Peer-acceptance rate per attempt
   - ASN/operator diversity
   - Relay-back observation rates
   - Reject reason distribution

Exit criteria: thresholds in §3.2 met. If not, design pivots before Gate 3.

### Gate 3 — Internal MVP (1 week, behind feature flag)

10. `TxPolicyValidator`.
11. `OutgoingTransactionStore` + `OutgoingTransactionMonitor`.
12. `TxRelayCoordinator` with reject classification.
13. `BroadcastService.SubmitAsync` extended path.
14. `WalletHub.BroadcastTracked` + per-txid SignalR subscription.
15. Per-client rate limiting on hub method.
16. End-to-end test broadcasting a real low-value OP_RETURN, observing
    mempool sighting + mining.

Exit criteria: 10+ real-tx successful broadcasts on testnet/mainnet,
end-to-end state machine transitions verified.

### Gate 4 — Production readiness (deferred)

17. Inbound listener (optional, opt-in).
18. Peer scoring + rotation.
19. OpenTelemetry metrics emission.
20. admin-ui pages.
21. Operator alerts + runbook.
22. `extmsg` framing for tx > 2 MB (lifts Phase 1 cap).
23. Deprecate `WalletHub.Broadcast(hex)`.

Status moves from `Draft` to `Approved` after Gate 2 soak passes §3.2
acceptance criteria.

## 15. Open questions and risks

### 15.1 Open questions

1. **K and N defaults**: Phase 1 ships N=8, K=2. Tune via Gate 2 metrics.
2. **Auth model for `BroadcastTracked`**: TBD in implementation. Discussion
   needed: API key, JWT, anonymous + IP rate limit, or "same as existing
   WalletHub auth".
3. **Mining-pool peering**: should `InitialPeers` include known
   mining-pool IPs (TAAL, GorillaPool, etc.) to short-circuit propagation?
   Trade-off: closer to miners vs. centralization signal. Deferred to
   Gate 4 with operator decision.
4. **`Broadcast.cs` migration script**: precise mapping of existing fields
   to `OutgoingTransaction.PeerAttempts`. To be detailed in implementation.

### 15.2 Risks

1. **Gate 2 acceptance rate may not meet §3.2 thresholds.** This is the
   single biggest risk. If 30% acceptance is not achievable from a fresh
   IP with the honest UA `/ConsigliereThinNode:0.1.0/`, we have three
   options: (a) negotiate whitelist with TAAL/GorillaPool, (b) impersonate
   `/Bitcoin SV/` UA (with documented risk of policy retaliation),
   (c) pivot to embedded bitcoind sidecar. Decision happens at Gate 2 review.
2. **Tx propagation to miners is unmeasured.** Even if our 8 peers accept
   us, distance-to-miner is unknown. Gate 2 soak must measure
   `time_to_mined_ms` p50/p99 explicitly.
3. **Reject reason taxonomy may be incomplete.** §5.2 table is derived
   from public BSV source; production peers may use custom reasons. Gate 2
   soak collects observed reasons; table extended before Gate 3.
4. **Protocol drift**: Teranode adoption may shift requirements (e.g.,
   association ID becomes effectively mandatory). Codec version-locked to
   `bitcoin-sv` 1.2.x; `AssociationIdMode` config flag enables fast
   response without rearchitecture.
5. **Bootstrap UX**: opensource user runs Consigliere for first time on a
   fresh VPS, expects "it just works". First-run diagnostic command
   surfaces peer-reachability stats; if peer pool fails to reach 4+ in
   first 5 minutes, surface actionable error (not silent retry).

## 16. References

- Spike journey: [p2p-broadcaster-design.md §15–§16](./p2p-broadcaster-design.md)
- Design audit: [thin-node-design-audit.md](./thin-node-design-audit.md)
- Reference implementation (TypeScript): `github.com/kevinejohn/bsv-p2p` (`src/messages/version.ts`, `src/index.ts`)
- Reference implementation (Go): `github.com/bsv-blockchain/teranode` (`services/legacy/peer/peer.go::negotiateOutboundProtocol`)
- Reference implementation (C++): `github.com/bitcoin-sv/bitcoin-sv` (`src/net/net_processing.cpp`, `src/net/net.cpp`, `src/protocol.cpp`, `src/net/net_message.cpp`)
- Spike artifacts: `tests/Spikes/P2p/Spike.Handshake/Program.cs`, gist series A–K
- BSV protocol docs: `https://wiki.bitcoinsv.io/`

## 17. Audit findings — reconciliation summary

Audit `thin-node-design-audit.md` (GPT-5 Codex) verdict: MAJOR REVISION REQUIRED.

| Finding | Severity | Status |
|---|---|---|
| C1: Premature "Approved" status | Critical | **Accepted** — status now `Draft`; §3.2 evidence gate; §14 Gate 2 must pass before approval |
| C2: Broadcast surface lacks abuse/validity controls | Critical | **Accepted** — §10 `TxPolicyValidator`, §9.2 per-client rate limit, §9.3 per-txid subscription, all moved into Phase 1 (Gate 3) |
| C3: Outbound sizing under-specified | Critical | **Accepted** — §3 specifies `protoconf` send, §5.1 respects peer `maxRecvPayloadLength` and `feefilter`; Phase 1 caps tx at 2 MB; `extmsg` deferred to Gate 4 |
| H1: Association ID claim too strong | High | **Accepted** — §3 softens to "current compatibility"; §11 `AssociationIdMode` config |
| H2: We don't send `protoconf` | High | **Accepted** — §3 and §5.1 add `protoconf` send after our `verack` |
| H3: 32-way bootstrap looks like scanning | High | **Accepted** — §6.1 rate-limited (4 concurrent + jitter + per-/24 diversity) |
| H4: Lifecycle can't distinguish reject types | High | **Accepted** — §5.2 classification table; §7.1 adds `PolicyInvalid`, `ConflictRejected`, `EvictedOrDropped`, `ObserverUnknown` states; classification moved to Gate 3 |
| H5: Two parallel audit models | High | **Accepted** — §8.3 migrates `Broadcast` collection into `OutgoingTransaction`; §9.1 deprecates legacy hub method |
| H6: Phase 1 in 2 weeks unrealistic | High | **Accepted** — §14 restructured into Gates 1/2/3 with exit criteria |
| M1: `getheaders` overconfident | Medium | **Accepted** — §5.1 specifies locator parsing before empty reply |
| M2: Outbound-only reach handwaved | Medium | **Accepted** — §13.2 adds peer diversity + ASN metrics; §15.2 risk #2 explicit |
| M3: Disaster recovery undefined | Medium | **Accepted** — §12 states recovery contract: in-flight tx best-effort, lost on DB wipe |
| M4: SignalR scope inconsistent | Medium | **Accepted** — §9.3 default = per-txid subscription with auth check |
| L1: Don't impersonate SV-Node UA | Low | **Accepted** — §3.1 default UA `/ConsigliereThinNode:0.1.0/`; impersonation is a design risk not a config detail |
| L2: Component paths not in zone catalog | Low | **Accepted** — §4.2 zone catalog update before implementation |

Open questions raised by auditor, now answered:
- Tx size for Phase 1: **2 MB cap** (legacy max).
- Fee policy: **enforce locally** (parse + min-fee + respect peer feefilter).
- ARC/RPC fallback: **rejected for opensource**; legacy HTTP-provider path retained behind operator-explicit flag for DXS internal migration only.
- Mining-pool peer hardcoding: **deferred to Gate 4** with operator decision.
- Auth model: **TBD in implementation** (open question §15.1).
- Spike J/K peer details: 1/14 acceptance rate on two different IPs (Mac residential + DigitalOcean DC1). Full distribution captured in Gate 2 soak.
