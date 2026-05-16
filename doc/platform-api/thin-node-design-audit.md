# Thin-Node Design Audit
Reviewer: GPT-5 Codex
Date: 2026-05-14
Verdict: MAJOR REVISION REQUIRED

## Executive summary

The design is not ready for implementation commitment. The narrow `version` payload conclusion is mostly defensible: association ID is optional in current SV-Node parsing, Teranode only sends it under multistream/block-priority conditions, and `bsv-p2p` omits it. The larger design is still too optimistic about peer access, relay reach, Phase 1 scope, and production safety. The biggest issue is that the document labels the work "Approved for Phase 1 implementation" while moving reject classification, peer scoring, rate limiting, metrics, and lifecycle reconciliation out of Phase 1.

## Critical findings (must address before any code is written)

### C1. The design promotes an unproven network-access assumption to an approved architecture

**Where:** `consigliere-thin-node-design.md` lines 3, 48-70, 589-607.

**Evidence:** The spike history is materially weaker than the design summary. The old spike document says D-I reached `0/14` locally after a source-derived handshake and concludes that a from-scratch C#/Python mirror is "at least multi-week reverse-engineering with uncertain outcome" (`p2p-broadcaster-design.md` lines 562-568, 672-676). The new design extrapolates from Spike J/K to "Protocol works" and expected `4-8` steady-state peers (`consigliere-thin-node-design.md` lines 48-51, 589-592) without publishing the acceptance distribution, peer operators, elapsed soak time, or a successful tx relay.

**Why it matters:** A broadcaster's real metric is not "one handshake succeeded"; it is "submitted tx reaches miners reliably under normal and degraded peer conditions." The design still has no acceptance threshold for first-run success, no relay-to-miner proof, and no criterion for aborting back to ARC/RPC.

**Recommendation:** Change status to draft. Add a pre-implementation gate: from a fresh IP, sustain at least 8 outbound peers for 24h, broadcast at least N real low-risk txs, observe independent mempool sighting and mining, and define PASS/PIVOT/ABORT thresholds. Do not start production implementation until that evidence is attached.

### C2. Phase 1 exposes a broadcast surface before basic abuse, validity, and terminal-failure controls exist

**Where:** `consigliere-thin-node-design.md` lines 533-560, 563-568, 475-485.

**Evidence:** Phase 1 includes `WalletHub.BroadcastTracked`, `TxRelayCoordinator`, and a retrying monitor, but moves reject classification to Phase 2 and caller rate limiting to Phase 3 (`consigliere-thin-node-design.md` lines 547-560, 565-568). Existing `BroadcastService` only parses the transaction and has a small string-based permanent failure check for `missing-inputs` and `mandatory-script-verify-flag-failed` (`BroadcastService.cs` lines 42-45, 251-255).

**Why it matters:** Without local size/fee/policy checks, reject classification, and client throttles, a SignalR client can make the service announce garbage or oversized txs to all peers repeatedly. That is how the process earns bans, not just failed user requests.

**Recommendation:** Move these into Phase 1: per-client rate limits, max raw hex size, transaction parse and txid dedup, configurable fee/size policy floor, reject-code classification, and a terminal-failure quorum. `BroadcastTracked` should not be available until these controls exist.

### C3. Outbound message sizing is under-specified and can produce protocol violations for BSV-scale txs

**Where:** `consigliere-thin-node-design.md` lines 192, 217-231, 204, 356-360.

**Evidence:** The design says `tx` is "raw transaction bytes" and all messages use the standard 24-byte header (`consigliere-thin-node-design.md` lines 219-231). SV-Node has a legacy protocol payload default of 1 MB and raises limits through `protoconf` (`protocol.h` lines 35-40; `net_processing.cpp` lines 4387-4400). It also has an extended header path where payloads over `uint32_t` use command `extmsg`, zero checksum, extended command, and 64-bit length (`protocol.h` lines 81-84; `protocol.cpp` lines 221-238, 304-321). The working `bsv-p2p` client enables extmsg when peer version is at least 70016 (`bsv-p2p/src/index.ts` line 261).

