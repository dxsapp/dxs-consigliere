# Consigliere Public API Contract v1

## Product Promise

`Consigliere` is a selective-indexed BSV backend for wallet, trading, payment, and infrastructure systems.

Primary promise:
- authoritative indexed state for managed scope
- stable realtime for managed scope
- no mandatory self-hosted node requirement
- normalized behavior across multiple upstream chain sources

Non-goals:
- full-network indexing
- explorer-first product behavior
- exposing provider-specific semantics as the public contract

## API Priority

Priority order:
1. wallet and payment backend queries
2. trading and infra state/readiness queries
3. realtime delivery for managed scope
4. administrative and operational visibility
5. explorer-like assist methods

## Managed Scope

The API is built around explicitly managed scope, not around full-chain completeness.

Managed scope includes:
- tracked addresses
- tracked tokens
- transactions affecting tracked addresses or tracked tokens
- derived state needed to answer authoritative balance, UTXO, history, and token-state queries

For `v1`, the only primary public managed units are:
- `tracked_address`
- `tracked_token`

Transactions are queryable and participate in derived indexed scope, but are not primary public tracking units.

Token identity rule for `v1`:
- the only canonical token key is `tokenId`
- all public token routes, storage references, and tracking registration use `tokenId`
- semantic distinctions beyond `tokenId` belong to higher-level business logic, not to the base platform contract

Expansion boundary for `v1`:
- `tracked_address` state uses strict direct-touch scope only
- `tracked_token` state includes full reverse token lineage through all parent generations until valid issuance
- full `(D)STAS` lineage lookup is allowed only for validation and Back-to-Genesis correctness paths

## Common Response Envelope

Every state endpoint should be able to return:
- `data`
- `scope`
- `indexing`
- `realtime`

Example shape:

```json
{
  "data": {},
  "scope": {
    "tracked": true,
    "entityType": "address",
    "entityId": "1..."
  },
  "indexing": {
    "status": "live",
    "completeness": "authoritative",
    "lagBlocks": 0,
    "lagSeconds": 2,
    "asOfBlockHeight": 123456,
    "asOfBlockHash": "..."
  },
  "realtime": {
    "active": true,
    "sourceStatus": "healthy"
  }
}
```

Minimum metadata contract:
- `scope.tracked`
- `indexing.status`
- `indexing.completeness`
- `indexing.asOfBlockHeight`
- `indexing.lagBlocks`

## Consistency Classes

### Managed authoritative
- tracked and live
- strongest SLA
- expected for wallet, terminal, and payment-critical flows

`live` is reached only after:
- required historical backfill is complete
- realtime is attached
- the gap between backfill and realtime is closed

### Managed catching-up
- tracked but still backfilling or catching up
- not authoritative and not readable through state endpoints

`catching_up` is intentionally kept as a separate visible lifecycle state rather than being merged into `backfilling`.

## Decision: No State Reads Before `live`

For tracked entities in `backfilling` or `catching_up`, `Consigliere` must not return state answers.

Reason:
- before `live`, balances, UTXOs, history, and token state can all still be false
- even well-marked partial state is too easy for business clients to misuse

Allowed before `live`:
- scope status
- lifecycle state
- lag and readiness metadata

Not allowed before `live`:
- balance answers
- UTXO answers
- authoritative history answers
- token state answers

Progress note:
- readiness progress is capability-dependent
- `status` is mandatory
- lag and progress details are returned only when they can be computed honestly from the active sync strategy and available upstream sources
- no universal progress percentage is guaranteed in `v1`

### Assist
- helper lookups outside authoritative tracked scope
- no promise of full completeness

## Decision: No General Untracked Reads in v1

`Consigliere` does not provide general untracked state reads in `v1`.

If an address, token, or state object is not tracked and not known inside authoritative managed scope, the service must not answer with inferred or provider-derived state as if it were reliable.

Implications:
- no general address-state reads for untracked addresses
- no general token-state reads for untracked tokens
- no convenience explorer-style fallback for unknown entities
- state APIs are reserved for tracked or already-authoritative managed scope

This rule exists to preserve:
- product honesty
- clear SLA boundaries
- predictable cost
- separation from explorer-style behavior

## Decision: Known Transaction Lookup Is Allowed

Transactions are the basic information unit of the blockchain, so `Consigliere` may return a transaction if it already knows it.

This is the only allowed narrow exception to the no-untracked-state rule.

