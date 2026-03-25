# Consigliere VNext Implementation Slices

## Purpose

This document turns the platform decisions in this directory into an execution-safe delivery plan for the new `Consigliere` version.

The plan is intentionally sliced by repository ownership zones rather than by vague feature themes.

This is a `vnext` plan:
- new implementation branch
- performance-first backend
- selective-indexed scope remains the product model
- current API should be preserved where cheap, but not at the expense of correctness, simplicity, or throughput

## Branch

- implementation branch: `codex/consigliere-vnext`

## Delivery Principles

- keep hot ingest cheap
- prefer mirror-write and staged cutover over big-bang replacement
- keep one accountable zone per slice
- require explicit handoff when downstream slices depend on upstream invariants
- benchmark any slice that changes hot-path CPU, allocation rate, storage cost, or replay throughput
- keep Raven as state and read-model storage, not as the long-term hot ingest mutation engine

## Concurrency Cap

To avoid integration debt and context sprawl, vnext execution uses the following concurrency cap:

- at most `1` critical-path slice may be `in_progress`
- at most `2` sidecar slices may run in parallel with the critical path
- at most `3` delegated subagents may be open at the same time

Interpretation:
- the critical path stays local to the operator unless there is a strong reason to delegate it
- sidecar slices should be benchmark, docs, fixture, or bounded adapter work that cannot block the next local step
- if audit remediation is active, it consumes one of the sidecar or critical-path slots rather than being treated as free extra work

## Performance Budget Table

Until `S03` establishes measured baselines, slices must avoid obvious regressions by rule:
- no additional provider call on the known-tx read path
- no unbounded background queue introduction
- no increase in legacy Raven write amplification on the hot path

After `S03`, the following working budgets apply unless a later audit revises them explicitly:

| Metric | Budget | Applies from | Notes |
|---|---|---|---|
| Journal append p95 | no worse than `+20%` from baseline | `S08+` | compare like-for-like replay fixture |
| Journal replay throughput | no worse than `-15%` from baseline | `S08+` | lower is worse |
| Tx lifecycle query p95 | no worse than `+20%` from baseline | `S16+` | measured on same fixture set |
| Revalidation burst throughput | no worse than `-20%` from first accepted token-lineage baseline | `S23+` | lower is worse |
| Raven writes per logical tx | must trend downward by `S30` compared to pre-vnext baseline | `S30` | this is a core migration objective |
| Projection lag recovery after burst | must recover to steady-state within the replay/benchmark scenario target window | `S24+` | exact window recorded in benchmark evidence |

Budgets are stop/go inputs for audit gates rather than decorative metrics.

## Execution Evidence Path

All durable rollout evidence should live under:
- `doc/stream-tasks/consigliere-vnext/`

Recommended structure:
- `doc/stream-tasks/consigliere-vnext/master.md`
- `doc/stream-tasks/consigliere-vnext/audits/`
- `doc/stream-tasks/consigliere-vnext/benchmarks/`
- `doc/stream-tasks/consigliere-vnext/remediation/`

Every significant slice should record:
- commit hashes
- validation commands
- benchmark output locations when relevant
- handoff notes when a downstream slice is blocked on an upstream invariant

## Slice Commit Policy

Commit discipline for vnext:
- one commit should cover one slice or one very narrow milestone inside a slice
- do not batch unrelated zones into one commit
- do not mix remediation work with fresh downstream slice work in one commit
- benchmark-only or docs-only evidence commits are allowed when they close a slice cleanly

When a slice needs multiple commits:
- keep all commits inside the same ownership zone
- record the partial milestone in the execution evidence path
- do not open downstream dependent slices until the slice acceptance criteria are satisfied

## Hard Stop Criteria

Execution must stop and enter an audit/remediation phase immediately if any of the following appears:
- replay nondeterminism
- incorrect readiness or authoritative gating
- revalidation correctness regression
- uncontrolled queue growth
- benchmark regression beyond budget
- API compatibility drift outside the recorded matrix
- AI-first boundary drift causing mixed-responsibility implementation
- rollback path loss during cutover waves

