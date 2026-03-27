# Consigliere Admin UI Stack And Rules

## Mandatory Frontend Baseline

Follow these source documents exactly:
- `/Users/imighty/Code/docs/project-stack-profiles.md`
- `/Users/imighty/Code/docs/frontend-principles.md`
- `/Users/imighty/Code/docs/frontend-route-driven-hydration.md`
- `/Users/imighty/Code/docs/frontend-ai-first-engineering.md`

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
- partial history, degraded state, and unknown-root findings must remain explicit in UI text
- if an endpoint is missing, scaffold the route cleanly and leave a typed TODO seam instead of inventing fake API behavior

## Hosting Model

- admin frontend is bundled in this repo
- final bundle is served by ASP.NET static files
- keep frontend build output and hosting wiring simple; no second runtime server for v1
