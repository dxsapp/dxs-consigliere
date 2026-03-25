# Implementation Roadmap

## Purpose

This roadmap translates the platform decisions in this directory into an implementation sequence for `Consigliere`.

It is intentionally ordered to avoid mixing public contract work, source-routing work, and storage changes in one uncontrolled stream.

## Guiding Storage Decision

RavenDB remains the primary store for:
- managed state
- readiness state
- read models
- address and token projections
- operational sync state

However, write-pattern rework is a required roadmap item, not an optional optimization.

Reason:
- the current write profile is too chatty for the long-term high-throughput target
- `Consigliere` should keep Raven as state/query storage, but reduce hot-path write amplification

## Decision: Target Write Architecture Uses an Ingest Journal

The target write architecture is:
- append-style ingest journal first
- downstream RavenDB projections second

This is not just a loose ingest/projection split.

It means:
- one canonical durable ingest fact path
- projection workers update Raven read models downstream
- Raven remains the managed-state and read-model store, not the hot ingest mutation engine

Rationale:
- reduces per-transaction write amplification on the hot path
- improves replay and rebuild options
- creates a cleaner foundation for multisource ingest and future direct-to-network sources

## Decision: Projections Are Asynchronous

Read-model and state projections are updated asynchronously from the ingest journal.

This means:
- ingest durability does not wait for all read models to be updated
- projection lag is an expected managed property of the system
- readiness and authoritative-read guarantees are enforced by lifecycle state, not by pretending projections are always instant

## Decision: Automatic STAS/DSTAS Revalidation Must Survive the Rework

The current system automatically revalidates STAS/DSTAS transactions when missing history becomes available or relevant lineage changes.

This behavior is mandatory and must survive the write-pattern redesign.

Implications:
- moving fanout or dependency logic out of Raven indexes is allowed
- losing automatic dependency-driven revalidation is not allowed
- the future architecture must still react when:
  - missing ancestors become available
  - lineage changes
  - reorg or deletion invalidates prior validation conclusions

## Decision: Revalidation Dependency Storage Uses Direct Edges Only

The roadmap does not target a full transitive dependency graph.

Instead, dependency-reactive validation should use only direct-edge storage, such as:
- `missingDependencies[]`
- `dependsOnTxIds[]`

Revalidation then propagates incrementally across direct dependencies.

## Decision: Reverse Dependency Lookup Is Materialized

The roadmap includes a materialized reverse dependency lookup for direct dependents.

This is needed so that lineage changes can trigger targeted revalidation without expensive broad discovery queries.

## Decision: Dependency Storage Is Split from Query Storage

The roadmap should keep:
- canonical dependency facts near the ingest/projection pipeline
- public validation/read state in Raven

This preserves a clean boundary between operational revalidation mechanics and business-facing read models.

## Decision: Ingest Journal Stores Chain Observation Events

The canonical ingest journal unit is a chain observation event.

It is not limited to:
- raw transaction blobs
- bare tx documents

Instead, the journal records observed facts such as:
- source saw a transaction
- source saw a transaction in a block
- source lost visibility
- block-connected or block-disconnected observations

Raw transaction data remains important, but as payload within an observation-oriented ingest model rather than the only canonical journal shape.

## Decision: Minimal Journal Observation Types in v1

The minimum journal observation types are:
- `tx_seen_by_source`
- `tx_seen_in_mempool`
- `tx_seen_in_block`
- `tx_dropped_by_source`
- `block_connected`
- `block_disconnected`

This is enough to build:
- transaction lifecycle
- source visibility
- confirmation state
- reorg handling
- rebroadcast observation logic

## Decision: Journal Uses `eventId` Plus Deterministic Dedupe Fingerprint

Each journal record should carry:
- `eventId`
- a deterministic dedupe fingerprint

Meaning:
- `eventId` uniquely identifies the journal record itself
- the dedupe fingerprint identifies the semantic fact for idempotent projection logic

The dedupe fingerprint must be built from the semantic identity of the observation, not from transport noise.