### Autonomous Remediation Rule

Hard stop does not automatically mean user escalation.

If the issue can be fixed without new product decisions, destructive operations, missing credentials, or architectural contradiction, the operator must:
- create a remediation slice
- write a concrete fix plan
- execute the fix locally or with one bounded agent
- validate the fix
- record the outcome in the evidence path

Only then may execution continue.

Escalate to the user only when the remediation requires:
- a new business decision
- an API compatibility exception
- a destructive migration choice
- missing external access or credentials
- a change to already approved architectural constraints

## API Compatibility Matrix

The rollout must maintain a compatibility matrix in the execution evidence path.

Suggested columns:
- `surface`
- `current contract`
- `vnext handling`
- `compatibility goal`
- `change approval required`
- `notes`

Seed entries for the matrix:

| Surface | Current contract | VNext handling | Compatibility goal | Change approval required | Notes |
|---|---|---|---|---|---|
| `GET /api/tx/get/{id}` | return raw tx hex for known tx | preserve route | preserve where cheap | yes, if response shape changes | current known-tx semantics already align |
| `GET /api/tx/batch/get` | batch raw tx hex lookup | preserve route | preserve where cheap | yes | keep current batch ceiling unless justified |
| `GET /api/tx/by-height/get` | assist-style block tx query | preserve or wrap | evolve only if perf/cost justifies | yes | not a core product promise |
| `POST /api/tx/broadcast/{raw}` | broadcast route exists | preserve route, evolve semantics | preserve route, improve lifecycle semantics | yes, if route or body changes | submission, not synchronous final confirmation |
| `GET /api/tx/stas/validate/{id}` | STAS validation route exists | preserve route | preserve and strengthen semantics | yes | align with authoritative B2G model |
| SignalR tx/balance events | existing realtime callbacks | additive-first evolution | preserve where cheap | yes, if event shape breaks consumers | move toward documented lifecycle/readiness model |

No slice is allowed to change a public surface without updating this matrix.

## Cutover Mode Flags

Cutover should be staged through explicit runtime modes rather than hidden behavioral switches.

Minimum mode set:
- `legacy`
- `mirror_write`
- `shadow_read`
- `vnext_default`

Interpretation:
- `legacy`: current production path only
- `mirror_write`: legacy path remains authoritative while observations are also written to journal
- `shadow_read`: vnext projections are computed and compared, but not yet the public default
- `vnext_default`: vnext projections and orchestration become the public default

Slices `S30` to `S32` must not proceed without documenting which mode is active and how rollback returns to the previous stable mode.

## Performance Gates

Every relevant slice should measure at least one of:
- append latency
- replay throughput
- projection lag
- queue depth under burst
- allocations per transaction
- Raven write count per logical transaction
- payload storage growth
- query latency for `tx`, `balances`, `UTXOs`, `history`, or validation views

## Status Vocabulary

- `not_opened`
- `todo`
- `in_progress`
- `blocked`
- `done`

## Slice Ledger

