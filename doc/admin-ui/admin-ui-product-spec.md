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
7. Runtime / Ops
8. Storage / Sources
9. Findings / Recent Failures

## Must-Have Actions

1. login/logout
2. add watched address
3. add watched token
4. upgrade address to full history
5. upgrade token to full history with trusted roots
6. inspect readiness and history status
7. inspect rooted token security state
8. inspect provider/cache/storage runtime status

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
