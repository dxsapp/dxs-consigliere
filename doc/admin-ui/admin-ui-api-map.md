# Consigliere Admin UI API Map

## Endpoint Selection Rules

- prefer `/api/admin/*` when both `admin` and `ops` surfaces exist for the same domain
- treat `/api/admin/*` as shell-facing summary endpoints
- treat `/api/ops/*` as detailed runtime/diagnostic endpoints
- for combined Runtime/Ops screens:
  - use `admin` summary cards first
  - use `ops` endpoints for drill-down panels and detailed tables
- do not invent a frontend merge contract that hides this distinction

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

Severity enum:
- `error`
- `warning`

UI rules:
- `error` => critical/danger styling
- `warning` => warning styling
- unknown future values => neutral fallback

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
Use for cache summary cards in Dashboard and Runtime/Ops overview.

### `GET /api/admin/storage/status`
Use for storage summary cards in Dashboard and Runtime/Ops overview.

### `GET /api/admin/blockchain/sync-status`
Use for dashboard sync card.

## Providers Page Endpoints

### `GET /api/admin/providers`
Returns the operator-facing provider catalog and bounded provider configuration snapshot.

Response shape:
- `recommendations`
  - `realtimePrimaryProvider`
  - `restPrimaryProvider`
  - `rawTxFetchProvider`
- `config`
  - `static`
  - `override`
  - `effective`
  - `overrideActive`
  - `restartRequired`
  - `allowedRealtimePrimaryProviders[]`
  - `allowedRestPrimaryProviders[]`
  - `allowedBitailsTransports[]`
  - `updatedAt`
  - `updatedBy`
- `providers[]`

Provider-config value shape:
- `realtimePrimaryProvider`
- `restPrimaryProvider`
- `bitailsTransport`
- `bitails`
  - `apiKey`
  - `baseUrl`
  - `websocketBaseUrl`
  - `zmqTxUrl`
  - `zmqBlockUrl`
- `whatsonchain`
  - `apiKey`
  - `baseUrl`
- `junglebus`
  - `baseUrl`
  - `mempoolSubscriptionId`
  - `blockSubscriptionId`

Provider catalog item shape:
- `providerId`
- `displayName`
- `roles[]`
- `supportedCapabilities[]`
- `recommendedFor[]`
- `activeFor[]`
- `status`
- `description`
- `missingRequirements[]`
- `helpLinks[]`

Semantics:
- `recommendations` are the product-level safe defaults:
  - realtime = `bitails`
  - REST = `whatsonchain`
  - raw tx = `junglebus`
- `static` = values from static config only
- `override` = persisted operator override only, or `null` when none exists
- `effective` = static config plus operator override
- `restartRequired=true` means override persistence succeeded, but service restart is still required before runtime source/client wiring is guaranteed to switch fully
- this is the shell-facing source of truth for the dedicated `/providers` page

### `PUT /api/admin/providers/config`
Persists the bounded provider override.

Request:
- `realtimePrimaryProvider`
- `restPrimaryProvider`
- `bitailsTransport`
- `bitails`
  - `apiKey`
  - `baseUrl`
  - `websocketBaseUrl`
  - `zmqTxUrl`
  - `zmqBlockUrl`
- `whatsonchain`
  - `apiKey`
  - `baseUrl`
- `junglebus`
  - `baseUrl`
  - `mempoolSubscriptionId`
  - `blockSubscriptionId`

Allowed values in v1:
- `realtimePrimaryProvider`: `bitails | junglebus | node`
- `restPrimaryProvider`: `whatsonchain | bitails`
- `bitailsTransport`: `websocket | zmq`

Responses:
- `200` with the same payload shape as `GET /api/admin/providers`
- `400 { code }` for invalid requests

Current error-code families:
- required selector missing:
  - `realtime_primary_provider_required`
  - `rest_primary_provider_required`
  - `bitails_transport_required`
- invalid selector value:
  - `invalid_realtime_primary_provider`
  - `invalid_rest_primary_provider`
  - `invalid_bitails_transport`
- missing provider-specific requirements:
  - `bitails_websocket_endpoint_required`
  - `bitails_zmq_endpoint_required`
  - `bitails_rest_base_url_required`
  - `whatsonchain_base_url_required`
  - `junglebus_mempool_subscription_id_required`