| Slice | Zone | Preferred lane | Status | Depends on | Goal | Validation |
|---|---|---|---|---|---|---|
| `S01` | `repo-governance` | `operator/governance` | `todo` | - | create durable vnext task package and execution ledger | docs review |
| `S02` | `service-bootstrap-and-ops` | `operator/platform` | `todo` | `S01` | introduce `Consigliere:Sources` and `Consigliere:Storage` option models | config binding tests + startup check |
| `S03` | `verification-and-conformance` | `operator/verification` | `todo` | `S01` | create benchmark and replay harness scaffolding | benchmark project runs |
| `S04` | `service-bootstrap-and-ops` | `operator/platform` | `todo` | `S02` | wire new options into DI without changing runtime behavior | startup/build proof |
| `S05` | `indexer-state-and-storage` | `operator/state` | `todo` | `S02` | add raw transaction payload-store abstraction with Raven implementation | storage tests |
| `S06` | `platform-common` | `operator/platform` | `todo` | `S03` | add journal interfaces, append contracts, sequence/checkpoint primitives | unit tests |
| `S07` | `indexer-state-and-storage` | `operator/state` | `todo` | `S05`,`S06` | add Raven-backed observation journal store | append + replay tests |
| `S08` | `verification-and-conformance` | `operator/verification` | `todo` | `S07` | benchmark journal append, replay, and dedupe behavior | benchmark output |
| `S09` | `external-chain-adapters` | `operator/integration` | `todo` | `S02` | define provider capability descriptors and health probes for existing adapters | adapter fixture tests |
| `S10` | `indexer-ingest-orchestration` | `operator/runtime` | `todo` | `S04`,`S09` | add capability routing policy skeleton with preferred mode, fallbacks, verification role | orchestration tests |
| `S11` | `public-api-and-realtime` | `operator/api` | `todo` | `S10` | add provider ops DTOs and `/api/ops/providers` vnext shape | controller contract tests |
| `S12` | `bsv-runtime-ingest` | `operator/runtime` | `todo` | `S07`,`S10` | mirror existing tx observations into the journal from runtime ingest | replay sample + no-regression check |
| `S13` | `indexer-ingest-orchestration` | `operator/runtime` | `todo` | `S12` | mirror block observations and source-visibility semantics into the journal | replay + reorg sample |
| `S14` | `indexer-state-and-storage` | `operator/state` | `todo` | `S12`,`S13` | build tx-lifecycle projection store from journal | projection tests |
| `S15` | `public-api-and-realtime` | `operator/api` | `todo` | `S14` | expose new `tx state` model while keeping current tx hex API compatible | API tests |
| `S16` | `verification-and-conformance` | `operator/verification` | `todo` | `S14`,`S15` | benchmark tx-lifecycle projection and query latency | benchmark output |
| `S17` | `indexer-state-and-storage` | `operator/state` | `todo` | `S02`,`S07` | add tracked address/token state docs and status docs | Raven integration tests |
| `S18` | `indexer-ingest-orchestration` | `operator/runtime` | `todo` | `S17`,`S13` | implement readiness transitions, gap closure, and `live` gating engine | replay + state transition proof |
| `S19` | `public-api-and-realtime` | `operator/api` | `todo` | `S17`,`S18` | add readiness endpoints and enforce not-ready read refusal | controller tests |
| `S20` | `indexer-state-and-storage` | `operator/state` | `todo` | `S07`,`S17` | add direct dependency facts and reverse dependents storage | storage tests |
| `S21` | `bsv-protocol-core` | `operator/protocol` | `todo` | `S20` | extract explicit lineage and validation evaluator inputs out of current implicit store flow | protocol tests |
| `S22` | `indexer-ingest-orchestration` | `operator/runtime` | `todo` | `S20`,`S21` | implement dependency-driven revalidation worker from journal and dependency facts | replay + revalidation tests |
| `S23` | `verification-and-conformance` | `operator/verification` | `todo` | `S22` | run token-lineage and revalidation burst/reorg benchmarks | benchmark output |
| `S24` | `indexer-state-and-storage` | `operator/state` | `todo` | `S18`,`S22` | build address balances and UTXO projections off journal-driven state | query tests + perf sample |
| `S25` | `indexer-state-and-storage` | `operator/state` | `todo` | `S18`,`S22` | build token state, token UTXO, and token history projections off journal-driven state | query tests + perf sample |
| `S26` | `public-api-and-realtime` | `operator/api` | `todo` | `S24`,`S25` | expose vnext address/token/readiness APIs over new projections | controller tests |
| `S27` | `public-api-and-realtime` | `operator/api` | `todo` | `S14`,`S18`,`S22` | align SignalR/realtime contracts with lifecycle and readiness semantics | websocket/manual proof |
| `S28` | `service-bootstrap-and-ops` | `operator/platform` | `todo` | `S10`,`S11`,`S19`,`S26`,`S27` | expose full vnext config examples and operator-facing startup validation | startup/config proof |
| `S29` | `verification-and-conformance` | `operator/verification` | `todo` | `S24`,`S25`,`S26`,`S27` | full replay, burst, soak, and reorg regression suite on vnext pipeline | test and benchmark suite |
| `S30` | `indexer-state-and-storage` | `operator/state` | `todo` | `S24`,`S25`,`S29` | cut over hot-path state writes away from legacy `TransactionStore` fanout model | before/after state diff |
| `S31` | `indexer-ingest-orchestration` | `operator/runtime` | `todo` | `S30` | cut over ingest orchestration to journal-first pipeline | replay proof + rollback note |
| `S32` | `public-api-and-realtime` | `operator/api` | `todo` | `S30`,`S31` | switch public reads and realtime streams to vnext projections by default | API and realtime proof |
| `S33` | `service-bootstrap-and-ops` | `operator/platform` | `todo` | `S32` | final startup, config, deployment, and migration packaging for vnext | startup/build/deploy proof |

