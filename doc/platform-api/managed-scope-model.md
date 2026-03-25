# Managed Scope Model

## Why Managed Scope Exists

`Consigliere` is designed to be a reliable and cost-efficient BSV backend without requiring every business to run its own full node.

That only works if indexing is selective.

The service does not promise full-network indexing. It promises high-quality state and realtime for explicitly managed scope.

## Scope Objects

## Decision: v1 Primary Managed Units

For `v1`, the only primary public managed units are:
- `tracked_address`
- `tracked_token`

Explicitly not primary public managed units in `v1`:
- `tracked_tx`
- named scope groups
- policy-defined graph scopes

Rationale:
- aligns with wallet, trading, payment, and infra backend workloads
- keeps control-plane API small and predictable
- keeps selective indexing cost easier to reason about
- avoids turning `tx` into a long-lived management object instead of a derived state transition object

In `v1`, transactions remain:
- queryable
- indexable as derived scope
- available for lifecycle and validation APIs

But transactions are **not** first-class public registration units.

### Tracked address
A BSV address explicitly registered for indexing, realtime, and derived state maintenance.

### Tracked token
A token identifier explicitly registered for indexing, realtime, and derived state maintenance.

## Decision: Token Identity

For `v1`, the canonical identity of a tracked token is **only** `tokenId`.

Interpretation:
- `tokenId` is the platform key for registration, storage, lookup, routing, and public API addressing
- operationally, this is treated as the issuance-side token address identity

Explicitly out of scope for the base platform identity model:
- redeem-hash-based alternate identity keys
- composite protocol-plus-token keys
- lineage-root identity objects
- semantic token subtypes derived from the same issuance address

Rationale:
- keeps the platform contract simple and stable
- avoids leaking business-specific token semantics into the base indexing model
- makes storage and API routing deterministic

Documentation requirement:
- platform docs must state clearly that users should not create semantically different tokens from the same issuance address if they expect `Consigliere` to treat them as separate platform-level assets

### Derived scope
Transactions, UTXOs, balances, token state, and history that affect tracked addresses or tracked tokens.

### Assist lookup
A non-authoritative query outside tracked scope. These are allowed as helper operations but are not the core product promise.

## Tracking Modes

- `continuous`: maintain live state and realtime continuously
- `bootstrap_once`: backfill once for readiness or migration workflows
- `address_only`: address state without token-focused expansion
- `token_only`: token-focused state without arbitrary address expansion
- `address_and_token`: both dimensions maintained together

## Lifecycle States

- `registered`: accepted but not yet scheduled
- `backfilling`: historical state is being collected
- `catching_up`: historical coverage exists but live tip is not yet reached
- `live`: authoritative tracked state is available
- `degraded`: service still answers but guarantees are reduced
- `paused`: intentionally not advancing
- `failed`: tracking flow cannot continue without intervention

## Decision: Keep `catching_up` as a Separate Lifecycle State

`catching_up` remains a first-class lifecycle state in `v1`.

It is intentionally separate from `backfilling` because it expresses a different operational condition:
- historical coverage is largely or fully present
- realtime attachment or final tip alignment is still being finalized
- authoritative state guarantee is not available yet

Rationale:
- the extra verbosity is useful
- operators and clients can distinguish deep historical sync from final readiness alignment
- it makes readiness and lag semantics easier to reason about

## Completeness Model

Completeness values exposed to clients:
- `authoritative`
- `catching_up`
- `best_effort`
- `not_tracked`

Rules:
- `authoritative` is allowed only when lifecycle is `live`
- `catching_up` is allowed during `backfilling` and `catching_up`
- `best_effort` is allowed during `degraded`
- `not_tracked` is for unregistered scope

## Authoritative State Rules

For a tracked entity in `live`:
- balances must reflect indexed ground truth
- UTXO queries must be consistent with internal indexed state
- token state and STAS/DSTAS classification must come from normalized internal rules
- realtime events must be aligned with the indexed state model