Rules:
- if the transaction is already known to `Consigliere`, it may be returned
- `Consigliere` returns only the transaction data it already has
- `Consigliere` does not fabricate, infer, or fetch missing metadata just to enrich an otherwise unknown transaction
- if indexed state or metadata is unavailable, the response may contain only raw or minimally known transaction data
- a transaction is considered "known" only if it already exists in `Consigliere` local persisted store
- the fact that an upstream provider could return the transaction right now does not make it a known transaction for public API purposes

Implication:
- transaction lookup is allowed as a known-object assist method
- transaction state enrichment remains scoped to managed authoritative data

## Core API Groups

### 1. Control plane
Controls what the service tracks and how scope is maintained.

Mandatory endpoints:
- `POST /api/scope/addresses`
- `POST /api/scope/addresses/bulk`
- `DELETE /api/scope/addresses/{address}`
- `POST /api/scope/addresses/untrack/bulk`
- `POST /api/scope/tokens`
- `POST /api/scope/tokens/bulk`
- `DELETE /api/scope/tokens/{tokenId}`
- `POST /api/scope/tokens/untrack/bulk`
- `GET /api/scope/status`
- `GET /api/scope/readiness`
- `POST /api/scope/backfill`
- `POST /api/scope/pause`
- `POST /api/scope/resume`

### 2. State and query plane
Main product surface.

#### Transactions
- `GET /api/tx/get/{id}`
- `GET /api/tx/batch/get`
- `GET /api/tx/state/{id}`
- `GET /api/tx/stas/validate/{id}`

`GET /api/tx/by-height/get` is classified as assist, not core promise.

Broadcast note:
- `POST /api/tx/broadcast/{raw}` is a submission operation
- network visibility and eventual confirmation are established asynchronously through ingest/realtime observation rather than a single immediate provider response

## Decision: Transaction State Includes Broadcast-Aware Lifecycle

`v1` transaction state should expose a lifecycle model rich enough for broadcast-driven business flows.

Exact `tx lifecycleStatus` enum in `v1`:
- `broadcast_submitted`
- `seen_by_source`
- `seen_in_mempool`
- `confirmed`
- `reorged`
- `dropped`

This gives clients a usable operational model for submitted transactions without collapsing submission and final confirmation into one step.

## Decision: `tx state` Includes Lifecycle, Visibility, and Managed Relevance

The `v1` transaction-state object is not only a lifecycle object.

It includes:
- lifecycle
- visibility
- managed-scope relevance

Rationale:
- a business client needs to know not only where a transaction is in its lifecycle
- but also whether `Consigliere` considers that transaction relevant to managed scope

## Decision: Minimal `tx state` Shape in v1

The minimal `tx state` object includes:

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

Notes:
- `relevanceTypes[]` is a list because a transaction may be relevant for multiple reasons at once
- `validationStatus` is optional and applies only where validation semantics are meaningful

#### Addresses
- `GET /api/address/{address}/state`
- `GET /api/address/{address}/balances`
- `GET /api/address/{address}/utxos`
- `GET /api/address/{address}/history`
- `GET /api/address/{address}/activity`

## Decision: Address State Core for v1

The required core address state surface for `v1` is:
- readiness/status
- balances
- UTXOs
- history

`activity` may exist, but it is not part of the minimal mandatory address state core.

#### Tokens
- `GET /api/token/{tokenId}/state`
- `GET /api/token/{tokenId}/balances`
- `GET /api/token/{tokenId}/utxos`
- `GET /api/token/{tokenId}/history`
- `GET /api/token/{tokenId}/holders`

Global holders semantics across the whole network are out of scope for v1.

## Decision: Token State Core for v1

The required core token surface for `v1` is:
- readiness/status
- token state
- balances
- UTXOs
- history
- token transaction validation against Back-to-Genesis rules

`holders` is a useful derivative view, but it is not part of the minimal mandatory token core.

For `(D)STAS`, the existing `validate stas tx` capability is part of this token-core promise and should evolve into the platform's authoritative Back-to-Genesis validation path.

## Decision: Token State and Transaction Validation Stay Separate

`v1` keeps these as separate concerns:

1. token state
2. transaction validation

Rationale:
- asset-level token state and tx-level validation answer different questions
- token state should describe the tracked token as an asset
- transaction validation should describe whether a specific token transaction satisfies Back-to-Genesis and protocol rules