## Mandatory Audit Gates

After a significant amount of slice work, execution must stop for an audit before the next wave continues.

Significant means:
- one whole wave completed
- or 5 completed implementation slices since the previous audit gate

Every audit gate is blocking.

The operator must:
- review quality
- review reuse vs duplication
- review adherence to AI-first repository qualities
- review performance evidence and new bottlenecks
- produce a fix plan for all discovered issues
- execute the required fix slices
- record the audit outcome in the task package

Only after the required fixes are integrated may work continue on downstream slices.

### Audit Gate `A1`

Trigger after:
- `S08`

Focus:
- config and benchmark harness quality
- journal abstraction cleanliness
- payload-store abstraction reuse quality

### Audit Gate `A2`

Trigger after:
- `S16`

Focus:
- journal semantics correctness
- tx lifecycle state coherence
- replay determinism
- out-of-order and duplicate handling

### Audit Gate `A3`

Trigger after:
- `S23`

Focus:
- token validation correctness
- dependency graph discipline
- revalidation storm behavior
- protocol/state boundary cleanliness

### Audit Gate `A4`

Trigger after:
- `S29`

Focus:
- business projection correctness
- API compatibility drift
- realtime contract quality
- end-to-end performance envelope

### Audit Gate `A5`

Trigger after:
- `S31`

Focus:
- cutover safety
- rollback viability
- legacy-path retirement risks
- operator ergonomics for vnext startup and troubleshooting

## Audit Outputs

Each audit gate must produce:
- an audit note
- a fix plan
- an explicit list of required remediation slices
- validation evidence

Recommended remediation naming:
- `R01`, `R02`, `R03`, ...

Remediation slices are first-class work.

They may block future slices and must be completed before the next wave starts if they affect:
- correctness
- AI-first boundary quality
- hot-path performance
- API compatibility
- cutover safety

## Wave A: Planning And Perf Foundations

### `S01` `repo-governance`

- Goal:
  - create a durable execution ledger for vnext work
  - keep slices, status, handoffs, and evidence explicit
- Owned paths:
  - `doc/platform-api/**`
  - optional `doc/stream-tasks/**`
- Key outputs:
  - vnext parent task doc
  - slice status tracker
  - handoff references for cross-zone waves
- Done when:
  - every slice in this document has an owner lane, validation target, and dependency order

### `S02` `service-bootstrap-and-ops`

- Goal:
  - introduce new config models for `Consigliere:Sources` and `Consigliere:Storage`
  - do not change runtime routing yet
- Owned paths:
  - `src/Dxs.Consigliere/Configs/**`
  - `src/Dxs.Consigliere/Setup/**`
  - `src/Dxs.Consigliere/appsettings*.json`
- Key outputs:
  - options classes for sources and storage
  - default config binding
  - compatibility notes with existing `AppConfig`
