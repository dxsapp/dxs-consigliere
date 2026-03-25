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

### Managed catching-up
- tracked but still backfilling or catching up
- partial data allowed only when explicitly marked

### Assist
- helper lookups outside authoritative tracked scope
- no promise of full completeness

## Core API Groups

### 1. Control plane
Controls what the service tracks and how scope is maintained.

Mandatory endpoints:
- `POST /api/scope/addresses`
- `DELETE /api/scope/addresses/{address}`
- `POST /api/scope/tokens`
- `DELETE /api/scope/tokens/{tokenId}`
- `GET /api/scope/status`
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

#### Addresses
- `GET /api/address/{address}/state`
- `GET /api/address/{address}/balances`
- `GET /api/address/{address}/utxos`
- `GET /api/address/{address}/history`
- `GET /api/address/{address}/activity`

#### Tokens
- `GET /api/token/{tokenId}/state`
- `GET /api/token/{tokenId}/balances`
- `GET /api/token/{tokenId}/utxos`
- `GET /api/token/{tokenId}/history`
- `GET /api/token/{tokenId}/holders`

Global holders semantics across the whole network are out of scope for v1.

### 3. Realtime plane
Realtime is a core promise, but only for managed scope.

Expected channels:
- address activity
- token activity
- tx lifecycle updates
- balance updates
- scope readiness and degradation events

Minimum event types:
- `tx_seen`
- `tx_confirmed`
- `tx_reorged`
- `tx_dropped`
- `balance_changed`
- `token_state_changed`
- `scope_caught_up`
- `scope_degraded`

### 4. Ops plane
Operational endpoints are first-class because the service is an infra backend, not just a data API.

Mandatory endpoints:
- `GET /api/ops/health`
- `GET /api/ops/indexing/status`
- `GET /api/ops/providers`
- `GET /api/ops/backfill/status`
- `POST /api/admin/reindex`
- `POST /api/admin/backfill`

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
