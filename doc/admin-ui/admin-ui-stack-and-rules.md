# Consigliere Admin UI Stack And Rules

## Baseline

This file is the primary handoff contract for the admin shell.
Do not require a second local docs repo to understand the implementation rules.

If the broader frontend baseline docs are available, follow the same style and stack direction.
If they are not available, this file remains authoritative for the admin shell.

## Mandatory Frontend Baseline Summary

- use the established monorepo frontend stack; do not introduce a parallel framework choice
- keep architecture route/shell-driven, not page-local effect-driven
- keep domain logic in stores/services, not page components
- keep frontend AI-first:
  - small modules
  - obvious ownership
  - thin adapters over backend DTOs
  - no hidden business logic duplication
- prefer explicit, readable operational UI over decorative UI

## Stack

- React 19
- TypeScript
- Vite 7
- MUI
- MUI Data Grid
- MUI X Charts
- MobX
- mobx-react-lite
- mobx-persist-store
- react-router-dom 7
- framer-motion
- pnpm

## Architecture Rules

- route/shell-driven hydration, not page-level `useEffect` ownership
- business logic lives in stores, not in page components
- backend DTO strings are authoritative; frontend must not remap domain status into custom enums without a hard reason
- UI components stay presentational where possible
- use MUI theme and `sx`; do not introduce a second styling system
- do not duplicate readiness/history/rooted token business rules in frontend

## Product-Specific Rules

- admin shell is dense, operational, and status-first
- prefer tables + details pages/drawers over marketing-style cards everywhere
- every mutating action needs explicit success/error handling from backend responses
- for mutating action results, use toast notifications consistently
- use inline validation only for local form validation, not as the primary server-result pattern
- partial history, degraded state, and unknown-root findings must remain explicit in UI text
- if an endpoint is missing, scaffold the route cleanly and leave a typed TODO seam instead of inventing fake API behavior
- treat `/api/admin/*` as summary/shell endpoints and `/api/ops/*` as detailed runtime endpoints
- do not expose bulk history-upgrade endpoints in v1 UI

## Token Full-History UX Rules

- `trustedRoots[]` input uses one multiline textarea
- one root txid per line is the primary UX
- pasted comma/space-separated values may be normalized client-side before submit
- normalize to lowercase, trim, deduplicate, and validate `64` hex chars
- show parsed preview/count before submit
- full-history token upgrade requires confirmation dialog

## Notifications Pattern

- success on server mutation => toast
- server error on mutation => toast
- local validation errors => inline field errors
- do not mix Snackbar, inline Alert, and page-level banners for the same mutation outcome

## Findings Severity Contract

- expected values today:
  - `error`
  - `warning`
- future unknown values must render with neutral fallback styling, not crash or disappear

## Hosting Model

- admin frontend is bundled in this repo
- final bundle is served by ASP.NET static files
- keep frontend build output and hosting wiring simple; no second runtime server for v1
