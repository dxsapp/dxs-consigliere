---
created: 2026-05-18
type: program
status: draft (awaiting approval)
parent: —
related: docs/admin-ui/design-handoff/00-design-brief.md (the design contract);
         docs/admin-ui/design-bundle/ (the mockups + tokens);
         /Users/imighty/Code/docs/project-stack-profiles.md (the stack baseline);
         /Users/imighty/Code/docs/frontend-principles.md (engineering principles)
---

# Admin UI vNext — Implementation Program

Build the Consigliere admin UI from scratch on the
workspace-default frontend stack, matching the hi-fi mockups
in `docs/admin-ui/design-bundle/`. The existing
`src/admin-ui/` stub is abandoned and will be deleted at S0.

## Goal

A production admin UI for Consigliere covering all 14 screens
from the design brief. Operator (biz-op) section ships first
+ correct; System section ships next + functional; polish +
ops gates last.

Business outcome: a single human surface on top of the W1-W6
backend, replacing log-grep workflows for biz-ops and ad-hoc
admin endpoint hits for engineers.

## Product Decision

- **Replace `src/admin-ui/` with a from-scratch Vite project**
  (deletion happens at S0; no migration path from the stub).
- **Workspace-default stack, no deviations**: React 19 + Vite
  7 + TypeScript + MUI + MUI X DataGrid/Charts + MobX +
  mobx-persist-store + react-router-dom 7 + framer-motion +
  pnpm. See `/Users/imighty/Code/docs/project-stack-profiles.md`.
- **Real backend by default; mock layer env-switchable** for
  offline dev (mock mirrors real contract shape). Per
  stack-profile rule.
- **Stop-and-audit per slice** mirroring the W1-W6 backend
  program: each slice has a pre-execution A1 prompt + a
  post-execution audit. Prereq slices (S0 + the foundational
  S1-S3 trio) have slice-level audit gates.
- **No new contract surfaces on the backend.** The UI consumes
  only the frozen admin REST + SignalR contracts at program
  close (`docs/stream-tasks/consigliere-thin-node-observer-program/evidence/closeout.md`).
  Any backend gap (e.g. SignalR push for alerts; hot-reload
  config endpoint) is recorded as a post-program follow-up,
  not silently absorbed into this UI program.

## Scope

In scope:

- **Foundation (S0-S3).** Fresh Vite project, MUI theme from
  the design tokens, app shell (AppBar / Drawer / router /
  ThemeProvider / MobX root / persist-store), shared API
  client + SignalR client + typed DTOs + error normalization,
  env-driven real/mock switch.
- **Operator screens (S4-S7).** Dashboard (4 design variants),
  entity detail (Tx / Address / Token sharing the timeline
  pattern), Broadcast Queue kanban, Alerts.
- **System screens (S8-S10).** P2P Pool, Source Metrics,
  Headers Chain, Broadcast Inspector, Configuration (read-
  only this wave; tune affordance hidden behind a future
  backend hot-reload endpoint), Logs/Raw, Providers, Setup.
- **Polish + ops (S11-S12).** WCAG AA audit, perf pass
  (Lighthouse + bundle size budget), e2e smoke for golden
  paths, production build verification, deletion of old
  `src/admin-ui/`, program closeout doc.

Out of scope (post-program follow-ups):

- **Backend hot-reload config endpoint** (Configuration screen
  ships read-only; inline edit affordance lands when the
  endpoint does).
- **SignalR push for alerts** (poller is `?since=`
  incremental; if backend later adds push, UI switches).
- **Live mainnet broadcast validation** (operator-session
  item, same residual as the backend program).
- **Alert escalation sinks** (Slack / PagerDuty webhook); the
  Alerts screen has a hook-point in the layout for a future
  "Routes" tab.
- **SPA-side log streaming** (depends on a backend log-stream
  endpoint).

## Core Rules

1. **Workspace-stack-only.** No additional UI libraries. MUI
   primitives + `sx`/theme overrides. No CSS / SCSS / styled-
   components. Animations only via framer-motion.
2. **Layered architecture.** `UI components → MobX stores →
   API/SignalR client → typed DTOs`. UI components render
   store state; never call the API directly.
3. **Route-driven hydration.** Initial business data is owned
   by route loaders / shell-level `ensureLoaded(...)` calls,
   not page `useEffect` (per `frontend-principles.md` §16-17).