**Why it matters:** BSV post-Genesis allows large transactions. Sending a 50 MB transaction to a peer that advertised 2 MB, or encoding a future oversized payload with the basic header, is not a benign failure. It can disconnect us and damage peer reputation.

**Recommendation:** Define outbound sizing before implementation: parse and store peer `protoconf.maxRecvPayloadLength`; refuse or hold txs exceeding every connected peer's limit; implement extmsg framing even if Phase 1 caps tx size below 4 GB; and respect `feefilter` before announcing to a peer.

## High-severity findings

### H1. `version` without association ID is safe today, but the long-term claim is too strong

**Where:** `consigliere-thin-node-design.md` lines 53-70 and 599-602.

**Evidence:** SV-Node only reads `LIMITED_BYTE_VEC(associationID)` if bytes remain after `relay` (`net_processing.cpp` lines 1757-1794), so omission is accepted by current parsing. Teranode includes `AssociationID` only when `AllowBlockPriority` is enabled and the ID is non-empty (`teranode/services/legacy/peer/peer.go` lines 2278-2283). `bsv-p2p` writes version through `relay` and stops (`bsv-p2p/src/messages/version.ts` lines 98-111). However, SV-Node's double-spend code treats missing association IDs as a degraded identity signal, falling back to pointer comparison (`net_processing.cpp` lines 4463-4473).

**Why it matters:** The omission is not a correctness bug for Phase 1. It is a compatibility risk if Teranode/multistream policy becomes more important, especially around stream association and DS detection.

**Recommendation:** Keep omission as the Phase 1 default, but implement tolerant parsing of optional association IDs and document a future `AssociationIdMode = None|Echo|Generate` config. Do not claim it is permanently unnecessary.

### H2. We receive `protoconf`, but the design never sends it

**Where:** `consigliere-thin-node-design.md` lines 186-215.

**Evidence:** SV-Node sends `verack` and immediately `protoconf` after processing version (`net_processing.cpp` lines 1821-1824). Teranode also sends `verack` and queues `protoconf` after negotiation (`teranode/services/legacy/peer/peer.go` lines 2482-2489). The design's send list includes `version`, `verack`, `pong`, `inv`, `tx`, `notfound`, `headers`, and `addr`, but not `protoconf` (`consigliere-thin-node-design.md` lines 208-215).

**Why it matters:** It may not be strictly required by spec, but it makes us less node-like and caps what peers can safely send us. The spike history explicitly suspected "correct authch, sendheaders, protoconf timing" as a reputation signal (`p2p-broadcaster-design.md` lines 608-612).

**Recommendation:** Send `protoconf` after `verack` with a conservative receive payload limit and supported stream policy string. Test both directions against SV-Node and Teranode.

### H3. Cold-start fanout is likely to recreate the ban problem

**Where:** `consigliere-thin-node-design.md` lines 248-255 and 603-607.

**Evidence:** The spike history says 14+ rapid connections in 30 seconds may have triggered rate limiting (`p2p-broadcaster-design.md` lines 613-615). The new design proposes 32 candidate handshakes in parallel (`consigliere-thin-node-design.md` line 253).

**Why it matters:** From a public peer's perspective, this looks like scanning or flapping from an unknown IP. If we burn the first-run IP reputation, the open-source UX becomes worse than an HTTP fallback.

**Recommendation:** Replace 32-way fanout with a rate-limited bootstrap: small concurrency, jitter, per-/24 diversity, cooldown on silent disconnect, and persisted negative results. Add a first-run diagnostic that reports acceptance without continuing to hammer peers.

### H4. The lifecycle cannot distinguish propagation delay, eviction, conflict, and permanent invalidity

**Where:** `consigliere-thin-node-design.md` lines 291-360 and 475-485.

