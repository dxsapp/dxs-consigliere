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

## Current Missing Endpoints

These are the concrete missing backend contracts for a complete admin shell:

1. tracked addresses list
2. tracked tokens list
3. tracked address details aggregate endpoint
4. tracked token details aggregate endpoint
5. remove/untrack address
6. remove/untrack token
7. findings/recent failures feed
8. dashboard aggregate summary

Frontend must not synthesize these contracts locally.