4. **Single source of truth per screen.** Each screen has one
   MobX store; cross-screen shared state lives in a parent
   store. No prop-drilling business data.
5. **Async state machines explicit.** Every async flow exposes
   `idle | loading | success | error` (or named transitions
   for complex flows). No silent "loading forever" states.
6. **Background activity cleanup.** Every poll / timer /
   SignalR subscription has cleanup + cancellation + retry-
   with-backoff. No leaks across route changes.
7. **Typed contracts.** DTOs in `src/types/api.ts` (or
   generated from a contract spec); domain types in
   `src/types/domain.ts`. UI never sees raw JSON.
8. **Error normalization.** One `AppError` model; HTTP /
   network / business errors map onto it before reaching the
   store.
9. **A11y as default.** Keyboard nav on every interactive
   element; WCAG AA contrast in both palettes; `aria-label`
   on every icon-only button. MUI primitives give most for
   free; the audit (S11) verifies.
10. **Pnpm verify must pass on every commit.** `pnpm typecheck
    + lint + build + test + verify` (per
    `project-stack-profiles.md`).
11. **Stop-and-audit per slice.** S0 + S2 + S3 gate the rest
    with slice-level audits. S4-S10 covered by the program-
    level A1.
12. **Mock layer mirrors real shape.** When `VITE_API_MODE=mock`,
    every endpoint returns the same DTO shape as production
    with seed data. No "mock-only" fields.

## Ownership Zones

| Program zone | Repo zone (per repository-zones registry) | Files |
|---|---|---|
| `admin-ui-foundation` | `admin-ui` (out of zone catalog; lives at `src/admin-ui/`) | New: `src/admin-ui/package.json` · `vite.config.ts` · `tsconfig.json` · `index.html` · `src/main.tsx` · `src/app/{App,routes,theme,api,signalr}.ts` · `src/stores/{root,shell,*}.ts` · `src/types/{api,domain,errors}.ts` · `src/lib/{api-client,signalr-client,mock}/` |
| `admin-ui-operator-screens` | `admin-ui` | `src/admin-ui/src/screens/{dashboard,transactions,broadcast-queue,addresses,tokens,alerts}/` (per screen: page + store + components) |
| `admin-ui-system-screens` | `admin-ui` | `src/admin-ui/src/screens/{p2p-pool,source-metrics,headers,broadcast-inspector,configuration,logs,providers,setup}/` |
| `admin-ui-tests` | `verification-and-conformance` | `src/admin-ui/src/**/*.test.tsx` · `src/admin-ui/tests/e2e/` (Playwright) · contract-mirror tests for the mock layer |
| `admin-ui-docs` | `repo-governance` | `docs/admin-ui/design-handoff/*` · `docs/admin-ui/design-bundle/*` · `docs/stream-tasks/admin-ui-vnext-program/*` |

