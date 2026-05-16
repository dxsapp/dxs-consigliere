# P2P Transaction Broadcaster — Design

Status: **SUPERSEDED** by [consigliere-thin-node-design.md](./consigliere-thin-node-design.md)
Owner: Consigliere runtime
Last updated: 2026-05-14

> This document is the journey, not the destination. It records the spike
> chain that led us from "P2P broadcast as anonymous client" through
> "thin-node from scratch" to the final approved design. §15 captures the
> Spike A blocker, §16 captures Spikes D–I (where we mistakenly believed
> we were stuck), and **Spike J/K finally proved the protocol works once
> we removed the optional `LIMITED_BYTE_VEC` association ID tail and set
> `services=0x25`**. The thin-node design has moved to its own document.

> ⚠ This document was drafted before Spike A. Phase 1 (direct P2P broadcast
> from an anonymous client to public BSV peers) is **not viable** under
> current mainnet operator policy. §15 captures the spike outcome and the
> open strategic choices that remain. Sections §1–§14 are preserved as the
> original design and remain valid IF Phase 1 is unblocked by a peering
> agreement or a self-hosted listening node.

## 1. Motivation

Consigliere relays user transactions to the BSV mainnet. Today this is done
through HTTP providers (`Bitails`, `WhatsOnChain`) and the optional local
`bitcoind` JSON-RPC. In production:

- HTTP providers are unreliable as a primary channel (rate limits, downtime,
  silent drops, no relay feedback).
- A self-hosted `bitcoind` is the only fully reliable option, but costs
  $200+/month and requires operating a node.

A direct Bitcoin **peer-to-peer** broadcaster, speaking the BSV network
protocol over TCP to public peers, gives us a self-hosted-quality reliability
profile **without running a node**. We pay only for outbound TCP and a handful
of long-lived connections.

This document is the source of truth for the broadcaster's architecture.

## 2. Goals and non-goals

### Goals
- P2P is the **primary** broadcast channel.
- HTTP providers (`Bitails`, `WhatsOnChain`, optional `bitcoind`) are the
  **fallback** path, reused as-is.
- Every submitted transaction has a tracked lifecycle persisted in RavenDB,
  from submission through confirmation.
- API surface stays compatible: existing `WalletHub.Broadcast(hex) → bool`
  keeps working; a new `BroadcastTracked` method exposes the tracked model.
- Mainnet only in Phase 1.

### Non-goals (Phase 1)
- Testnet support.
- Signing transactions. All inbound transactions are pre-signed.
- Fee-bumping or transaction replacement.
- Acting as a full node: we do **not** index blocks, validate scripts at the
  network level, or serve headers/blocks to peers. We are a write-only peer.
- mAPI / ARC integration. Can be added as another HTTP fallback later.

## 3. Inventory of existing primitives

Confirmed in repository audit (2026-05-14):

| Area | What exists | Path |
|---|---|---|
| HTTP broadcast — Bitails | `BitailsRestApiClient.Broadcast(txHex)` | `src/Dxs.Infrastructure/Bitails/BitailsRestApiClient.cs` |
| HTTP broadcast — WoC | `WhatsOnChainRestApiClient.BroadcastAsync(body)` | `src/Dxs.Infrastructure/WoC/WhatsOnChainRestApiClient.cs` |
| RPC broadcast — node | `BitcoindService.Broadcast(hex)` | `src/Dxs.Consigliere/Services/Impl/BitcoindService.cs` |
| Hub entry point | `WalletHub.Broadcast(transaction)` | `src/Dxs.Consigliere/WebSockets/WalletHub.cs` |
| Orchestrator interface | `IBroadcastService` | `src/Dxs.Consigliere/Services/` |
| Audit document | `Broadcast` (attempts per provider) | `src/Dxs.Consigliere/Data/Models/Broadcast.cs` |
| Tx model | `Transaction` (raw, hex, id, inputs, outputs) | `src/Dxs.Bsv/Models/Transaction.cs` |
| Tx hex persistence | `TransactionHexData` | `src/Dxs.Consigliere/Data/Models/Transactions/TransactionHexData.cs` |
| Binary codec primitives | `BitcoinStreamReader`, `BufferWriter`, `HashStream` | `src/Dxs.Bsv/Protocol/` |
| Background worker base | `PeriodicTask` | `src/Dxs.Common/BackgroundTasks/PeriodicTask.cs` |
| Mempool/block observers | Existing Bitails/JungleBus/ZMQ ingestion | various |

