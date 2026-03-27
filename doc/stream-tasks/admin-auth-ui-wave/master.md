# Admin Auth UI Wave

## Goal

Add a minimal operator admin shell for Consigliere and a simple backend admin auth layer.

This wave is explicitly **not** a broad product UI. It is an operator-facing shell for:
- login
- tracked scope management
- runtime and readiness visibility
- rooted token history visibility
- operational status visibility

## Product Frame

Consigliere is an open-source self-hosted scoped BSV indexer.
The admin UI is an operator tool for people running their own indexed scope, not a consumer product.

## Frontend Baseline

Frontend implementation must follow the shared baseline documented in:
- `/Users/imighty/Code/docs/project-stack-profiles.md`
- `/Users/imighty/Code/docs/frontend-principles.md`
- `/Users/imighty/Code/docs/frontend-route-driven-hydration.md`
- `/Users/imighty/Code/docs/frontend-ai-first-engineering.md`

Default constraints pulled from those docs:
- React 19 + TypeScript + Vite 7
- MUI + MUI Data Grid + MUI X Charts
- MobX + mobx-react-lite + mobx-persist-store
- react-router-dom 7
- framer-motion
- `pnpm`
- business logic in stores, not UI
- route/shell-driven hydration, not page `useEffect` ownership
- MUI-only styling via theme and `sx`

## Zones

- `public-api-and-realtime`
- `service-bootstrap-and-ops`
- `verification-and-conformance`
- separate frontend app/module wave owned by Claude

## Scope v1

### Must-have screens
1. Login
2. Dashboard
3. Tracked Addresses list
4. Tracked Tokens list
5. Address details
6. Token details
7. Runtime / Ops
8. Storage / Sources
9. Findings / recent failures

### Must-have actions
1. login/logout
2. add/remove watched address
3. add/remove watched token
4. upgrade address/token to `full_history`
5. inspect state/history readiness
6. inspect rooted token security state
7. inspect source/storage/runtime status

## Auth Model v1

Use the simplest acceptable operator auth:
- one admin identity from config
- username configurable but single-account
- password hash in config
- cookie session auth
- auth can be disabled for trusted local/self-hosted deployments
- no registration
- no RBAC matrix
- no database-backed users

## Delivery Decisions

- admin shell lives in this repo and is served by ASP.NET static files
- built-in login/password only for v1
- reverse-proxy auth is intentionally out of scope for v1

## Status Ledger

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `AA1` | `public-api-and-realtime` | `operator/api` | `todo` | - | endpoint inventory | existing admin/ops API surface is mapped and missing endpoints are explicit |
| `AA2` | `service-bootstrap-and-ops` | `operator/platform` | `todo` | `AA1` | auth integration tests + startup check | simple admin auth backend exists with config-bound cookie auth |
| `AA3` | `repo-governance` | `operator/governance` | `todo` | `AA1`,`AA2` | docs review | Claude handoff package exists with product spec, API map, and frontend rules |
| `AA4` | `frontend app` | `Claude` | `not_opened` | `AA3` | frontend quality gates | operator admin shell is implemented against the documented API and stack rules |
| `AA5` | `verification-and-conformance` | `operator/verification` | `not_opened` | `AA4` | focused end-to-end/manual QA | auth, tracked scope management, readiness, runtime, and rooted token visibility all work end-to-end |

## Deliverables Before Claude Starts

1. `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
2. `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`
3. `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-stack-and-rules.md`

## Definition of Done

- operator can log in and log out
- operator can manage tracked addresses/tokens
- operator can inspect readiness/history/rooted state without Swagger or Raven
- operator can inspect runtime/storage/source status from the UI
- frontend implementation follows the shared `/Users/imighty/Code/docs` stack and architecture rules