## Slice Ledger

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S0 | foundation (project scaffold) | todo | — | `pnpm typecheck + lint + build` returns 0 errors against the empty shell; old `src/admin-ui/` deleted; new project boots at `src/admin-ui/` | empty Vite + React + TS + MUI app loads in dev; CI gates pass | slice-A1 |
| S1 | foundation (theme + tokens) | todo | S0 | `ConsigliereThemeConfig` from the design bundle ported to `src/app/theme.ts`; both palettes resolve; dense ↔ comfortable toggle works; theme persists via mobx-persist-store | tokens land 1:1 from `design-bundle/project/theme.jsx`; storybook-style theme demo route renders all palettes + densities | wave-A1 |
| S2 | foundation (shell + routing) | todo | S1 | AppBar + permanent Drawer (mobile temporary) + react-router-dom v7 + ThemeProvider + MobX root + persist-store; smart header search resolves tx/address/token/block by format and routes; sidebar reflects active route | shell renders empty screens for every route; theme toggle in header works; search routes to placeholder screen | slice-A1 |
| S3 | foundation (API + SignalR + mock) | todo | S2 | shared API client with `AppError` normalization; SignalR client with reconnect + per-widget stale callback; mock layer mirroring all admin DTOs; `VITE_API_MODE=real|mock` env switch | every admin REST endpoint + SignalR event has a typed client method + a mock-mode seed; integration test pins the mock-vs-real shape parity | slice-A1 |
| S4 | operator (Dashboard) | todo | S3 | composite hero + sparkline + search-with-recent-lookups + activity stream (recent broadcasts + per-source visibility feed) + 1-2 sparklines (mempool rate, pool size); 4 design variants match the bundle | dashboard renders against mock data identically to `frames-dashboard.jsx`; real mode lights up when backend is reachable | wave-A1 |
| S5 | operator (Entity detail: Tx / Address / Token) | todo | S3 | Tx detail with vertical `Stepper` mirroring the 5-stage `OutgoingTxState`; Address + Token share the timeline component; entity route is reached from header search | three entity types share one detail component; transitions stream over SignalR; matches `frames-tx-detail.jsx` | wave-A1 |
| S6 | operator (Broadcast Queue) | todo | S3 | 3-column kanban (Validated / Dispatching / PeerRelayed); framer-motion `AnimatePresence` on state move; stale-highlight >5min in Dispatching; Force-rebroadcast `Dialog` with confirmation | kanban moves cards via SignalR; Force-rebroadcast posts to backend + closes; matches `frames-broadcast.jsx` | wave-A1 |
| S7 | operator (Alerts) | todo | S3 | active alerts as `Card` stack (all 4 `P2pAlertType`); history `DataGrid` filterable by type + time range; new-alert toast (8s autohide); badge in header | active section + history journal match `frames-alerts.jsx`; toasts appear on new fires | wave-A1 |
| S8 | system (P2P Pool) | todo | S3 | peers `DataGrid` 60-70% with ScoreBar + per-component MiniBars; subnet/24 donut from `@mui/x-charts` 30-40%; matches `frames-p2p.jsx` | live peer telemetry; donut updates on poll; matches the bundle | wave-A1 |
| S9 | system (Source Metrics + Headers Chain + Broadcast Inspector) | todo | S3 | three screens following the brief: Source Metrics with full charts + per-source visibility cards; Headers Chain list + tip card + reorg log; Broadcast Inspector rawHex submit + lifecycle watcher | each screen calls the corresponding admin endpoint + handles errors per the AppError model | wave-A1 |
| S10 | system (Configuration + Logs/Raw + Providers + Setup) | todo | S3 | four screens following the brief; Configuration is read-only (tune affordance gated by feature-flag for future backend); Logs/Raw browser shows journal + raw document JSON viewer | screens render against mock + real; feature-flag for tune affordance present and OFF | wave-A1 |
| S11 | polish (a11y + perf) | todo | S4-S10 | WCAG AA pass on all 14 screens (axe-core or Lighthouse a11y); bundle size budget (initial JS < 300 KB gzipped); Lighthouse perf ≥ 90 on Dashboard cold load | a11y + perf audit reports green; budget assertion in CI | wave-A1 |
| S12 | closeout (e2e + deletion + closeout doc) | todo | S11 | Playwright e2e smoke for: header search → Tx detail → Force rebroadcast; Dashboard mounts; Alerts toast on injected event; old `src/admin-ui/` confirmed deleted (no orphan refs in repo); program closeout doc | `pnpm verify` green; `evidence/closeout.md` lists delivery hashes + residuals | wave-A1 |

S0 + S2 + S3 are the **foundational slices**; each has a
slice-level audit gating the next. S4-S10 are covered by the
program-level A1 plus normal per-slice review.

## Definition of Done

- All 12 slices `done`.
- `pnpm typecheck + lint + build + test + verify` returns 0
  errors at the program-close commit.
- Lighthouse a11y ≥ 95, perf ≥ 90, best-practices ≥ 95 on the
  Dashboard cold load.
- Initial JS bundle ≤ 300 KB gzipped (budget assertion in
  CI).
- All 14 screens reachable from the sidebar and render
  against both `VITE_API_MODE=real` (live Consigliere) and
  `VITE_API_MODE=mock`.
- Playwright e2e smoke green (the three flows in S12).
- `src/admin-ui/` legacy stub deleted; no orphan imports.
- `docs/stream-tasks/admin-ui-vnext-program/evidence/closeout.md`
  lists delivery hashes per slice, residuals, and a
  before/after screenshot.
- Two design-bundle deviations (`shape.borderRadius: 8` +
  `typography.code: JetBrains Mono`) recorded as accepted in
  the closeout.

## Delivery Notes

Commit hashes recorded here as slices close.

- Program package created: this commit (master draft).
- Pre-execution audit A1: pending.