- Done when:
  - app starts with old behavior but new option models are bound and injectable

### `S03` `verification-and-conformance`

- Goal:
  - establish benchmark and replay infrastructure before hot-path changes
- Owned paths:
  - `tests/**`
  - benchmark project paths if added
- Key outputs:
  - benchmark harness
  - replay harness for tx/block observation streams
  - baseline metrics capture scripts or test helpers
- Done when:
  - benchmark project can run append/replay samples and capture baseline throughput

### `S04` `service-bootstrap-and-ops`

- Goal:
  - wire new options models into DI and startup validation
- Owned paths:
  - `src/Dxs.Consigliere/Setup/**`
  - `src/Dxs.Consigliere/Startup.cs`
- Key outputs:
  - DI registrations for new config contracts
  - startup validation for malformed source/storage config
- Done when:
  - startup can reject invalid config deterministically without changing old source behavior

## Wave B: Payload And Journal Foundation

### `S05` `indexer-state-and-storage`

- Goal:
  - add `RawTransactionPayloads` abstraction with first Raven implementation
- Owned paths:
  - `src/Dxs.Consigliere/Data/**`
  - `src/Dxs.Consigliere/Services/Impl/**` only where payload persistence needs adapters
- Key outputs:
  - payload entity or storage contract
  - save/load by tx id or payload reference
  - optional compression support contract
- Done when:
  - raw tx payload can be stored once and loaded independently of the existing `TransactionHexData` model

### `S06` `platform-common`

- Goal:
  - define low-level observation-journal abstractions
- Owned paths:
  - `src/Dxs.Common/**`
- Key outputs:
  - append contract
  - monotonic sequence contract
  - projection checkpoint primitive
  - dedupe fingerprint primitive
- Done when:
  - upstream and downstream zones can use a stable journal interface without Raven-specific coupling

### `S07` `indexer-state-and-storage`

- Goal:
  - implement Raven-backed observation journal
- Owned paths:
  - `src/Dxs.Consigliere/Data/**`
  - `src/Dxs.Consigliere/Extensions/**` if persistence helpers are needed
- Key outputs:
  - journal record model
  - global sequence allocation
  - dedupe enforcement
  - payload reference support
- Done when:
  - observation events can be appended, replayed in sequence order, and deduped idempotently

### `S08` `verification-and-conformance`

- Goal:
  - benchmark the journal itself before any cutover
- Owned paths:
  - `tests/**`
- Key outputs:
  - append throughput benchmark
  - replay throughput benchmark
  - duplicate observation benchmark
- Done when:
  - the team has baseline append/replay cost numbers and can detect regressions in later slices

## Wave C: Source And Routing Control Plane

### `S09` `external-chain-adapters`

- Goal:
  - express existing providers in capability terms
- Owned paths:
  - `src/Dxs.Infrastructure/**`
  - `src/Dxs.Consigliere/Setup/ExternalChainAdaptersSetup.cs`
- Key outputs:
  - provider descriptors
  - health probe surface
  - rate-limit hint extraction
- Done when:
  - current providers can report capability availability and basic health without leaking into business APIs

### `S10` `indexer-ingest-orchestration`

- Goal:
  - introduce capability routing skeleton
- Owned paths:
  - `src/Dxs.Consigliere/Services/Impl/{BitcoindService.cs,JungleBusBlockchainDataProvider.cs,NodeBlockchainDataProvider.cs,NetworkProvider.cs}`
  - `src/Dxs.Consigliere/BackgroundTasks/**` where source selection is applied
- Key outputs:
  - preferred-mode policy
  - capability overrides
  - verification-source role wiring
- Done when:
  - runtime can answer "which source should serve this capability" without yet rewriting all producers

### `S11` `public-api-and-realtime`

- Goal:
  - expose provider ops contract from the new routing model
- Owned paths:
  - `src/Dxs.Consigliere/{Controllers,Dto,Notifications,WebSockets}/**`
