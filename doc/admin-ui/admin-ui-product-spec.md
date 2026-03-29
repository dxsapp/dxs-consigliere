# Consigliere Admin UI Product Spec

## Product Frame

Consigliere admin UI is an operator shell for a self-hosted scoped BSV indexer.
It is not a consumer product, wallet UI, or universal explorer.

## Delivery Shape

- frontend lives in this repo
- built as a small SPA bundle
- served by ASP.NET static files from the Consigliere service
- thin UI over existing HTTP API

## Auth Model v1

- single configurable admin account
- built-in login/password only
- cookie session auth
- auth can be disabled for trusted local deployments
- no registration
- no RBAC
- no multi-user management

## Must-Have Screens

1. Login
2. Dashboard
3. Tracked Addresses
4. Tracked Tokens
5. Address Details
6. Token Details
7. Providers
8. Runtime / Ops
9. Storage / Sources
10. Findings / Recent Failures

## Must-Have Actions

1. login/logout
2. add watched address
3. add watched token
4. upgrade address to full history
5. upgrade token to full history with trusted roots
6. inspect readiness and history status
7. inspect rooted token security state
8. inspect provider catalog and recommended defaults
9. inspect effective provider setup
10. apply/reset bounded provider overrides
11. inspect provider/cache/storage runtime status

## Explicit Non-Goals For v1

- no UI for bulk history-upgrade endpoints
- no multi-user admin management
- no reverse-proxy auth mode in UI
- no destructive purge UI for tracked entities
- no generic config editor
- no arbitrary capability-matrix editor
- no provider billing or purchase flow inside the shell

## Status-First UX Rules

- use exact backend state strings; do not invent simplified frontend states
- show `stateReadiness` and `historyReadiness` separately
- show rooted token security as explicit fields, not a vague secure/insecure badge
- degraded, partial-history, and unknown-root states must be visually explicit
- destructive or scope-changing actions require confirmation
- operator should not need Swagger or Raven for normal daily use

## Backend Contract Notes For UI Phase

- tracked lists/details/remove/findings/dashboard endpoints now exist
- delete/untrack is tombstone semantics, not destructive purge
- config-managed tracked entities cannot be deleted from the shell and should surface `managed_by_config`
- frontend should treat readiness/history/rooted status strings as authoritative and not collapse them into custom state machines
- runtime screens should use `admin` endpoints for summary and `ops` endpoints for detail
- Providers page is the onboarding and provider-configuration surface
- Runtime page remains diagnostics-first
- provider configuration is a bounded operator override layer, not static config editing

## Critical UX Decisions

### Token Full-History Upgrade

- `trustedRoots[]` is entered through one multiline textarea
- one txid per line is the primary UX
- normalize, deduplicate, and validate before submit
- show parsed preview/count
- require confirmation before submit

### Mutation Feedback

- server success => toast
- server failure => toast
- local form validation => inline field errors

### Providers Page

- show recommended defaults explicitly:
  - realtime = `Bitails`
  - REST = `WhatsOnChain`
- show recommended practical raw transaction source explicitly:
  - raw tx = `JungleBus / GorillaPool`
- show static vs override vs effective provider setup side by side
- show provider catalog cards for:
  - `Bitails`
  - `WhatsOnChain`
  - `JungleBus`
  - `ZMQ / Node`
- allow bounded override only for:
  - `realtimePrimaryProvider`
  - `restPrimaryProvider`
  - `bitailsTransport`
  - provider-specific connection fields exposed by backend
- show allowed options from backend, not hardcoded frontend enums
- apply/reset actions require confirmation
- when backend returns `restartRequired=true`, surface an explicit operator warning that restart is still needed before runtime source-selection changes apply fully
- provider help links should be visible and treated as first-class onboarding guidance

### Runtime Page

- keep runtime diagnostics on `/runtime`
- do not make Runtime the main provider-configuration surface
- if runtime source summary remains visible there, treat it as compatibility/diagnostic context only

### Findings Rendering

- severity values expected today: `error | warning`
- `error` uses critical styling
- `warning` uses warning styling
- unknown future values fall back to neutral styling
