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

## Decision: Source And Storage Config Stay Separate

`payload store` configuration does not belong under `Consigliere:Sources`.

Instead:
- `Consigliere:Sources` is only for upstream supply configuration
- internal artifact storage should live under a separate `Consigliere:Storage` section

Rationale:
- a payload store is not an upstream source
- mixing internal artifact storage with routing policy would blur architectural boundaries
- the same storage namespace can later host journal, dependency, and archival storage settings

## Decision: `Consigliere:Storage` Uses a General Envelope

`v1` should introduce `Consigliere:Storage` as a general storage envelope rather than a one-off `RawTransactionPayloads` top-level root.

However, the initial populated child in that envelope is intentionally narrow:
- `RawTransactionPayloads`

Rationale:
- keeps the config model extensible without fragmenting it too early
- preserves a clean place for future `Journal`, `Dependencies`, or `Archive` storage settings
- avoids pretending that raw-transaction payload storage is the only internal storage concern the platform will ever have

## Decision: Minimal `RawTransactionPayloads` Config Shape

The minimum configuration shape for `Consigliere:Storage:RawTransactionPayloads` in `v1` is:
- `enabled`
- `provider`
- `location`
- `compression?`

Interpretation:
- `enabled` turns the payload store on or off
- `provider` selects the storage backend implementation
- `location` contains backend-specific placement and connection details
- `compression` is optional and exists for storage-cost control

Explicitly not part of the base `v1` shape:
- `retention`
- `maxInlineBytes`
- encryption-specific knobs
- replication or multipart tuning

## Decision: Minimal `compression` Shape

When present, `compression` uses the following minimum shape in `v1`:
- `enabled`
- `algorithm`

Allowed `algorithm` values:
- `gzip`
- `zstd`

Rationale:
- gives meaningful storage-control choice without exposing low-level tuning knobs
- remains portable across `raven`, `fileSystem`, and `s3` payload-store backends

## Decision: Payload Retention Is Forever in v1

`v1` does not introduce a configurable retention policy for raw transaction payloads.

The default and only supported retention posture is:
- keep raw transaction payloads indefinitely

Rationale:
- it keeps the first payload-store contract simpler
- it matches operator expectations in BSV-heavy environments that prefer full payload preservation
- it avoids introducing premature garbage-collection policy before the write/storage model is implemented end to end

## Decision: Allowed Payload Store Providers in v1

The allowed `provider` values for `Consigliere:Storage:RawTransactionPayloads` are:
- `raven`
- `fileSystem`
- `s3`

Interpretation:
- the configuration contract is intentionally broader than the first implementation target
- initial implementation should assume `raven` first
- `fileSystem` and `s3` remain planned payload-store backends rather than mandatory first-wave runtime work

## Decision: `location` Is Provider-Specific and Non-Discriminated

`location` does not carry its own extra `mode` or `type` discriminator.

Interpretation:
- `provider` is already the discriminator for the payload-store backend
- `location` can therefore use a backend-specific shape without repeating the same type information

Rationale:
- avoids duplicated configuration intent
- keeps the shape smaller
- makes future provider-specific location blocks easier to read

## Decision: Minimal `raven` Payload Location Shape

For `provider = raven`, the minimum `location` shape in `v1` is:
- `database?`
- `collection`

Interpretation:
- `database` is optional and allows a future separate Raven database for payload documents
- `collection` is required and names the document collection that holds raw transaction payloads

Rationale:
- enough for the first implementation target
- avoids overloading the payload-store config with general Raven connection concerns
- keeps collection naming explicit instead of hiding it in code

## Decision: Minimal `fileSystem` Payload Location Shape

For `provider = fileSystem`, the minimum `location` shape in `v1` is:
- `rootPath`
- `shardByTxId?`

Interpretation:
- `rootPath` is the required root directory for raw transaction payload files
- `shardByTxId` is optional and allows directory sharding based on transaction id to avoid dumping all payload files into a single folder

Rationale:
- enough to make file-system storage operationally usable
- avoids introducing file-layout tuning knobs too early
- keeps the first local-storage contract explicit and simple

## Decision: Minimal `s3` Payload Location Shape

For `provider = s3`, the minimum `location` shape in `v1` is:
- `bucket`
- `prefix?`
- `region?`
- `endpoint?`

Interpretation:
- `bucket` is required
- `prefix` is optional and allows isolating payload objects within a shared bucket
- `region` is optional because some S3-compatible systems do not need it explicitly
- `endpoint` is optional so the same shape can work for non-AWS S3-compatible object storage

Rationale:
- enough for a credible object-storage contract
- avoids coupling the location shape to one specific vendor
- keeps credentials and transport policy outside the payload location block

## Decision: Tracked Lifecycle Storage Uses Split Documents

Tracked lifecycle and readiness storage uses a split model:

- public-facing state document
- internal progress/readiness document

Rationale:
- internal sync bookkeeping changes more frequently and more noisily
- public state documents should not be churned by every internal readiness update
- this keeps business-facing state cleaner while preserving detailed operational progress tracking

## Decision: Readiness Storage Uses a Shared Base Plus Typed Payload

The readiness/progress model uses:
- one shared base structure for common readiness semantics
- typed payload for address-specific or token-specific details

Rationale:
- avoids duplicating common lifecycle and readiness fields
- still allows different tracked object types to carry different progress and sync metadata

## Decision: Readiness Exists Both Internally and as a Public Snapshot

The model keeps both:
- internal readiness/progress truth
- public-facing readiness snapshot on state documents

Interpretation:
- the internal readiness document is the operational source of truth
- the public-facing snapshot is a derived cheap-read representation for API use

This preserves fast public reads without making the public state document the canonical source of readiness mechanics.

## Decision: Tracked Lifecycle Documents Use Deterministic IDs

Tracked lifecycle documents use deterministic human-readable ids.

Examples:
- `tracked/address/{address}`
- `tracked/token/{tokenId}`
- `tracked/address/{address}/status`
- `tracked/token/{tokenId}/status`

Rationale:
- easier routing
- easier operations and debugging
- avoids opaque identifiers for naturally keyed tracked objects

## Decision: Registration Creates Both Public and Status Documents

When a tracked entity is registered, the system creates both:
- the public-facing tracked state document
- the internal status/readiness document

Rationale:
- keeps the object visible in the public model immediately
- provides deterministic readiness access from the start
- simplifies orchestration and API consistency

## Decision: Untrack Uses Soft Delete / Tombstone Semantics

Removing a tracked entity uses soft-delete or tombstone semantics rather than immediate hard deletion.

Rationale:
- safer for operations and debugging
- avoids losing lifecycle context too aggressively
- fits tracked entities better as lifecycle-managed objects rather than simple CRUD rows

## Decision: Registration Is Idempotent

Tracked entity registration is idempotent.

If the same address or token is registered again, the platform should treat that as a safe repeat operation rather than a conflict by default.

## Decision: Bulk Registration Is Part of v1

Bulk registration for addresses and tokens is part of the `v1` control plane.

Expected behavior:
- single-item registration and bulk registration coexist
- bulk results should be per-item
- bulk operations inherit idempotent registration semantics

## Decision: Bulk Untrack Is Also Part of v1

Bulk untrack/remove is also part of the `v1` control plane.

Expected behavior:
- bulk untrack results are per-item
- they work with the same lifecycle-managed semantics as single-item untrack

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