Implication:
- token state may expose asset-level validation status
- `validate stas tx` style endpoints remain a distinct validation capability

## Decision: Token Validation Semantics in v1

`v1` token state should not expose a bare boolean `isValid`.

Instead:
- `issuanceKnown: bool`
- `validationStatus: unknown | valid | invalid`

Meaning:
- `issuanceKnown` answers whether `Consigliere` has resolved the token lineage back to issuance
- `validationStatus` answers the current validation result once enough lineage is known

This keeps provenance knowledge separate from the validation verdict.

## Decision: `protocolType` Is Public in v1

Token state in `v1` must expose `protocolType` publicly.

Rationale:
- validation semantics already depend on protocol family
- clients need to know whether they are dealing with `stas`, `dstas`, or future token families
- this creates a clean extension point for future protocol versioning and new token models

## Decision: `protocolVersion` Is Also Public in v1

Token state in `v1` should also expose `protocolVersion`.

This field may be optional or null when version semantics are unavailable, but it belongs in the public contract from the start.

Rationale:
- serious business integrations benefit from explicit version awareness
- adding version as a first-class field later would be a less clean contract evolution

## Decision: `issuer` Is Optional Token Metadata

`issuer` may be exposed in token state, but it is not a hard-required core field.

Interpretation:
- for a valid token, issuer identity is typically knowable
- but issuer is still treated as secondary metadata rather than the foundation of the token-state contract

This keeps the token core focused while still allowing useful metadata for clients that care about issuer information.

## Decision: `totalKnownSupply` Is Optional Token Metadata

`totalKnownSupply` may be exposed in token state, but it is not a required token-core field in `v1`.

It belongs to the same general informational class as `issuer`:
- useful when available
- not foundational to the base token-state contract

### 3. Realtime plane
Realtime is a core promise, but only for managed scope.

Expected channels:
- address activity
- token activity
- tx lifecycle updates
- balance updates
- scope readiness and degradation events

Minimum event types:
- `scope_status_changed`
- `scope_caught_up`
- `scope_degraded`
- `tx_seen`
- `tx_confirmed`
- `tx_reorged`
- `tx_dropped`
- `balance_changed`
- `token_state_changed`

## Decision: `balance_changed` Is First-Class

`balance_changed` is a first-class required realtime event in `v1`.

It is not treated as an optional convenience derivative of transaction events.

Rationale:
- for wallet, payment, and terminal backends, balance movement is one of the most important realtime contracts
- clients should not be forced to reconstruct balance semantics from raw transaction lifecycle events

## Decision: `token_state_changed` Is First-Class

`token_state_changed` is a first-class required realtime event in `v1`.

It is not treated as an optional convenience event layered on top of transaction events.

Rationale:
- tokens are a primary managed unit in the platform model
- token-centric clients should not need to reconstruct token state transitions from low-level tx flow alone

## Decision: Realtime May Start Before `live`

Realtime subscription is allowed before a tracked object reaches `live`.

But:
- the stream must be explicitly marked as non-authoritative until readiness is achieved
- clients must not treat pre-`live` realtime as equivalent to final indexed state

Rationale:
- early events are useful as liveness signals
- clients can observe that the system is active
- readiness and state guarantees still remain separate from event flow

## Decision: Minimal Realtime Event Envelope for v1

The minimal realtime event envelope is:

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

Notes:
- `txId` is optional
- `blockHeight` is optional
- `authoritative` tells the client whether the stream event is safe to interpret as authoritative at that moment

## Decision: Allowed Realtime Entity Types in v1

Allowed realtime `entityType` values:
- `address`
- `token`
- `scope`
- `transaction`

Notes:
- `address` and `token` remain the only primary public managed units
- `scope` exists for readiness and system-level managed-scope signals
- `transaction` exists for lifecycle events without turning transactions into primary public tracking units

### 4. Ops plane
Operational endpoints are first-class because the service is an infra backend, not just a data API.

Mandatory endpoints:
- `GET /api/ops/health`
- `GET /api/ops/indexing/status`
- `GET /api/ops/providers`
- `GET /api/ops/backfill/status`
- `POST /api/admin/reindex`
- `POST /api/admin/backfill`

## Decision: Provider Status Is a Separate Ops Surface

`v1` keeps provider status as its own ops surface rather than collapsing everything into one generic health endpoint.

Rationale:
- `Consigliere` is explicitly multisource
- operators need visibility into source health, degradation, and routing posture
- one coarse health signal is not enough for platform-grade troubleshooting