Confirmed missing:
- No Bitcoin P2P protocol code anywhere in the repo. `Dxs.Bsv/Zmq` talks only
  to a local node's ZMQ socket, not to remote peers.
- No outbound transaction queue, retry state, or lifecycle document.
- No peer registry or peer scoring.

## 4. High-level architecture

```
┌──────────────────────────────────────────────────────────────────┐
│  Public API:  WalletHub.Broadcast(hex)   |   WalletHub.BroadcastTracked(hex) │
└─────────────────────────┬────────────────────────────────────────┘
                          ▼
                ┌─────────────────────┐
                │ IBroadcastService   │   extended
                │  (orchestrator)     │
                └──────────┬──────────┘
                           │ persist state
                           ▼
                ┌─────────────────────┐
                │ OutgoingTxStore     │   RavenDB: outgoing-tx/{txid}
                └──────────┬──────────┘
                           │
        ┌──────────────────┼────────────────────────────┐
        ▼                  ▼                            ▼
┌──────────────┐   ┌────────────────┐         ┌──────────────────┐
│ P2P Engine   │   │ HTTP Fallback  │         │ Lifecycle Worker │
│ (primary)    │   │ Orchestrator   │         │ (PeriodicTask)   │
└──────┬───────┘   └────────┬───────┘         └────────┬─────────┘
       │                    │                          │
       ▼                    ▼                          ▼
┌──────────────┐   ┌────────────────┐         ┌──────────────────┐
│ Peer Pool    │   │ Existing HTTP  │         │ Mempool/Block    │
│ Manager      │   │ clients (3)    │         │ Observer (reused)│
└──────┬───────┘   └────────────────┘         └──────────────────┘
       │
       ▼
┌──────────────┐
│ PeerConnect. │   long-lived TCP, version/verack, inv/getdata/tx
└──────┬───────┘
       │
       ▼
   BSV network
```

## 5. Subsystems

### 5.1 P2P Engine (`src/Dxs.Bsv/P2p/`)

Implemented from scratch on top of existing `BitcoinStreamReader` /
`BufferWriter`. No external dependency.

Layers:

- **`MessageCodec`** — encode/decode Bitcoin P2P messages. Phase 1 message set:
  `version`, `verack`, `ping`, `pong`, `inv`, `getdata`, `tx`, `reject`,
  `addr`, `sendheaders` (ignored), `feefilter` (ignored).
- **`PeerConnection`** — one TCP socket. State machine:
  `Connecting → Handshaking → Ready → Closed`. Implements ping/pong keepalive
  every 30s, idle drop after 90s missed pong.
- **`PeerSession`** — high-level API per peer:
  - `SendTransactionAsync(tx, ct)` returns `PeerSendResult { Sent, ErrorReason }`.
  - Raises events: `OnInvReceived(txid)`, `OnReject(txid, reason)`,
    `OnAddrReceived(addrs)`.
- **`P2pBroadcaster`** (in `src/Dxs.Consigliere/Services/P2p/`) — fan-out
  facade: takes a `tx`, calls `SendTransactionAsync` on N peers concurrently,
  aggregates `PeerSendResult`, returns immediately when at least 1 peer
  succeeds, and continues collecting relay-back `inv` events in the
  background for up to `RelayBackWindowMs` (default 30s).

### 5.2 Peer Pool Manager (`src/Dxs.Bsv/P2p/Peers/`)

- **Source of candidates** (Phase 1):
  - Hardcoded seed list in `appsettings` (8–10 known good peers, found and
    validated during Spike A).
  - Bitails / WoC peer-list endpoints (refreshed every 6 hours).
- **Active pool**: 6 long-lived `PeerSession` connections kept open at all
  times. Replenished from candidates as connections drop.
