# Source Capability Matrix

## Purpose

`Consigliere` consumes multiple upstream BSV sources to maintain its own API SLA and cost profile.

These sources are supply inputs, not public API contracts.

The platform must route by capability, health, and economics rather than binding core logic to a single provider.

## Source Roles

A source may be assigned one or more internal roles:
- `authoritative`
- `fast`
- `cheap`
- `fallback`
- `verification`
- `degraded_only`

Roles are capability-specific, not source-global.

## Capability Set

Core internal capabilities:
- raw transaction fetch
- broadcast transaction
- block tip and chain status
- block crawl / historical ingest
- mempool observation
- transaction confirmation tracking
- address history lookup
- UTXO lookup
- spend lookup
- websocket or streaming event delivery

## Candidate Sources

### Node
Strengths:
- strongest local authority when available
- native RPC plus ZMQ
- good for verification and final state confirmation

Weaknesses:
- infrastructure cost
- not every business wants to run one
- operational burden

Likely internal roles:
- authoritative
- verification
- broadcast
- confirmation

### JungleBus
Strengths:
- useful streaming and historical ingestion model
- good fit for selective ingest and event flow
- can reduce need for self-hosted node in many scenarios

Weaknesses:
- provider-specific availability and subscription constraints
- external dependency for uptime and semantics

Likely internal roles:
- fast
- block crawl
- mempool or near-realtime ingest
- fallback for no-node mode

### Bitails
Strengths:
- useful REST capability set
- practical lookup source in provider-only or hybrid modes

Weaknesses:
- rate limits and provider constraints
- not ideal as the only truth source for every workload

Likely internal roles:
- cheap or assist lookup
- fallback read path
- selective supplemental fetch

### WhatsOnChain
Strengths:
- broad ecosystem familiarity
- can be useful as an auxiliary assist source

Weaknesses:
- not the preferred core product dependency for authoritative managed state
- semantics and cost profile must be treated as provider-specific

Likely internal roles:
- optional assist lookup
- degraded fallback

## Routing Principles

1. Route by capability, not by source brand.
2. Prefer the cheapest source that still satisfies the required consistency class.
3. Preserve a separate verification path for high-confidence state transitions.
4. Separate hot realtime paths from backfill and historical read paths.
5. Protect upstream quotas with capability-aware budgeting and backpressure.

## Initial Role Matrix

| Capability | Preferred role | Fallback role | Notes |
|---|---|---|---|
| raw tx fetch | fast or cheap provider | node or secondary provider | depends on latency/cost budget |
| broadcast | node when present | provider fallback if supported | higher confidence path preferred |
| block crawl | JungleBus or node | secondary provider | must support backfill well |
| mempool observation | JungleBus or node/ZMQ | degraded provider mode | realtime-critical |
| confirmation tracking | node or verification source | provider read fallback | should not rely on weakest source only |
| address history | indexed internal state first | assist provider for untracked reads | tracked scope stays internal |
| UTXO lookup | indexed internal state first | provider lookup for assist mode | authoritative for tracked scope must come from index |
| token state | internal index only | no provider passthrough promise | normalized domain logic |
| websocket events | internal only | none | public realtime is a Consigliere contract |

## Economics Model

Each capability should eventually have policy inputs for:
- cost per request or batch
- latency target
- rate-limit budget
- failure rate
- freshness requirement
- verification requirement

This allows operating modes such as:
- `no_node`
- `hybrid`
- `provider_only_high_availability`
- `verification_heavy`

## Product Constraint

No public endpoint should expose raw provider identity as part of its contract.

Provider choice is an internal routing concern unless explicitly surfaced in ops diagnostics.