- Key outputs:
  - provider ops DTOs
  - provider capability status DTOs
  - `/api/ops/providers` alignment with docs
- Done when:
  - ops API shows provider-first status with nested capability state

## Wave D: Mirror-Write Journal Ingest

### `S12` `bsv-runtime-ingest`

- Goal:
  - mirror current tx observations into the journal
- Owned paths:
  - `src/Dxs.Bsv/BitcoinMonitor/**`
  - any narrow adapters in `src/Dxs.Consigliere` that connect buses to journal writes
- Key outputs:
  - tx observation append on mempool and block detection
  - source identity attached to observations
- Done when:
  - every current tx event written into the old path is also observable in the new journal

### `S13` `indexer-ingest-orchestration`

- Goal:
  - mirror block-connected and block-disconnected semantics into journal events
- Owned paths:
  - `src/Dxs.Consigliere/BackgroundTasks/**`
  - orchestration services that currently reconcile block state
- Key outputs:
  - block observation append path
  - reorg/disconnect observations
  - source-visibility semantics for txs
- Done when:
  - replay of a block/reorg scenario can be driven from journal facts only

## Wave E: Transaction Lifecycle VNext

### `S14` `indexer-state-and-storage`

- Goal:
  - build tx lifecycle projection from journal
- Owned paths:
  - `src/Dxs.Consigliere/Data/**`
  - projection-specific read models and persistence logic
- Key outputs:
  - `known tx` state model
  - lifecycle projection
  - source visibility projection
  - payload availability projection
- Done when:
  - tx lifecycle can be read without relying on the legacy direct-write path

### `S15` `public-api-and-realtime`

- Goal:
  - expose tx lifecycle without breaking current tx hex retrieval
- Owned paths:
  - `src/Dxs.Consigliere/Controllers/TransactionController.cs`
  - `src/Dxs.Consigliere/Dto/**`
  - query services for tx state
- Key outputs:
  - `tx state` endpoint or response extension
  - compatibility layer for current `GET /api/tx/get/{id}`
- Done when:
  - current tx hex clients still work and new tx lifecycle semantics are available

### `S16` `verification-and-conformance`

- Goal:
  - benchmark and verify tx lifecycle path
- Owned paths:
  - `tests/**`
- Key outputs:
  - tx lifecycle replay tests
  - out-of-order and duplicate observation tests
  - tx lifecycle query benchmarks
- Done when:
  - lifecycle projection is deterministic under replay and measured under load

## Wave F: Tracked Scope And Readiness

### `S17` `indexer-state-and-storage`

- Goal:
  - introduce tracked entity docs and internal status docs
- Owned paths:
  - `src/Dxs.Consigliere/Data/**`
- Key outputs:
  - tracked address doc
  - tracked token doc
  - status/progress doc shape
  - deterministic ids and tombstone semantics
- Done when:
  - registration can create both public state doc and internal status doc deterministically

### `S18` `indexer-ingest-orchestration`

- Goal:
  - implement lifecycle transitions to `backfilling`, `catching_up`, `live`, `degraded`
- Owned paths:
  - `src/Dxs.Consigliere/BackgroundTasks/**`
  - runtime orchestration services
- Key outputs:
  - gap-closure logic
  - readiness transition engine
  - degraded transition triggers
- Done when:
  - a tracked object can safely move to `live` only after backfill, realtime attach, and gap closure

### `S19` `public-api-and-realtime`

- Goal:
  - expose readiness and enforce not-ready read denial
- Owned paths:
  - `src/Dxs.Consigliere/{Controllers,Dto,WebSockets}/**`
- Key outputs:
  - readiness endpoint(s)
  - status DTOs
  - gate enforcement on business reads
- Done when:
  - pre-`live` reads return readiness information and refuse authoritative state payloads

## Wave G: Explicit Token Validation Engine

### `S20` `indexer-state-and-storage`

- Goal:
  - add direct dependency fact storage and reverse dependents