- **Scoring** (Phase 2):
  - `latency_ms` (rolling mean), `drop_rate`, `reject_rate`,
    `relay_back_ratio` (how often peer's inv came back after our tx send).
  - Peers below a threshold get banned for an exponential cooldown.
- **Persistence**: `PeerRecord` document per host:port.

### 5.3 HTTP Fallback Orchestrator

Reuses existing clients. Activated when P2P sent zero peer acks within
`PeerAckTimeoutMs` (default 5s).

Mode `parallel-first-success`: fire all configured HTTP providers in parallel,
take the first success. Default order of providers (configurable):
`bitails`, `whatsonchain`, `bitcoind`.

### 5.4 Lifecycle Worker (`OutgoingTransactionMonitor : PeriodicTask`)

Runs every 15s. Scans non-terminal `OutgoingTransaction` documents:

- For `Submitted`/`Dispatching` stuck > 60s — re-enqueue dispatch.
- For `PeerAcked` / `MempoolSeen` — query observer state and advance to
  `Mined` / `Confirmed`.
- For `PeerAcked` with no mempool sighting after the configured re-broadcast
  window — re-broadcast (same tx bytes).
- For exceeded `MaxAttempts` — terminal `Failed`.

### 5.5 Mempool / Block Observer

Reuses the existing inbound feeds (Bitails websocket, JungleBus, optional
ZMQ from local node). A small adapter subscribes to inbound txids and
publishes a `MempoolSightingEvent` keyed by txid. The lifecycle worker reads
these and advances state. Block ingestion already happens — we hook onto the
existing block-applied event to detect `Mined`.

## 6. State machine

```
                              ┌─────────────┐
                              │  Submitted  │  ← client called Broadcast
                              └──────┬──────┘
                                     │ basic local validation
                                     ▼
                              ┌─────────────┐
                  ┌──reject───│ Dispatching │──────┐
                  │           └──────┬──────┘      │
                  │                  │             │
                  │           any peer sent OK     │ all peers failed
                  │                  ▼             ▼
                  │           ┌─────────────┐  ┌─────────────────┐
                  │           │  PeerAcked  │  │ HttpFallback    │
                  │           └──────┬──────┘  └────────┬────────┘
                  │                  │                  │
                  │           ≥K peers relayed inv      │ any provider ACK
                  │                  ▼                  ▼
                  │           ┌─────────────────────────────────┐
                  │           │         MempoolSeen             │
                  │           └────────────────┬────────────────┘
                  │                            │  observed in block
                  │                            ▼
                  │                    ┌─────────────┐
                  │                    │    Mined    │
                  │                    └──────┬──────┘
                  │                           │ N confirmations
                  │                           ▼
                  │                    ┌─────────────┐
                  │                    │  Confirmed  │   terminal ✓
                  ▼                    └─────────────┘
          ┌─────────────┐         ┌─────────────┐
          │  Rejected   │         │   Failed    │   all retries exhausted
          └─────────────┘         └─────────────┘
            terminal ✗              terminal ✗
```

State transitions are atomic Raven updates. Terminal states: `Confirmed`,
`Rejected`, `Failed`. A reorg that orphans a confirmed tx moves
`Mined → MempoolSeen` (existing block reorg handling fires the same event
hook).

## 7. Public API contract

### 7.1 `WalletHub.Broadcast(hex) → bool` (legacy, retained)

Existing behaviour preserved. Internally calls `IBroadcastService.SubmitAsync`
in fire-and-forget mode and returns `true` if persistence succeeded.

### 7.2 `WalletHub.BroadcastTracked(hex) → BroadcastReceipt` (new)

```csharp
public sealed record BroadcastReceipt(
    string TxId,
    BroadcastState State,
    DateTime At,
    string? Reason);
```

- Returns `Submitted` (or the terminal state if dedup hits an existing
  finished tx) within ~10–20ms (RavenDB write time).
- The client subscribes to `OnBroadcastStateChanged(txid, state, at, reason)`
  events through SignalR for downstream transitions.

### 7.3 SignalR events

- `OnBroadcastStateChanged` — emitted per state transition for all tracked tx.
  Filtering is performed client-side by txid, or via an optional
  `SubscribeToBroadcast(txid)` hub method that scopes events.

## 8. RavenDB schema

### 8.1 `OutgoingTransaction`

ID pattern: `outgoing-tx/{txid}`. Stored in a dedicated collection
`OutgoingTransactions`.

```text
TxId              : string
RawHex            : string                 // duplicated into TransactionHexData
State             : enum
CreatedAt         : utc
UpdatedAt         : utc
ClientId          : string?                // caller identity, if any
PeerAttempts      : list of
  { Peer, SentAt, RelayedAt?, RejectReason? }
HttpAttempts      : list of
  { Provider, At, Success, Error? }
MempoolSeenAt     : utc?
MinedAt           : utc?
BlockHash         : string?
ConfirmationCount : int
RetryCount        : int
NextRetryAt       : utc?
LastError         : string?
TerminalReason    : string?
```

An index `OutgoingTransactions/ByState` powers the lifecycle worker query.

### 8.2 `PeerRecord` (Phase 2)

ID pattern: `peer/{host}:{port}`.

```text
Host, Port
FirstSeen, LastSeen, LastConnectedAt
UserAgent
SuccessCount, FailCount
MeanLatencyMs
RelayBackRatio
BannedUntil?, BanReason?
IsSeed : bool                    // came from the hardcoded seed list
```

### 8.3 Existing `Broadcast` document

Kept as a per-attempt audit log (Bitails, WoC, bitcoind path). No schema
changes. The new `OutgoingTransaction` document is the lifecycle owner; the
existing `Broadcast` collection becomes a flat audit table per HTTP attempt.

## 9. Configuration

Extends the existing `Consigliere:Sources:Capabilities:Broadcast` block. Full
example:

```jsonc
"Consigliere": {
  "Broadcast": {
    "P2p": {
      "Enabled": true,
      "Network": "mainnet",
      "PoolSize": 6,
      "MinSuccessfulRelays": 2,
      "PeerAckTimeoutMs": 5000,
      "RelayBackWindowMs": 30000,
      "Seeds": [
        "node1.example:8333",
        "node2.example:8333"
      ],
      "PeerDiscoveryEnabled": true,
      "MaxConnectionsPerHost": 1,
      "UserAgent": "/Consigliere:1.0/"
    },
    "HttpFallback": {
      "Enabled": true,
      "Providers": [ "bitails", "whatsonchain", "bitcoind" ],
      "Mode": "parallel-first-success",
      "PerProviderTimeoutMs": 8000
    },
    "Retry": {
      "Backoff": [ "1m", "5m", "30m", "2h" ],
      "MaxAttempts": 5
    },
    "Confirmation": {
      "RequiredCount": 1
    }
  }
}
```

## 10. Failure modes

| Scenario | Behaviour |
|---|---|
| All peers unreachable | Activate HTTP fallback. If HTTP also fails, mark `RetryQueued`, schedule next backoff slot. |
| Peer sends `reject` (invalid signature, malformed, etc.) | Record `RejectReason`, continue with other peers. If all peers reject for the same invalid-class reason → terminal `Rejected`. |
| Peer sends `reject` (transient: mempool full, fee too low, etc.) | Record, continue, do not classify as terminal. |
| Resubmit of same txid | Idempotent: return current receipt without creating a new flow. |
| Mempool-seen but stuck > re-broadcast window | Re-send same bytes to a fresh peer subset. No fee-bump in Phase 1. |
| Reorg orphans a `Mined` tx | Observer event moves it back to `MempoolSeen`; lifecycle re-monitors. |
| Process restart | Lifecycle worker scans non-terminal documents and resumes. |
| Bad seed-list entries | Peer manager bans + falls back to runtime-discovered peers. |

## 11. Observability

Logs (Serilog, structured): one event per state transition with
`@OutgoingTx { TxId, State, Peer, Provider, Reason }`. Pattern lifted from the
existing `BroadcastService.logger.LogError("{@Broadcast}", ...)` usage.

Metrics (Phase 2 — adds OpenTelemetry or in-house counters following
`TransactionFilterMetrics` pattern):

- Counters: `tx_submitted`, `tx_peer_acked`, `tx_peer_relayed`,
  `tx_http_fallback`, `tx_mempool_seen`, `tx_mined`, `tx_confirmed`,
  `tx_rejected`, `tx_failed`.
- Histograms: `time_to_peer_ack_ms`, `time_to_relay_back_ms`,
  `time_to_mempool_ms`, `time_to_mined_ms`.
- Per-peer: `peer_send_success`, `peer_latency_ms`, `peer_relay_back_ratio`.

admin-ui (Phase 3):
- **Outgoing transactions** page: filterable list of `OutgoingTransaction`
  with state badge, attempt history, retry button.
- **Peers** page: live pool, scoring, ban list, force-disconnect button.

## 12. Implementation phases

### Phase 1 — MVP
1. `MessageCodec` covering `version`, `verack`, `ping`, `pong`, `inv`,
   `getdata`, `tx`, `reject`.
2. `PeerConnection` with handshake + keepalive.
3. `PeerSession` with `SendTransactionAsync` and inbound event raising.
4. Hardcoded seed list of 8–10 BSV peers (from Spike A).
5. `P2pBroadcaster` — fan-out, returns receipt after first peer send.
6. `OutgoingTransaction` document, store, and index.
7. `IBroadcastService` extended with `SubmitAsync` and tracked flow.
8. HTTP fallback wired through existing clients.
9. `WalletHub.BroadcastTracked` plus `OnBroadcastStateChanged` event.
10. Basic `OutgoingTransactionMonitor` (PeriodicTask) — minimal retry.

### Phase 2 — Reliability
11. Relay-back inv accounting against the active pool.
12. `PeerPoolManager` with scoring, bans, rotation.
13. Peer discovery from Bitails / WoC peer endpoints + `addr` exchange.
14. Lifecycle worker hooked to mempool/block observer events for
    `MempoolSeen`, `Mined`, `Confirmed`.
15. Re-broadcast of stuck transactions with backoff.
16. Reject reason classification (invalid vs transient).

### Phase 3 — Production hardening
17. Idempotency contract enforced at the hub.
18. Caller rate limiting.
19. OpenTelemetry metrics emission.
20. admin-ui surfaces: Outgoing + Peers panels.
21. Operational dashboards and alerts.

## 13. Open questions

These do not block Phase 1 but should be resolved before Phase 2 closes.

1. Exact K and N for relay-back quorum. Default N=6, K=2. Tune by metrics.
2. Confirmation threshold (default 1, configurable).
3. SignalR subscription model — broadcast-to-all-clients vs
   `SubscribeToBroadcast(txid)` scoping. Default to per-txid subscription.
4. Re-broadcast policy for `MempoolSeen` stuck transactions.
5. Whether to support an optional "preferred peer" override that uses the
   local bitcoind, when configured, as a priority pool member.

## 14. Research spikes (pre-Phase 1)

Before any production code, two minimal spikes confirm assumptions and
de-risk the protocol layer.

- **Spike A — TCP pinger.** Connect to 5–10 candidate BSV peers, perform
  `version` / `verack` handshake, exchange `ping`/`pong`, measure RTT, log
  user-agents. Output: a validated seed list + confirmation that the codec
  works against live nodes.
- **Spike B — inv receiver.** Connect to one peer, stay connected ~60s,
  decode every inbound message. Output: confirmation that our framing /
  parsing handles real network traffic, and a baseline for the rate of
  inv/tx messages we will need to process per peer.

Both spikes live in a throwaway location (`tests/Spikes/P2p/` or a temporary
console project), are not shipped, and are deleted before Phase 1 PR review.

## 15. Spike A — findings and strategy revision

**Date:** 2026-05-14
**Outcome:** Phase 1 of this design is blocked. Spike A reached zero
successful handshakes out of eight candidate peers. Spike B was not run.

### 15.1 What was tested

A throwaway console project (`tests/Spikes/P2p/Spike.Handshake/`) implemented
the Bitcoin P2P wire format from scratch: `version`/`verack`/`ping`/`pong`,
BSV mainnet magic `0xE3E1F3E8`, protocol 70016, user-agent
`/Bitcoin SV:1.2.1/`, services `0x21` (NODE_NETWORK | NODE_BITCOIN_CASH),
matching exactly what live BSV nodes advertise.

Peer set was sourced from `WhatsOnChain /v1/bsv/main/peer/info` filtered to
peers WoC is currently outbound-connected to (i.e. confirmed accepting
inbound from at least one network operator). Eight peers across multiple
operators (OVH, Hetzner, etc.) were tested sequentially with TCP_NODELAY
and a 500ms inter-peer delay.

### 15.2 Observed behaviour

All eight peers either:
- closed the TCP connection with FIN immediately after `accept`, before any
  application data was exchanged in either direction; or
- replied with RST shortly after our `version` frame.

The peer never responded with a `version`, `verack`, or `reject`. Wire-level
framing, magic, and payload were verified by hex dump.

### 15.3 Root cause (from BSV source)

The behaviour matches three explicit code paths in
[`src/net/net.cpp::AcceptConnection`](https://github.com/bitcoin-sv/bitcoin-sv/blob/master/src/net/net.cpp):

```cpp
if (IsBanned(addr) && !whitelisted) { CloseSocket(hSocket); return; }
if (!whitelisted && nConnectionsFromAddr >= nMaxConnectionsFromAddr) { ... }
if (nInbound >= nMaxInbound) { /* try evict; else */ CloseSocket(hSocket); }
```

Each of these closes the socket **before reading any bytes** from the peer
— consistent with our observation. We did not produce a `Misbehaving` event
ourselves (no version exchanged), so the most plausible explanation is
one of:

- the spike's public IP is on a banlist (shared/automatic across operators);
- the operators run an inbound whitelist and only accept peers they have
  agreements with;
- the operators' inbound slot capacity is saturated by their existing
  business peers.

The `extversion` hypothesis was investigated and **rejected**: current BSV
master (`net_processing.cpp`, `protocol.h`) contains no `extversion`
message type. The handshake is the standard `version`/`verack`.

### 15.4 Implications for the design

The core premise of this document — "P2P primary because HTTP providers are
unreliable and running a node is expensive" — survives the cost analysis
but fails the access analysis. Direct, unauthenticated P2P broadcast from
an anonymous client to public BSV peers in 2026 is **operationally
blocked**, independent of our implementation quality.

Anything in §1–§14 that depends on speaking to public peers without prior
agreement (sections §3 P2P Engine, §5.1 P2P Engine, §5.2 Peer Pool Manager
peer discovery via DNS / `addr` exchange, §9 P2P seed list) is on hold.

### 15.5 Forward paths (no decision yet)

1. **Self-hosted lightweight node.** Run a single BSV node on a small VPS
   (~$50–100/mo). The node opens outbound peer connections (which works —
   we observed `inbound:false` peers in WoC's list, meaning peers accept
   *outbound* requests from listening nodes). We push transactions through
   its RPC `sendrawtransaction`. This restores reliability without
   requiring us to win peering agreements.
2. **ARC-first.** Adopt TAAL ARC and/or GorillaPool ARC as the primary
   broadcast channel. These are HTTPS-based services with miner SLAs,
   designed exactly for our use case. Bitails / WhatsOnChain remain as
   secondary fallback. The "HTTP providers are unreliable" premise needs
   re-examination specifically for ARC, which is a different reliability
   tier from public WoC.
3. **Peering agreement.** Reach out to TAAL / GorillaPool / nChain to get
   our IP whitelisted on their peers. Original §1–§14 design then applies
   unchanged. Lead time and commercial terms unknown.
4. **Hybrid: ARC + self-node hot spare.** ARC as primary, a small self-host
   as warm fallback. Maximum reliability, ~$50/mo + ARC fees.

### 15.6 Decision pending

Strategic call deferred. When picked, this document will either be:
- amended in place (option 3 reactivates §1–§14 wholesale), or
- superseded by a new design document (options 1, 2, 4 require a new
  architecture; this file will be marked superseded with a forward link).

The spike project at `tests/Spikes/P2p/Spike.Handshake/` is kept on disk
as evidence and may be removed once the new direction is set.

## 16. Spikes D–I (thin-node feasibility deep dive)

**Date:** 2026-05-14
**Outcome:** Protocol-level handshake is now correctly implemented per
bitcoin-sv source code, but peers still silently disconnect us. Direct
P2P broadcast from a "from-scratch" thin-node implementation is not
working out of the box. Architecture decision pending.

### 16.1 Spike chronology

After §15 the goal shifted from "anonymous broadcast client" to
"Consigliere as a listening BSV peer (thin-node)". User constraint:
operationally cheap (no pruned-node ops cost), opensource-runnable on any
VPS, no dependency on a co-located full node.

| Spike | Purpose | Outcome |
|---|---|---|
| **D**  | Re-run handshake from user's production VPS to 17 WoC-listed listening peers | 0/17, all silently disconnect or RST. Hypothesis: pruned-on-same-IP duplicate-from-addr block. |
| **D2** | Stop pruned, wait 3 min, retry to **pruned's own peer-list** (12 IPs proven peering with this VPS) + 3 WoC controls | 0/15. Hypothesis disproved — peers reject us regardless of duplicate state. |
| **E**  | Sniff bitcoind's own outbound `version` bytes with raw-socket capture; compare to ours | Captured 5 distinct version frames. Discovered "19 bytes after `relay`" pattern — initially misinterpreted as one structure. |
| **F**  | Re-add the 19-byte "association ID" tail per Spike E discovery | 0/17 again. |
| **G**  | (Skipped) Was for verifying our outbound bytes via dump + wait 5 min cooldown. Superseded by source-code analysis. |
| **H**  | Source-code reverse engineering: read `net_processing.cpp`, `net.cpp`, `protocol.cpp`, `net_message.cpp`, `association_id.cpp`. **Corrected `assoc_id` encoding** (`compact_size(17) + IDType(0x00=UUID) + 16 bytes`, not `01 11 00 + UUID`). The `0x01` in Spike E capture was the `fRelay` byte, not part of assoc_id. | 0/14 locally too. |
| **I**  | Single-peer debug: drain all bytes peer sends before close. Peer sends **zero bytes** then graceful FIN. | Confirms: peer parsed our version, decided to silent-disconnect. |

### 16.2 The silent disconnect path (source-confirmed)

The pattern "peer accepts TCP, receives valid version, sends FIN with no
data" matches exactly one code path in bitcoin-sv master:

**`src/net/net_processing.cpp:4972-4983`** — invalid magic at frame-parse:
```cpp
if (memcmp(hdr.GetMsgStart(), chainparams.NetMagic(), 4) != 0) {
    LogPrint("PROCESSMESSAGE: INVALID MESSAGESTART ...");
    connman.Ban(pfrom->...);              // 24h IP ban
    pfrom->fDisconnect = true;
    return false;                          // ← no REJECT push
}
```

— or — `Misbehaving(pfrom, banScoreThreshold, "invalid-UA")` at line
**`1746`** when `IsClientUABanned(cleanSubVer)` matches the operator's
`-banclientua` substring list. `Misbehaving(100, ...)` sets
`state->fShouldBan = true` and a separate thread closes the socket
without sending REJECT (`609-632`).

Other disconnect paths in `ProcessVersionMessage`
(services mismatch, version too low, assoc_id malformed) **do** send a
REJECT before disconnect, which we would observe as ≥24 bytes received.

We verified our magic is correct (`0xE3E1F3E8` LE on wire, byte-for-byte
matches bitcoind's own captures). So the most plausible cause is UA-based
or accumulated-misbehavior ban for our IP/UA combination.

### 16.3 What we know works on this VPS

User's pruned bitcoin-sv currently holds **20 long-lived peer connections**
from IP `159.89.105.214`. So peer acceptance of this IP **is** possible
under some conditions, just not for our quick-handshake spike.

Two plausible explanations remain:
1. **Long-lived connections are grandfathered** in operator banlists. New
   short-lived connections from same IP face stricter filters.
2. **UA-based filters** — operators may have substring banlists that
   match `"/Bitcoin SV:"` from non-whitelisted IPs (i.e. impersonators),
   while real bitcoin-sv binary running from the same IP for hours
   establishes reputation via other signals (correct authch, sendheaders,
   protoconf timing, addr-relay participation, etc.).
3. **Connection rate limiting**: our spikes opened 14+ rapid connections
   in 30 seconds. Some operators may temporarily block sources that
   create-and-drop connections faster than a real node does.

We did not conclusively distinguish between these.

### 16.4 What a passing handshake actually requires (source-derived spec)

From the agent research and source code reading, a "passable" outbound
handshake must:

1. TCP connect with `TCP_NODELAY=1`. No bind, no other options.
2. Within 60s send a `version` payload (122 bytes typical) with:
   - protocol 70016, services 0x20 or 0x21, current timestamp
   - `addr_recv` (peer, ipv4-mapped, port 8333 BE)
   - `addr_from` (zeros + zeros + 0 port)
   - random 8-byte nonce (must be globally unique per attempt)
   - var_str user-agent
   - int32 start_height
   - byte fRelay = 0x01
   - `LIMITED_BYTE_VEC` association ID: `0x11 + 0x00 + 16 random bytes`
3. Magic `0xE3E1F3E8` on wire `e8 f3 e1 e3`.
4. Checksum = first 4 bytes of `sha256d(payload)`.
5. After receiving peer's `version`: immediately send `verack`, then
   `protoconf` with `2 fields, maxRecvPayloadLength=2097152,
   streamPolicies="Default"`.
6. After receiving peer's `verack`: optionally `authch`, `sendheaders`,
   `sendcmpct`. `fSuccessfullyConnected = true`.
7. Respond to peer's `ping` with `pong` carrying same nonce.
8. Don't trigger any `Misbehaving(N>0)` — no malformed messages, no
   pre-version traffic, no duplicate version.

Our Spike H meets all of these on the wire. Yet still 0/14.

### 16.5 Forward paths (pending decision)

1. **More diagnostic work**: bidirectional tcpdump capture during a
   real bitcoind restart — see what peer sends in the FIRST seconds of
   handshake. May reveal we're missing a specific post-verack message
   peers expect. Cost: a few more hours.
2. **Embed `bitcoin-sv` binary as a sidecar**: ship the real node binary
   inside Consigliere's container. It runs in pruned mode with minimal
   storage; broadcast goes via JSON-RPC `sendrawtransaction`. User-facing,
   no separate node to operate. Cost: ~1 week for packaging, lifecycle,
   resource caps, recovery-from-crash. Re-creates the operational cost
   user wanted to eliminate, but in an embedded form. Recovery may still
   need block reindex on certain crashes.
3. **Pivot to ARC-first design**: TAAL ARC (or GorillaPool ARC) as
   primary broadcast channel, Bitails / WhatsOnChain as redundancy. No
   P2P. Pragmatic, low risk, well-known territory. Loses the "no
   third-party dependency" property. Cost: ~1 week.
4. **Use a proven thin-node library**: search for a working BSV-aware P2P
   library in any language (Go btcd, C++ libbitcoin, NBitcoin .NET) and
   either bind to it or port its handshake logic. Likely path to a
   working pure-thin-node, but requires picking a library with active
   BSV support.

### 16.6 What we will NOT do

- **From-scratch Python or C# P2P implementation that mirrors bitcoin-sv
  byte-for-byte**. Spikes D–I demonstrated this is at least multi-week
  reverse-engineering with uncertain outcome. The source code is open
  but the production behaviour of public peers depends on operator
  configurations we cannot inspect.

### 16.7 Decision pending

User asked for a pause to think after Spike I. When resumed:
- if option 1 — produce instrumented tcpdump-both-sides script;
- if option 2 — superseding doc `embedded-bitcoind-broadcaster-design.md`;
- if option 3 — superseding doc `arc-first-broadcaster-design.md`;
- if option 4 — research spike to identify a working library + binding plan.

The spike scripts (`tests/Spikes/P2p/Spike.Handshake/Program.cs` plus the
Python series A–I) remain on disk as evidence and a future implementer's
starting point.