**Evidence:** The state machine has no transition for mempool eviction, fee rejection, conflicting reject reasons, or tx mined while observers were disconnected. SV-Node tracks recent rejects and non-final recently removed txs as "already known" (`net_processing.cpp` lines 929-955), so blindly re-announcing the same bytes can stop being useful. Reject parsing includes message, code, reason, and tx hash (`net_processing.cpp` lines 1475-1497), but classification is postponed to Phase 2 (`consigliere-thin-node-design.md` line 560).

**Why it matters:** The proposed `PeerAcked` and `MempoolSeen` states are not stable truth. A low-fee tx can disappear, a double spend can produce peer-specific reasons, and an observer outage can leave the tx stuck forever.

**Recommendation:** Add explicit states or substates for `PolicyRejected`, `ConflictRejected`, `EvictedOrDropped`, and `ObserverUnknown`. Phase 1 must include reject classification and reconciliation against block/mempool providers.

### H5. `Broadcast` and `OutgoingTransaction` split creates two audit models with different semantics

**Where:** `consigliere-thin-node-design.md` lines 410-420.

**Evidence:** The existing `Broadcast` document is already the audit record for provider attempts, with `TxId`, `Success`, `Code`, `Message`, `BatchId`, and `Attempts` (`Broadcast.cs` lines 3-14, 56-62). Existing `BroadcastService` persists `Broadcast`, runs configured providers, and marks UTXOs used only after success (`BroadcastService.cs` lines 54-83, 64-70). The new design says `Broadcast` is "Not used by the new P2P flow" and `WalletHub.Broadcast` returns true if persistence succeeded (`consigliere-thin-node-design.md` lines 412-420).

**Why it matters:** That is a semantic regression. The same method name would change from "some provider accepted" to "we wrote a Raven document." Operations would have two records for one business event.

**Recommendation:** Make `OutgoingTransaction` the lifecycle aggregate and either embed provider/peer attempts there or migrate `Broadcast` into a compatibility projection. Do not preserve `Broadcast(hex) -> bool` as a fake success signal.

### H6. Phase 1 is not credible in two weeks

**Where:** `consigliere-thin-node-design.md` lines 533-549.

**Evidence:** Phase 1 contains a P2P codec for 12 message types, TCP session lifecycle, DNS discovery, peer manager, Raven schema/index/store, relay coordinator, service integration, monitor, SignalR contract, and real network integration test. The repo has low-level `BitcoinStreamReader` and `BufferWriter`, but they are synchronous primitives with no network framing, backpressure, partial-read handling, or P2P header parser (`BitcoinStreamReader.cs` lines 44-68; `BufferWriter.cs` lines 91-134). Repo instructions also state no test framework was found in this repository (`doc/AGENTS.md` section 9).

**Why it matters:** Time pressure will push protocol correctness and failure handling into production discovery.

**Recommendation:** Split Phase 1 into a conformance spike and a disabled internal MVP. Public API, retry monitor, and lifecycle UI events come only after codec soak tests and peer behavior evidence.

## Medium-severity findings

### M1. `getheaders` handling is overconfident

**Where:** `consigliere-thin-node-design.md` lines 90-97, 196-197, 233-245.

**Evidence:** SV-Node does reply empty headers when its best-known header matches the peer, but it also parses locators and silently ignores unknown requests (`net_processing.cpp` lines 2990-3012, 3046-3051). The design says empty or silent responses are "free" without specifying valid request parsing.

**Recommendation:** Implement proper `getheaders`/`getblocks` decoding and return empty only after parsing succeeds. Malformed payloads should be counted as peer errors, not swallowed.

### M2. Outbound-only reach is hand-waved

**Where:** `consigliere-thin-node-design.md` lines 282-289, 583-585, 594-598.

**Evidence:** The doc says inbound is a Phase 2 bonus and "outbound-only is functionally complete." It also admits peer reach is bounded by pool count and may need known mining pool IPs.