- Owned paths:
  - `src/Dxs.Consigliere/Data/**`
- Key outputs:
  - `missingDependencies[]`
  - `dependsOnTxIds[]`
  - reverse dependent lookup
- Done when:
  - lineage change can locate only direct impacted dependents without broad scans

### `S21` `bsv-protocol-core`

- Goal:
  - make lineage and validation evaluation explicit and reusable
- Owned paths:
  - `src/Dxs.Bsv/{Script,Tokens,Transactions}/**`
  - related protocol helpers
- Key outputs:
  - explicit lineage evaluator inputs/outputs
  - reusable B2G validation contract
- Done when:
  - token validation can run outside the legacy implicit `TransactionStore` patch logic

### `S22` `indexer-ingest-orchestration`

- Goal:
  - implement dependency-driven revalidation worker
- Owned paths:
  - `src/Dxs.Consigliere/BackgroundTasks/**`
  - orchestration services coordinating dependency-triggered recompute
- Key outputs:
  - revalidation queue
  - direct-edge cascade engine
  - reorg and missing-dependency resolution hooks
- Done when:
  - lineage changes trigger targeted automatic revalidation without Raven fanout being the primary coordinator

### `S23` `verification-and-conformance`

- Goal:
  - stress test token lineage and revalidation
- Owned paths:
  - `tests/**`
- Key outputs:
  - revalidation storm test
  - reorg lineage test
  - token lineage benchmark
- Done when:
  - the new explicit validation engine has measured worst-case behavior

## Wave H: Core Business Projections

### `S24` `indexer-state-and-storage`

- Goal:
  - build address balances and UTXO projections from the new pipeline
- Owned paths:
  - `src/Dxs.Consigliere/Data/**`
  - address query services where needed
- Key outputs:
  - address balance projection
  - address UTXO projection
  - readiness-aware query contracts
- Done when:
  - address balances and UTXOs can be served from journal-driven state rather than the legacy hot mutation path

### `S25` `indexer-state-and-storage`

- Goal:
  - build token state, token UTXO, and token history projections from the new pipeline
- Owned paths:
  - `src/Dxs.Consigliere/Data/**`
  - token and validation query services where needed
- Key outputs:
  - token state projection
  - token UTXO projection
  - token history projection
- Done when:
  - token reads no longer require legacy implicit store semantics to stay correct

### `S26` `public-api-and-realtime`

- Goal:
  - expose address and token APIs over the new projections
- Owned paths:
  - `src/Dxs.Consigliere/{Controllers,Dto}/**`
- Key outputs:
  - new address/token query surfaces
  - compatibility wrappers where existing routes must be preserved
- Done when:
  - the business-facing API reads from vnext state while keeping the migration surface controlled

### `S27` `public-api-and-realtime`

- Goal:
  - align realtime contracts with tx lifecycle and readiness semantics
- Owned paths:
  - `src/Dxs.Consigliere/{WebSockets,Notifications,Services/Impl/ConnectionManager.cs}`
- Key outputs:
  - non-authoritative pre-`live` stream semantics
  - `scope_status_changed`, `scope_caught_up`, `scope_degraded`
  - `balance_changed` and `token_state_changed` alignment
- Done when:
  - websocket/signalr events match the documented v1 realtime contract

## Wave I: Packaging, Cutover, And Validation

### `S28` `service-bootstrap-and-ops`

- Goal:
  - expose final config examples and startup diagnostics for vnext mode
- Owned paths:
  - `src/Dxs.Consigliere/Setup/**`
  - `src/Dxs.Consigliere/appsettings*.json`
  - `doc/platform-api/**`
- Key outputs:
  - config examples bound to actual implementation
  - startup validation
  - ops guidance for `node`, `hybrid`, and provider-only modes
- Done when:
  - operators can configure and start vnext without reading code

### `S29` `verification-and-conformance`

- Goal:
  - run full-system validation on the vnext pipeline
