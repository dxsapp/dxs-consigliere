# Consigliere Admin UI API Map

## Auth Endpoints

### `GET /api/admin/auth/me`
Returns current auth status for the admin shell.

Response shape:
- `enabled: boolean`
- `authenticated: boolean`
- `mode: "cookie" | "disabled"`
- `username?: string`
- `sessionTtlMinutes?: number`

Semantics:
- when auth is disabled: returns `enabled=false`, `authenticated=true`, `mode="disabled"`
- when auth is enabled and user is not logged in: returns `enabled=true`, `authenticated=false`, `mode="cookie"`

### `POST /api/admin/auth/login`
Request:
- `username: string`
- `password: string`

Responses:
- `200` with auth status payload on success
- `400` with `{ code: "credentials_required" }` when request is incomplete
- `401` with `{ code: "invalid_credentials" }` when credentials are wrong

### `POST /api/admin/auth/logout`
Clears cookie session when auth is enabled.
Returns auth status payload.

## Existing Operator Endpoints

## Admin Tracking Endpoints

### `GET /api/admin/tracked/addresses`
Returns tracked addresses for the admin shell.

Query:
- `includeTombstoned: boolean` default `false`

Response item shape:
- `address`
- `name`
- `isTombstoned`
- `tombstonedAt`
- `createdAt`
- `updatedAt`
- `failureReason`
- `integritySafe`
- `readiness`

### `GET /api/admin/tracked/tokens`
Returns tracked tokens for the admin shell.

Query:
- `includeTombstoned: boolean` default `false`

Response item shape:
- `tokenId`
- `symbol`
- `isTombstoned`
- `tombstonedAt`
- `createdAt`
- `updatedAt`
- `failureReason`
- `integritySafe`
- `readiness`

### `GET /api/admin/tracked/address/{address}`
Returns tracked-address details aggregate.

Important semantics:
- returns `404 { code: "not_tracked", entityId }` when the address is absent
- returns full readiness/history payload nested inside the tracked-address response

### `GET /api/admin/tracked/token/{tokenId}`
Returns tracked-token details aggregate.

Important semantics:
- returns `404 { code: "not_tracked", entityId }` when the token is absent
- returns rooted token history fields via nested readiness/history payload

### `DELETE /api/admin/tracked/address/{address}`
Untracks a DB-managed address.

Important semantics:
- this is **tombstone/stop tracking**, not history purge
- removes runtime watch set entry for DB-managed entities
- returns `409 { code: "managed_by_config", entityId }` for statically configured addresses
- returns `404 { code: "not_tracked", entityId }` when the address does not exist

Success payload:
- `entityType`
- `entityId`
- `code: "untracked"`
- `tombstoned: true`
- `tombstonedAt`

### `DELETE /api/admin/tracked/token/{tokenId}`
Untracks a DB-managed token.

Important semantics:
- this is **tombstone/stop tracking**, not history purge
- removes runtime watch set entry for DB-managed entities
- returns `409 { code: "managed_by_config", entityId }` for statically configured tokens
- returns `404 { code: "not_tracked", entityId }` when the token does not exist

Success payload:
- `entityType`
- `entityId`
- `code: "untracked"`
- `tombstoned: true`
- `tombstonedAt`

## Admin Summary Endpoints

### `GET /api/admin/findings`
Returns operator-facing findings derived from persisted tracked status.

Query:
- `take: number` default `100`

Response item shape:
- `entityType`
- `entityId`
- `code`
- `severity`
- `message`
- `observedAt`

Current finding sources:
- tracked status `failureReason`
- rooted token `unknownRootFindings`

### `GET /api/admin/dashboard/summary`
Returns thin dashboard aggregate counts.

Response shape:
- `activeAddressCount`
- `activeTokenCount`
- `tombstonedAddressCount`
- `tombstonedTokenCount`
- `degradedAddressCount`
- `degradedTokenCount`
- `backfillingAddressCount`
- `backfillingTokenCount`
- `fullHistoryLiveAddressCount`
- `fullHistoryLiveTokenCount`
- `unknownRootFindingCount`
- `blockingUnknownRootTokenCount`
- `failureCount`

### `GET /api/admin/cache/status`
Use for cache panel and dashboard cache summary.

### `GET /api/admin/storage/status`
Use for storage panel.

### `GET /api/admin/blockchain/sync-status`
Use for dashboard sync card.

### `POST /api/admin/manage/address`
Create/update watched address.
Returns tracked address readiness payload.

Important request fields:
- `address`
- `name?`
- `historyPolicy.mode`: `forward_only | full_history`

### `POST /api/admin/manage/stas-token`
Create/update watched token.
Returns tracked token readiness payload.

Important request fields:
- `tokenId`
- `symbol?`
- `historyPolicy.mode`: `forward_only | full_history`
- `tokenHistoryPolicy.trustedRoots[]` required for token `full_history`

### `POST /api/admin/manage/address/{address}/history/full`
Upgrade watched address to full history.
Returns history status payload.

### `POST /api/admin/manage/stas-token/{tokenId}/history/full`
Upgrade watched token to rooted full history.
Request body:
- `trustedRoots[]`

Returns history status payload.

### `POST /api/admin/manage/address/history/full`
Bulk address full-history upgrade.

### `POST /api/admin/manage/stas-token/history/full`
Bulk token full-history upgrade.

### `GET /api/ops/providers`
Use for providers/sources panel.

### `GET /api/ops/cache`
Use for runtime cache card/panel.

### `GET /api/ops/storage`
Use for runtime storage card/panel.

## Supporting Public Readiness Endpoints

These are public endpoints today, but the admin shell can use them for entity drill-downs.

### `GET /api/readiness/address/{address}`
Returns full tracked readiness payload for an address.

### `GET /api/readiness/token/{tokenId}`
Returns full tracked readiness payload for a token.