For a tracked entity not yet in `live`:
- the API may answer
- the response must expose lifecycle and lag
- the client must never infer full correctness from that response

## Scope Expansion Rules

The default index should remain strict and selective.

## Decision: v1 Expansion Boundary

For `v1`, managed scope expansion is asymmetric:

- `tracked_address` uses **strict direct-touch only**
- `tracked_token` uses **full reverse token lineage expansion**

Default indexed scope includes only:
- transactions directly touching tracked addresses
- all parent transactions, at any generation depth, that are required to unwind a tracked token back through its token lineage
- derived balances, UTXOs, and history for that managed scope

This means:
- no generic ancestry crawling for ordinary address-driven wallet, trading, payment, or infra state
- no arbitrary local graph expansion
- no automatic descendant expansion unless the descendant itself directly touches managed scope

Rationale:
- AML-style historical graph analysis is out of scope
- payment and business state do not require full predecessor history by default
- selective direct-touch indexing keeps the cost model predictable
- token correctness requires lineage awareness in the reverse direction
- in the common case reverse token unwinding is effectively cheap, while worst-case cost is concentrated in initial token syncs and pathological reorg scenarios

## Token Reverse Lineage Rule

For `tracked_token`, `Consigliere` must unwind the token backwards through every parent generation participating in that token's history.

In practical terms:
- any parent transaction in any generation that carries the tracked token is part of managed scope
- reverse unwinding continues until valid issuance is reached
- this rule is token-specific and does not imply full graph exploration around unrelated addresses or non-token branches

This rule is expected to be:
- near-constant cost in the common case after sync warm-up
- expensive mainly during initial token sync
- expensive during catastrophic or unusual reorg recovery

## Exception: STAS / DSTAS Validation Lineage

There is one explicit validation exception beyond ordinary indexed state.

For `(D)STAS` transaction validation, `Consigliere` may need the full previous `(D)STAS` lineage back to a valid issuance point in order to resolve Back-to-Genesis validation correctly.

That lineage rule applies only to:
- validation flows
- token conformance checks
- explicit token-state correctness paths that require Back-to-Genesis proof

It does **not** turn the whole index into a generic ancestry crawler for every tracked entity.

Disallowed expansion as a default behavior:
- indiscriminate full-address graph crawling
- full-network token holder derivation
- explorer-style indexing of unrelated chain activity

## Readiness Signals

Each tracked entity should expose:
- lifecycle state
- latest indexed height
- lag in blocks
- lag in seconds when available
- degradation marker if upstream routing is operating below normal quality

## Decision: Readiness Boundary for `live`

A tracked address or tracked token may be marked `live` only when all of the following are true:

1. historical backfill required for that managed entity is complete
2. realtime ingestion for that entity is already attached
3. the gap between backfill and realtime has been closed
4. current indexed state is safe to treat as authoritative

This rule is strict because partial history can produce false free-state conclusions, including:
- an apparently unspent UTXO that is actually already spent
- incomplete history views
- incorrect token state at the moment the client starts trusting the object

Operationally:
- `live` means safe for authoritative balance, UTXO, and state reads
- anything earlier must remain in a non-authoritative lifecycle state

## Decision: Readiness Is Both Discoverable and Enforced

Readiness must exist in two forms:

1. discoverable readiness metadata and status endpoints
2. hard enforcement on state endpoints

Implication:
- clients can inspect readiness explicitly
- state endpoints must still refuse premature reads rather than relying on the client to behave perfectly

## Decision: Minimal Readiness Metadata for v1

The minimum readiness metadata object includes:
- `tracked`
- `entityType`
- `entityId`
- `lifecycleStatus`
- `readable`
- `authoritative`
- `degraded`
- `lagBlocks?`
- `progress?`

Optional fields remain optional because readiness detail depends on source capability and sync strategy.