- Owned paths:
  - `tests/**`
- Key outputs:
  - replay suite
  - burst suite
  - soak suite
  - reorg regression suite
- Done when:
  - vnext has measured correctness and throughput evidence for all critical flows

### `S30` `indexer-state-and-storage`

- Goal:
  - remove legacy hot-path state fanout as the primary write mechanism
- Owned paths:
  - `src/Dxs.Consigliere/Services/Impl/TransactionStore.cs`
  - related storage-layer paths
- Key outputs:
  - legacy write path downgraded or retired
  - vnext state projections are authoritative for core business reads
- Done when:
  - one logical tx no longer requires the old multi-patch Raven hot-path fanout

### `S31` `indexer-ingest-orchestration`

- Goal:
  - switch ingest orchestration to journal-first as the authoritative runtime path
- Owned paths:
  - `src/Dxs.Consigliere/BackgroundTasks/**`
  - runtime orchestration services
- Key outputs:
  - journal-first orchestration flow
  - rollback notes for cutover
- Done when:
  - new observations enter journal first and downstream projections own state application

### `S32` `public-api-and-realtime`

- Goal:
  - switch public reads and streams to vnext projections by default
- Owned paths:
  - `src/Dxs.Consigliere/{Controllers,Dto,WebSockets,Notifications}/**`
- Key outputs:
  - default reads on vnext state
  - default realtime on vnext lifecycle/readiness semantics
- Done when:
  - the public service operates on vnext internals without shadow reads from the legacy path

### `S33` `service-bootstrap-and-ops`

- Goal:
  - final migration packaging for the new service version
- Owned paths:
  - `src/Dxs.Consigliere/Program.cs`
  - `src/Dxs.Consigliere/Startup.cs`
  - `Dockerfile`
  - deployment and docs surfaces
- Key outputs:
  - cutover mode flags if needed
  - migration notes
  - startup/build/deploy proof
- Done when:
  - vnext can be built, started, configured, and rolled out as a coherent service version

## Critical Path

The highest-value dependency spine is:

`S02 -> S04 -> S05 -> S06 -> S07 -> S10 -> S12 -> S13 -> S14 -> S17 -> S18 -> S20 -> S21 -> S22 -> S24 -> S25 -> S26 -> S29 -> S30 -> S31 -> S32 -> S33`

This is the minimum path to get from concept to journal-first business-state cutover.

## Parallelizable Side Work

The main sidecar slices that can run in parallel without blocking the next local critical-path step are:
- `S03`
- `S08`
- `S09`
- `S11`
- `S16`
- `S19`
- `S23`
- `S27`
- `S28`

These slices are ideal candidates for subagent execution once the upstream dependency they rely on is stable.

## Highest-Risk Slices

The slices most likely to surface hidden production complexity are:
- `S07` observation journal persistence
- `S12` and `S13` mirror-write ingestion
- `S18` readiness transition engine
- `S22` dependency-driven revalidation
- `S24` and `S25` business projection cutovers
- `S30` legacy hot-path retirement

These should not be merged without explicit before/after evidence.

## Benchmark-Required Slices

Benchmarks are mandatory for:
- `S03`
- `S08`
- `S16`
- `S23`
- `S24`
- `S25`
- `S29`
- any slice that materially changes append, replay, projection, or validation throughput

## Rollback Discipline

Before `S30`:
- legacy path remains available
- mirror-write and shadow projection comparison are preferred

During `S30` to `S32`:
- keep rollback flags or a documented rollback procedure
- do not cut over both state reads and realtime semantics without fresh replay proof

## Final Definition Of Done

Vnext is done when:
- journal-first ingest is authoritative
- business reads come from vnext projections
- readiness gating is enforced
- token revalidation is explicit and dependency-driven
- provider routing and provider ops surfaces match the documented contracts
- replay, burst, soak, and reorg suites pass with recorded evidence
- the service can run in `node`, `hybrid`, and provider-only modes with documented config