Semantics:
- persists override outside static files
- this is a bounded provider-onboarding/configuration contract, not a generic config editor
- request may include provider URLs and API keys for the bounded provider set shown on the page
- if requested values match static effective policy, backend resets the override instead of storing a no-op document

### `DELETE /api/admin/providers/config`
Removes the persisted provider override and returns the restored providers snapshot.

Semantics:
- resets back to static config
- does not mutate static files
- restores the `/providers` page to product defaults plus static deployment config

## Runtime Sources Policy Endpoints

### `GET /api/admin/runtime/sources`
Returns the operator-facing realtime routing snapshot.

Response shape:
- `realtimePolicy.static`
- `realtimePolicy.override`
- `realtimePolicy.effective`
- `realtimePolicy.overrideActive`
- `realtimePolicy.restartRequired`
- `realtimePolicy.allowedPrimarySources[]`
- `realtimePolicy.allowedBitailsTransports[]`
- `realtimePolicy.updatedAt`
- `realtimePolicy.updatedBy`

Realtime policy value shape:
- `primaryRealtimeSource`
- `fallbackSources[]`
- `bitailsTransport`

Semantics:
- `static` = values from static config only
- `override` = persisted operator override only, or `null` when none exists
- `effective` = static config plus operator override
- `restartRequired=true` means override persistence succeeded, but service restart is still required before runtime source selection is guaranteed to switch fully
- this endpoint is the shell-facing source of truth for the Runtime Sources panel

### `PUT /api/admin/runtime/sources/realtime-policy`
Persists a narrow realtime override.

Request:
- `primaryRealtimeSource`
- `bitailsTransport`

Allowed values in v1:
- `primaryRealtimeSource`: `bitails | junglebus | node`
- `bitailsTransport`: `websocket | zmq`

Responses:
- `200` with the same payload shape as `GET /api/admin/runtime/sources`
- `400 { code }` for invalid requests

Current error codes:
- `primary_realtime_source_required`
- `invalid_primary_realtime_source`
- `bitails_transport_required`
- `invalid_bitails_transport`

Semantics:
- compatibility surface only
- delegates to the broader provider config service under the hood
- affects realtime policy only
- does not expose the full provider configuration contract

### `DELETE /api/admin/runtime/sources/realtime-policy`
Removes the persisted realtime override and returns the restored runtime-sources snapshot.

Semantics:
- resets back to static config
- does not mutate static files
- this is the only reset action exposed in v1 for runtime source policy

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

UI rules for `tokenHistoryPolicy.trustedRoots[]`:
- use one multiline textarea
- operator enters one root txid per line
- frontend may also accept pasted comma/space-separated values, but normalize before submit
- normalize to lowercase, trim whitespace, deduplicate
- validate each item as `64` hex chars before submit
- show parsed preview and count before confirmation

### `POST /api/admin/manage/address/{address}/history/full`
Upgrade watched address to full history.
Returns history status payload.

### `POST /api/admin/manage/stas-token/{tokenId}/history/full`
Upgrade watched token to rooted full history.
Request body:
- `trustedRoots[]`

Returns history status payload.

UI rules:
- use multiline textarea input, not repeated add/remove controls
- block submit when parsed root list is empty
- block submit when any txid is invalid
- show confirmation dialog before submit because this is a critical scope-changing action

### `POST /api/admin/manage/address/history/full`
Bulk address full-history upgrade.

UI note:
- do not expose this in v1 admin UI
- treat it as internal/operator fallback endpoint for now

### `POST /api/admin/manage/stas-token/history/full`
Bulk token full-history upgrade.

UI note:
- do not expose this in v1 admin UI
- treat it as internal/operator fallback endpoint for now

## Runtime/Ops Detail Endpoints

### `GET /api/ops/providers`
Use for detailed providers/sources panel.

### `GET /api/ops/cache`
Use for detailed runtime cache diagnostics, not for top-level dashboard summary.

### `GET /api/ops/storage`
Use for detailed runtime storage diagnostics, not for top-level dashboard summary.

## Supporting Public Readiness Endpoints

These are public endpoints today, but the admin shell can use them for entity drill-downs.

### `GET /api/readiness/address/{address}`
Returns full tracked readiness payload for an address.

### `GET /api/readiness/token/{tokenId}`
Returns full tracked readiness payload for a token.