## Decision: No State Reads Before `live`

Tracked entities in `backfilling` or `catching_up` must not expose state reads.

Before `live`, the system may expose only readiness metadata, such as:
- lifecycle state
- lag
- backfill progress
- degradation status

It must not expose state answers such as:
- balances
- UTXOs
- authoritative history
- token state

Reason:
- before readiness is complete, any of these may still be false
- the platform contract prefers no answer over a potentially misleading answer

## Decision: Progress Metadata Is Capability-Dependent

`Consigliere` does not guarantee one universal progress metric for `backfilling` or `catching_up`.

Progress depends on:
- available upstream APIs
- the chosen sync strategy
- whether lag can be measured reliably for the current tracked object

Implications:
- `status` is mandatory
- readiness metadata is returned when it can be computed honestly
- progress percentage is optional, not guaranteed
- different tracking flows may expose different progress detail levels

The platform must prefer:
- no progress number
over
- a misleading progress number

## Realtime Contract

Realtime is defined for managed scope only.

The system should emit events when tracked scope changes state, including:
- `scope_status_changed`
- `scope_caught_up`
- `scope_degraded`
- `tx_seen`
- `tx_confirmed`
- `tx_reorged`
- `tx_dropped`
- `balance_changed`
- `token_state_changed`

`balance_changed` is first-class rather than merely derived convenience metadata.
`token_state_changed` is also first-class rather than merely derived convenience metadata.

## Decision: Pre-`live` Realtime Is Allowed but Non-Authoritative

Tracked objects may start producing realtime events before they become `live`.

However:
- that realtime stream must be explicitly treated as non-authoritative
- event flow before readiness is a liveness and early-signal channel, not a final state guarantee

This allows clients to see that the system is alive and advancing without confusing early events with authoritative indexed truth.

## Decision: Minimal Realtime Metadata Shape for v1

The minimum realtime event metadata shape includes:
- `eventId`
- `eventType`
- `entityType`
- `entityId`
- `txId?`
- `blockHeight?`
- `timestamp`
- `authoritative`
- `lifecycleStatus`
- `payload`

Allowed realtime `entityType` values in `v1`:
- `address`
- `token`
- `scope`
- `transaction`

## Product Invariants

1. Managed scope is the core unit of promise.
2. Authoritative state is only promised for managed scope in `live`.
3. Cost efficiency depends on keeping scope selective.
4. Public API must tell clients whether scope is tracked and whether it is ready.
5. Internal provider routing must never downgrade a response silently.
6. Untracked entities do not receive general state answers in `v1`.
7. A known transaction may still be returned as a known blockchain object, but only with data already present in `Consigliere`.
8. "Known transaction" means present in local persisted store, not merely reachable from an upstream provider.

## Decision: Transaction Lifecycle Must Cover Broadcast Observation

The `v1` transaction lifecycle must be able to represent:
- broadcast submission
- observation by sources
- mempool visibility
- confirmation
- drop or loss of visibility

## Decision: Transaction State Must Also Express Managed Relevance

The `v1` transaction-state model includes:
- lifecycle
- visibility
- managed-scope relevance

## Decision: Minimal Transaction-State Fields in v1

The minimum transaction-state fields are:
- `txId`
- `known`
- `lifecycleStatus`
- `authoritative`
- `relevantToManagedScope`
- `relevanceTypes[]`
- `seenBySources[]?`
- `seenInMempool?`
- `blockHash?`
- `blockHeight?`
- `firstSeenAt?`
- `lastObservedAt?`
- `validationStatus?`
- `payloadAvailable`

## Decision: Address State Core for v1

The mandatory address state core consists of:
- readiness/status
- balances
- UTXOs
- history

## Decision: Token State Core for v1

The mandatory token state core consists of:
- readiness/status
- token state
- balances
- UTXOs
- history
- Back-to-Genesis token transaction validation

