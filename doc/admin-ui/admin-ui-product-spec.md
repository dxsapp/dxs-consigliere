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

- first-run bootstrap decides whether admin protection exists at all
- single configurable admin account when admin protection is enabled
- admin credentials are persisted in DB-backed setup state
- built-in login/password only
- cookie session auth
- admin auth can be disabled for trusted local deployments
- no registration
- no RBAC
- no multi-user management

## Must-Have Screens

1. Login
2. Setup
3. Dashboard
4. Tracked Addresses
5. Tracked Tokens
6. Address Details
7. Token Details
8. Providers
9. Runtime / Ops
10. Storage / Sources
11. Findings / Recent Failures

## Must-Have Actions

1. login/logout
2. complete first-run setup wizard
3. add watched address
4. add watched token
5. upgrade address to full history
6. upgrade token to full history with trusted roots
7. inspect readiness and history status
8. inspect rooted token security state
9. inspect provider catalog and recommended defaults
10. inspect effective provider setup
11. apply/reset bounded provider overrides
12. inspect provider/cache/storage runtime status

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
- `/setup` is the first-run onboarding and provider-configuration surface
- `/providers` is advanced provider settings + provider docs after setup
- Runtime page remains diagnostics-first
- provider configuration is a bounded operator override layer, not static config editing

## Setup Wizard

- setup is capability-first, not provider-first
- first-run questions are:
  - admin access
  - raw transaction source
  - REST fallback
  - realtime source
  - review
- recommended defaults:
  - raw tx = `JungleBus / GorillaPool`
  - REST fallback = `WhatsOnChain`
  - realtime = `Bitails websocket`
- wizard v1 does not expose `Node ZMQ`; keep that as an advanced provider/runtime path after setup
- `/setup` is accessible without auth only while setup is incomplete
- once setup is completed:
  - wizard stops being the normal entry path
  - login is required only if admin protection was enabled

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

- present `/providers` as advanced settings and provider docs, not first-run onboarding
- show current advanced configuration clearly:
  - defaults
  - saved config
  - active runtime config
- show recommended capability defaults explicitly:
  - realtime = `Bitails websocket`
  - REST fallback = `WhatsOnChain`
  - raw tx = `JungleBus / GorillaPool`
- make it obvious that `Bitails` API key is optional for first-run websocket onboarding and becomes an upgrade field for paid or higher-limit usage
- show provider catalog cards for:
  - `Bitails`
  - `WhatsOnChain`
  - `JungleBus`
  - `ZMQ / Node`
- allow bounded override only for:
  - `realtimePrimaryProvider`
  - `rawTxPrimaryProvider`
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
- show JungleBus block-sync health explicitly:
  - observed JungleBus height
  - highest known local indexed height
  - lag blocks
  - last control/scheduled/processed timestamps
  - last error or unavailable reason
- show JungleBus chain-tip assurance as a separate card from raw lag:
  - `state`
  - `assuranceMode`
  - single-source warning
  - control-flow stalled flag
  - local-progress stalled flag
  - last observed movement
  - last local progress
- do not imply full external confirmation when the backend reports `assuranceMode=single_source`

### Findings Rendering

- severity values expected today: `error | warning`
- `error` uses critical styling
- `warning` uses warning styling
- unknown future values fall back to neutral styling

### Entity Detail Surfaces

- address detail page should expose operator-facing local-state counters:
  - BSV balance
  - token balance snapshot
  - UTXO counts
  - transaction count
  - first/last activity
  - latest projection sequence
- token detail page should expose operator-facing local-state counters:
  - protocol + validation
  - issuer + redeem address
  - local known supply + burned satoshis
  - holder count
  - UTXO count
  - transaction count
  - first/last activity
  - latest projection sequence
- rooted-history/trusted-roots/unknown-root status should still come from readiness/history, not from a second derived frontend state machine