## Decision: Provider Ops Shape Is Per-Provider First

`v1` models provider diagnostics as a per-provider status object with nested capability substatus.

This means the primary ops surface is organized around providers such as `node`, `junglebus`, `bitails`, and `whatsonchain`, while each provider object can expose capability-level status for:
- `broadcast`
- `realtime_ingest`
- `block_backfill`
- `raw_tx_fetch`
- `validation_fetch`

Rationale:
- operators reason about concrete providers first
- capability health still matters and must remain visible
- per-provider top-level status avoids an overly fragmented ops surface

### Minimal Provider Status Shape

The minimum `provider status` object in `v1` is:
- `provider`
- `enabled`
- `configured`
- `roles[]`
- `healthy`
- `degraded`
- `lastSuccessAt?`
- `lastErrorAt?`
- `lastErrorCode?`
- `rateLimitState?`
- `capabilities`

Notes:
- `roles[]` is a list rather than a single value because one provider may serve multiple roles at once
- `currentUsage` is intentionally omitted from the base contract because it is too vague; if needed later, `v2` should add a more precise field

### Minimal Capability Status Shape

Each entry inside `capabilities` uses the following minimum shape in `v1`:
- `enabled`
- `healthy`
- `degraded`
- `lastSuccessAt?`
- `lastErrorAt?`
- `lastErrorCode?`
- `rateLimitState?`
- `active`

Where:
- `enabled` means the capability is allowed by configuration for this provider
- `active` means the routing layer currently uses this provider for that capability rather than only keeping it available as a standby path

### Minimal `rateLimitState` Shape

The minimum `rateLimitState` object in `v1` is:
- `limited`
- `remaining?`
- `resetAt?`
- `scope?`
- `sourceHint?`

Interpretation:
- `limited` indicates whether the provider or capability is currently under an active rate-limit condition
- `remaining` is optional because not all providers expose a trustworthy remaining-budget counter
- `resetAt` is optional because not all providers disclose a clean reset boundary
- `scope` may be `provider`, `capability`, or `unknown`
- `sourceHint` is a short machine-friendly reason such as `http_429` or `quota_exhausted`

## Error Contract

Public errors must remain provider-agnostic.

Standard error codes:
- `bad_request`
- `not_found`
- `not_tracked`
- `scope_not_ready`
- `degraded_mode`
- `rate_limited`
- `internal_error`

Example:

```json
{
  "error": {
    "code": "scope_not_ready",
    "message": "Address is tracked but still catching up",
    "details": {
      "status": "catching_up",
      "lagBlocks": 18
    }
  }
}
```

## Public Contract Invariants

1. For tracked entities in `live`, state endpoints must be authoritative within current reorg guarantees.
2. For tracked entities not yet in `live`, partial state must be explicitly marked.
3. For untracked entities, the API must not imply full completeness.
4. Realtime guarantees apply only to managed scope.
5. Upstream provider choice must not leak into public contract semantics.
6. Public API is optimized for business backends, not for full-chain exploration.

## Decision: Degraded Readability Uses an Integrity Rule

`v1` degraded handling is intentionally simple:

- if degradation is integrity-safe, state endpoints may remain readable with degraded marking
- if degradation is integrity-unsafe, state endpoints must close and return readiness/degradation status only

This keeps the contract strict where truth is at risk and permissive where the system is merely operating below ideal quality.

## Decision: No Extra Public Degraded Enum in v1

The public API does not need a separate degraded subtype enum in `v1`.

Clients should interpret degraded behavior from:
- `lifecycleStatus`
- `degraded`
- `readable`
- `authoritative`

## Decision: Readiness Must Exist Both as Status and as Enforcement

`v1` requires both:

1. explicit readiness/status endpoints for tracked objects and scope orchestration
2. enforcement on state endpoints, which must still reject reads when scope is not ready

This means:
- clients can poll readiness directly
- state endpoints do not need to double as readiness discovery tools
- business clients still get hard safety guarantees when they try to read too early

## Decision: Minimal Readiness Object for v1

The minimal readiness object for a tracked address or tracked token is:

- `tracked`
- `entityType`
- `entityId`
- `lifecycleStatus`
- `readable`
- `authoritative`
- `degraded`
- `lagBlocks?`
- `progress?`

Notes:
- `lagBlocks` is optional
- `progress` is optional
- the object may be extended later without changing the baseline contract