**Recommendation:** Treat outbound-only as a measurable degraded mode. Phase 1 should report peer diversity, ASN/operator concentration, relay-back count, and time-to-independent-mempool sighting.

### M3. Disaster recovery is not defined

**Where:** `consigliere-thin-node-design.md` lines 362-390, 475-485.

**Evidence:** Recovery only covers process restart with intact RavenDB. If RavenDB is wiped, in-flight raw hex and attempts are lost.

**Recommendation:** State the recovery contract: either in-flight broadcasts are best-effort and lost on DB loss, or raw submissions are journaled elsewhere and replayable.

### M4. SignalR broadcast event scoping is inconsistent

**Where:** `consigliere-thin-node-design.md` lines 435-439 and 580-582.

**Evidence:** Section 9.3 says without subscription, the hub emits all transitions to the connection. Section 14.1 says Phase 1 defaults to per-txid subscription.

**Recommendation:** Pick one. The safer default is per-txid subscription plus authorization/rate checks before group join.

## Low-severity / nits

### L1. The default user agent should not impersonate SV-Node

**Where:** `consigliere-thin-node-design.md` lines 63-68, 464.

**Evidence:** Spike history lists UA-based filters as a plausible failure cause (`p2p-broadcaster-design.md` lines 605-612).

**Recommendation:** Use an honest UA such as `/ConsigliereThinNode:0.1.0/` after testing whether peers tolerate it. If impersonation is required to connect, that is a design risk, not a config detail.

### L2. Component paths do not fit the zone catalog cleanly

**Where:** `consigliere-thin-node-design.md` lines 170-182.

**Evidence:** `src/Dxs.Bsv/P2p/**` is not in the current zone catalog. Protocol codecs belong in `bsv-protocol-core`; TCP sessions and peer runtime behavior look closer to `bsv-runtime-ingest` or a new zone.

**Recommendation:** Update repository zones before implementation, or place files under existing owned paths with explicit handoff contracts.

## What the design got right

The corrected `version` payload is the strongest part of the document. Current SV-Node parsing makes the association ID tail optional, Teranode conditionally includes it, and `bsv-p2p` works without it. The design should preserve that conclusion, but narrow the claim to current compatibility rather than future protocol policy.

The decision to keep this as a long-lived peer with ping/pong and `inv -> getdata -> tx` relay is directionally correct. Serving tx bytes only after `getdata` matches normal inventory relay and avoids blasting large payloads to peers that did not ask.

Persisting a lifecycle record is also the right product shape. Users need something better than a boolean broadcast response. The issue is not the lifecycle concept; it is that the proposed Phase 1 lifecycle lacks enough signals to make its states trustworthy.

## Recommended changes to Phase 1 scope

Replace the current two-week Phase 1 with three gates:

1. **Conformance gate:** codec, headers including extmsg, `version` with optional association ID parsing, `protoconf`, `feefilter`, `reject`, and byte-vector tests against captured SV-Node/Teranode/bsv-p2p frames.
2. **Network gate:** disabled-by-default peer manager with rate-limited bootstrap, 24h soak, peer diversity metrics, relay-back observation, and documented PASS/PIVOT/ABORT thresholds.
3. **Internal MVP gate:** `BroadcastTracked` behind config, local validation, rate limiting, reject classification, idempotent `OutgoingTransaction`, and reconciliation against observer/provider state.

Defer inbound listener, admin UI, known mining-pool peering, and full scoring. Do not defer reject classification, rate limiting, peer payload limits, or basic metrics.

## Questions you couldn't answer from the doc

- What were the exact Spike J/K peer counts, operators, duration, and tx relay results?
- What tx size range must opensource users broadcast in the first release?
- What fee policy should Consigliere enforce before announcing?
- Is ARC/RPC fallback truly rejected for all deployments, or only for default open-source mode?
- Who owns peer IP sourcing, and are known mining-pool peers legally/operationally acceptable to hardcode?
- What is the intended client authorization model for `BroadcastTracked`?