`holders` is derived knowledge, not token-core minimum surface.

## Decision: Token Validation Shape in v1

Token validation in `v1` separates:
- issuance knowledge
- validation verdict

Minimum validation semantics:
- `issuanceKnown: bool`
- `validationStatus: unknown | valid | invalid`

Token validation remains split across:
- asset-level token state semantics
- transaction-level validation endpoints

## Decision: Validation Must Be Dependency-Reactive

STAS/DSTAS validation in the platform model is dependency-reactive.

Meaning:
- validation results are not static once computed
- if missing lineage later appears, or if relevant lineage changes, affected validation results must be recomputed automatically

## Decision: Validation Dependency Model Stores Only Direct Edges

The validation dependency model stores only direct dependency edges.

Minimum expected fields:
- `missingDependencies[]`
- `dependsOnTxIds[]`

These fields describe only direct parents or direct missing relatives relevant to the current transaction.

Explicitly not stored by default:
- full transitive dependency closure
- fully expanded lineage graph for every transaction

Rationale:
- avoids graph blow-up
- keeps dependency storage bounded
- still allows revalidation to propagate incrementally through the chain of direct relationships

## Decision: Reverse Dependents Must Be Materialized

The platform should maintain a materialized direct reverse lookup:
- `txId -> direct dependents[]`

Rationale:
- avoids expensive dependent-discovery queries on every lineage change
- supports fast dependency-reactive revalidation
- remains bounded because only direct edges are stored

## Decision: Dependency Model Uses Split Placement

The dependency model uses split placement:

- canonical dependency facts live alongside the ingest/projection pipeline
- query-facing validation state lives in Raven read models

Rationale:
- keeps operational dependency mechanics separate from public query storage
- avoids overloading Raven with every internal revalidation concern
- preserves Raven as the external state/read-model surface

## Decision: Token State Must Expose `protocolType`

The public token state contract in `v1` includes `protocolType`.

This is required so clients can distinguish:
- `stas`
- `dstas`
- future token protocol families

It also creates a natural path for later protocol-version exposure.

## Decision: Token State Must Expose `protocolVersion`

The public token state contract in `v1` also includes `protocolVersion`.

The value may be optional or null when unavailable, but the field itself is part of the baseline contract.

## Decision: `issuer` Is Optional Metadata

`issuer` may be exposed for token state in `v1`, but it is not mandatory token-core identity.

## Decision: `totalKnownSupply` Is Optional Metadata

`totalKnownSupply` may also be exposed in `v1`, but it is optional informational metadata rather than mandatory token-core state.

## Decision: Degraded Mode Is Integrity-Based

`degraded` in `v1` is handled by a simplified integrity-based rule.

There are two degraded classes:

### 1. Integrity-safe degraded
Operational quality is reduced, but indexed state integrity is still intact.

Examples:
- source failover happened without losing indexed correctness
- a weaker upstream mode is active, but no state gap exists
- observability or performance quality degraded without breaking truth guarantees

Behavior:
- state endpoints may remain readable
- responses must expose degraded status
- clients must not treat the service as being in normal healthy mode

### 2. Integrity-unsafe degraded
State correctness can no longer be guaranteed.

Examples:
- a realtime gap is detected
- reorg recovery is unresolved
- indexing integrity is in doubt
- required verification path is broken in a way that undermines truth guarantees

Behavior:
- state endpoints must close, just as they do before `live`
- only readiness/degradation metadata may be returned

Rationale:
- avoids shutting down useful reads for every operational issue
- still hard-stops business clients when state truth is no longer trustworthy

## Decision: No Separate Public Degraded Subtype in v1

`v1` does not introduce a separate public degraded subtype enum.

Clients are expected to understand degraded behavior from the existing readiness fields:
- `lifecycleStatus`
- `degraded`
- `readable`
- `authoritative`

This keeps the public contract smaller while still expressing the important operational distinction.