Examples:
- `tx_seen_by_source` -> `source|tx_seen_by_source|txId`
- `tx_seen_in_mempool` -> `source|tx_seen_in_mempool|txId`
- `tx_seen_in_block` -> `source|tx_seen_in_block|txId|blockHash`
- `tx_dropped_by_source` -> `source|tx_dropped_by_source|txId`
- `block_connected` -> `source|block_connected|blockHash`
- `block_disconnected` -> `source|block_disconnected|blockHash`

## Decision: Journal Uses a Monotonic Sequence

The ingest journal must provide its own monotonic sequence for ordering and projection checkpoints.

This sequence is required because:
- timestamps are not reliable enough for canonical processing order
- multisource ingestion can arrive with skewed observation times
- projection workers need a stable checkpoint primitive

## Decision: Journal Sequence Is Global in v1

`v1` uses one global journal sequence.

Rationale:
- simpler replay semantics
- simpler projection checkpointing
- simpler operational reasoning

Per-partition or per-source sequencing is deferred until there is a proven need for that additional complexity.

## Decision: Raw Transaction Payload Is Stored Once and Referenced

The journal should not duplicate full raw transaction payloads across observation events.

Model:
- raw transaction payload is stored once in a canonical payload store
- tx-related journal events reference that payload when applicable
- non-tx observation events carry no raw-transaction payload

Rationale:
- avoids duplicating very large BSV transactions in the database
- keeps journal records lighter
- preserves one canonical raw-transaction artifact for tx-related processing

## Decision: Payload Storage Is an Abstraction

The canonical payload store is an abstraction, not a hard-coded physical backend.

Expected implementations may include:
- Raven-backed payload storage for small installations
- file or local object storage
- remote object storage such as S3-compatible systems

Rationale:
- large BSV transactions make payload storage strategy a real deployment concern
- different operators will want different cost and storage profiles

## Decision: Payload Store Scope Is Limited to Transaction Payloads in v1

`v1` payload storage scope is limited to raw transaction payloads.

Block payload storage is explicitly deferred to a later phase.

Rationale:
- keeps the first payload abstraction narrow
- avoids expanding implementation scope too early
- focuses on the payload type already known to be operationally important

## Phases

### Phase 1: Contract and Config Scaffolding
- align config model with `providers`, `routing`, and `capabilities`
- add canonical provider sections
- add preferred mode, fallback, verification, and capability override config surface
- keep current runtime behavior as close as possible while introducing the new config shape

### Phase 2: Managed Scope and Readiness Model
- add explicit tracked entity lifecycle storage
- add readiness/status surfaces for tracked addresses and tracked tokens
- enforce the `live` boundary for state endpoints
- allow non-authoritative realtime before `live`

### Phase 3: Transaction Lifecycle and Broadcast Observation
- model tx lifecycle for submitted transactions
- integrate source observation into tx-state transitions
- formalize rebroadcast recovery behavior around long-unconfirmed transactions

### Phase 4: Source Routing Layer
- introduce preferred mode default routing
- add capability-level overrides
- add first-class verification-source handling
- keep provider identity out of business-facing state APIs while exposing it in ops diagnostics

### Phase 5: Write-Pattern Rework Around RavenDB
- reduce write amplification in the ingest path
- reduce per-transaction patch chatter in the hot path
- separate ingest flow from read-model projection responsibilities more cleanly
- preserve RavenDB as the authoritative managed-state/read-model store

### Phase 6: Token and Validation Normalization
- align token state, protocol fields, versioning, and validation semantics with the public contract
- keep tx validation separate from asset-level token state
- preserve Back-to-Genesis as a first-class validation capability

### Phase 7: Ops and Provider Diagnostics
- add provider diagnostics surface
- expose readiness, degradation, and fallback posture operationally
- add metrics needed for SLA/cost control and troubleshooting

## Required Cross-Cutting Constraint

Every phase must preserve the main product promise:
- authoritative indexed state
- stable realtime
- selective managed scope
- no general untracked state reads

## Required Performance Constraint

The roadmap must not treat storage optimization as a nice-to-have.

Write-path rework is mandatory before claiming the platform is ready for serious high-load business usage.
