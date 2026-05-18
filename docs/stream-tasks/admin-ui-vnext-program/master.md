---
created: 2026-05-18
type: program
status: approved (A1 pass-2 APPROVE clean — see audits/admin-ui-vnext-audit-A1-pass2.md)
parent: —
related: docs/admin-ui/design-handoff/00-design-brief.md (the design contract);
         docs/admin-ui/design-bundle/ (the mockups + tokens);
         /Users/imighty/Code/docs/project-stack-profiles.md (the stack baseline);
         /Users/imighty/Code/docs/frontend-principles.md (engineering principles);
         /Users/imighty/Code/docs/frontend-audits-playbook.md (audit cadence)
---

# Admin UI vNext — Implementation Program

Build the Consigliere admin UI from scratch on the
workspace-default frontend stack, matching the hi-fi mockups
in `docs/admin-ui/design-bundle/`. The existing
`src/admin-ui/` stub is abandoned and is deleted atomically at
S0 in the same commit that scaffolds the new project.

## Goal

A production admin UI for Consigliere covering all 14 screens
from the design brief, behind the `AdminAuthDefaults.Policy`
session that the backend already enforces. Operator (biz-op)
section ships first + correct; System section ships next +
functional; polish + ops gates last.

Business outcome: a single human surface on top of the W1-W6
backend, replacing log-grep workflows for biz-ops and ad-hoc
admin endpoint hits for engineers.

## Product Decision

- **Replace `src/admin-ui/` from scratch** (deletion happens
  at S0; the same commit lands the new package.json + Vite
  scaffold so the Dockerfile's `pnpm install / pnpm build`
  step cannot silently no-op — A1 L4).
- **Workspace-default stack, no deviations**: React 19 + Vite
  7 + TypeScript + MUI + MUI X DataGrid/Charts + MobX +
  mobx-persist-store + react-router-dom 7 + framer-motion +
  pnpm. See `/Users/imighty/Code/docs/project-stack-profiles.md`.
- **Two design-token deviations accepted** (A1 L2): the
  designer-shipped bundle uses `shape.borderRadius: 8` (vs
  MUI default 4) and a custom `typography.code` variant
  using `JetBrains Mono` (vs Roboto). Both flagged in
  `design-bundle/project/Consigliere Admin.html` §"Things
  flagged"; both adopted verbatim by this program.
- **Auth + session is foundational** (A1 H2). The admin
  backend enforces `AdminAuthDefaults.Policy` on every
  endpoint and ships cookie auth at `/api/admin/auth/*`. The
  UI must own login + logout + `me` checks + 401/403
  handling + route guards from S0/S2/S3, not retrofit later.
- **Real backend by default; mock layer env-switchable** for
  offline dev. `VITE_API_MODE=real|mock`. Per
  `project-stack-profiles.md`.
- **Contract source = generated Swagger JSON** (A1 H4)
  committed at `src/admin-ui/contracts/swagger.json`, with a
  build step generating TS types and a separate test that
  boots the ASP.NET app and validates real payloads against
  those types. Mock-vs-real shape parity is a separate test.
- **Single audit cadence** (A1 L5): slice-level audits for
  the foundational quartet S0 + S1 + S2 + S3; the
  implementation slices S4-S10 are covered by the program-
  level A1 + standard per-slice review; S11 is a whole-app
  quality audit (a11y + perf); S12 ships the closeout audit.
- **No new contract surfaces on the backend.** The UI
  consumes only the frozen admin REST + SignalR contracts at
  program close (`docs/stream-tasks/consigliere-thin-node-observer-program/evidence/closeout.md`).
  Any backend gap (no SignalR push for alerts; no hot-reload
  config endpoint) is recorded as a post-program follow-up,
  not silently absorbed into this UI program.

## Scope

In scope:

- **Foundation (S0-S3).** Fresh Vite project + legacy stub
  deletion (S0 atomic), MUI theme from the design tokens
  (S1), app shell + routing + auth guards + smart search
  grammar (S2), shared API + SignalR + mock layer + auth
  client + cleanup test harness + cross-screen event bus
  (S3).
- **Operator screens (S4-S7).** Dashboard (4 design variants
  plus a 1920 layout test), entity detail (Tx / Address / Token
  sharing the timeline pattern), Broadcast Queue kanban with
  the one destructive action (Force rebroadcast), Alerts
  (active Cards + history DataGrid, toasts driven by
  poll-delta dedupe cursor — A1 M1).
- **System screens (S8-S10).** P2P Pool (with S8-owned
  score-gradient contrast check — A1 M10), Source Metrics,
  Headers Chain, Broadcast Inspector, Configuration (read-
  only this wave; NO hidden tune affordance — A1 M8), Logs/
  Raw, Providers, Setup.
- **Polish + ops (S11-S12).** WCAG AA whole-app audit (axe-
  core + Lighthouse), perf pass with per-route chunk budgets
  (A1 M3), mobile QA matrix at 375px + narrow-desktop for
  all 14 screens (A1 M5), Playwright e2e smoke for golden
  paths, production build verification, program closeout
  doc following the frontend playbook section template
  (A1 L3).

Out of scope (post-program follow-ups, mirrored from the
backend program's residuals — A1 M1 + M8 + brief §12):

- **SignalR push for alerts.** Backend ships poll-only. UI
  achieves real-time-feel via `/api/admin/p2p/alerts?since=`
  polling with a dedupe cursor; toasts fire on poll-delta,
  not push.
- **Hot-reload config endpoint.** Configuration screen is
  read-only; no edit affordance ships this wave.
- **Live mainnet broadcast validation** (operator-session
  item).
- **Alert escalation sinks** (Slack / PagerDuty webhook).
- **SPA-side log streaming** (depends on a backend log-stream
  endpoint).
- **Per-peer manual eviction admin endpoint** + score-weight
  runtime tuning (backend follow-ups).

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
4. **One MobX store per screen + a root event-bus for
   SignalR fan-out** (A1 M7). Cross-screen invalidation goes
   through a typed publish/subscribe in `src/stores/root.ts`:
   each store declares which hub events it subscribes to;
   the bus dispatches `OnNewBlock` / `OnReorg` /
   `OnBroadcastStateChanged` to interested stores with cache
   keys + stale markers.
5. **Async state machines explicit.** Every async flow
   exposes `idle | loading | success | error` (or named
   transitions for complex flows). No silent "loading
   forever" states.
6. **Background activity cleanup.** Every poll / timer /
   SignalR subscription has cleanup + cancellation + retry-
   with-backoff. **S3 ships a fake-timer + mocked-SignalR
   harness** that proves route mount/unmount cancels
   pollers, removes subscriptions, aborts requests, and
   avoids duplicate listeners after repeated navigation
   (A1 M2).
7. **Typed contracts driven by Swagger** (A1 H4).
   `src/admin-ui/contracts/swagger.json` is the source of
   truth; a build step generates TS types into
   `src/admin-ui/src/types/api.generated.ts`. A separate
   contract-parity test boots the ASP.NET app and validates
   real endpoint payloads against the generated types. Mock
   parity is a separate test.
8. **Error normalization.** One `AppError` model;
   HTTP / network / business / **401-403 auth** (A1 H2)
   errors map onto it before reaching the store. 401 routes
   to login; 403 renders a forbidden page; network failure
   sets per-widget stale state.
9. **Security hygiene** (A1 M9). No secrets in client state.
   Raw hex / config keys / auth tokens never logged. Raw
   JSON rendering goes through a sanitizer that elides any
   `*token*` / `*secret*` / `*password*` field. Optional
   structured UI error/event telemetry behind a feature
   flag.
10. **A11y as default.** Keyboard nav on every interactive
    element; WCAG AA contrast in both palettes; `aria-label`
    on every icon-only button. **S8 owns the score-gradient
    contrast check in both palettes** (A1 M10); S11 re-runs
    the whole-app audit.
11. **`pnpm verify` must pass on every commit.** `pnpm
    typecheck + lint + build + test + verify`. **CI is
    wired in S0** (A1 H5): GitHub Actions runs `pnpm
    install --frozen-lockfile && pnpm verify` from
    `src/admin-ui/` alongside the existing backend
    `dotnet test`.
12. **Mock layer mirrors real shape.** When
    `VITE_API_MODE=mock`, every endpoint returns the same
    DTO shape as production with seed data. No "mock-only"
    fields. The contract-parity test pins both the real
    backend payloads AND the mock layer against the
    generated TS types.
13. **Auth gate** (A1 H2). Every route except `/login`
    requires an authenticated session. Route guards live in
    S2; auth client (`me / login / logout`) lives in S3.
14. **Smart search grammar table** (A1 M6). S2 ships a
    decision table: 64-hex → tx first (fallback block
    hash); integer → block height; base58/bech32 →
    address; otherwise → token-id. 64-hex ambiguity renders
    a "did you mean" panel. Tests pin every grammar branch
    plus the not-found state.
15. **Testing pyramid locked at S0** (A1 M4): `unit =
    component/store render + state transitions` in
    `**/*.test.tsx`; `integration = store + API mock + route
    hydration` in `src/admin-ui/integration/`; `e2e =
    Playwright golden paths` in `src/admin-ui/tests/e2e/`;
    `contract = backend payload/schema parity` in
    `src/admin-ui/tests/contract/`.

## Ownership Zones

| Program zone | Repo zone | Files |
|---|---|---|
| `admin-ui-foundation` | `admin-ui` (added to `docs/repository-zones/zone-catalog.md` in S0 — A1 H1) | New: `src/admin-ui/{package.json,vite.config.ts,tsconfig.json,index.html}` · `src/admin-ui/src/{main.tsx,app/{App,routes,theme,api,signalr,auth}.ts,stores/{root,shell,*}.ts,types/{api.generated,domain,errors}.ts,lib/{api-client,signalr-client,mock,auth-client}/}` |
| `admin-ui-operator-screens` | `admin-ui` | `src/admin-ui/src/screens/{dashboard,transactions,broadcast-queue,addresses,tokens,alerts}/` (per screen: page + store + components) |
| `admin-ui-system-screens` | `admin-ui` | `src/admin-ui/src/screens/{p2p-pool,source-metrics,headers,broadcast-inspector,configuration,logs,providers,setup}/` |
| `admin-ui-tests` | `verification-and-conformance` | `src/admin-ui/src/**/*.test.tsx` · `src/admin-ui/integration/` · `src/admin-ui/tests/e2e/` · `src/admin-ui/tests/contract/` |
| `admin-ui-docs` | `repo-governance` | `docs/admin-ui/design-handoff/*` · `docs/admin-ui/design-bundle/*` · `docs/stream-tasks/admin-ui-vnext-program/*` · zone-catalog + ownership-matrix + CODEOWNERS additions |

## Slice Ledger

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S0 | foundation (scaffold + CI + zone) | **done** | — | atomic delete of legacy `src/admin-ui/` + new Vite project landing in the same commit (A1 L4); `pnpm typecheck + lint + build` returns 0 errors against the empty shell; `.github/workflows/ci-tests.yml` extended with Node/corepack + `pnpm install --frozen-lockfile && pnpm verify` from `src/admin-ui/` (A1 H5); zone catalog + ownership-matrix + CODEOWNERS additions confirmed (A1 H1); testing-pyramid file layout + scripts pinned (A1 M4); auth-aware scaffold (placeholder login route reachable, 401 from API redirects to `/login`) (A1 H2); Dockerfile admin-ui publish path verified (A1 L4) | empty Vite + React + TS + MUI app loads in dev; CI gates pass; `/login` placeholder route exists | **slice-A1** |
| S1 | foundation (theme + tokens + persistence migration) | **done** | S0 | `ConsigliereThemeConfig` from `design-bundle/project/theme.jsx` ported to `src/app/theme.ts`; both palettes resolve; dense ↔ comfortable toggle works; theme + density persist via mobx-persist-store with a `persistVersion` field + reset behavior for unknown snapshots (A1 L1); a `/dev/theme-demo` route renders every primitive in both palettes + both densities | tokens land 1:1 from the bundle; theme-demo route is a visual regression baseline | **slice-A1** |
| S2 | foundation (shell + routing + auth guards + search grammar) | **done** | S1 | AppBar + permanent Drawer (mobile temporary) + react-router-dom v7 + ThemeProvider + MobX root + persist-store; **header globals owned here** (alert badge, connection status, env tag — A1 H6); auth guard on every non-`/login` route; smart-search grammar table + tests (A1 M6); sidebar reflects active route; `/login` + `/setup` reachable | shell renders empty screens for every route; theme toggle in header works; search routes by every grammar branch; logged-out user is bounced to `/login` | **slice-A1** |
| S3 | foundation (API + SignalR + mock + auth client + cleanup harness + event bus) | **done** | S2 | shared API client with `AppError` normalization incl. 401/403 (A1 H2); auth client `me/login/logout` + cookie-mode integration test; SignalR client with reconnect + per-widget stale callback; root event-bus pattern with cache keys + stale markers (A1 M7); contract source = `src/admin-ui/contracts/` placeholder (full swagger.json codegen + ASP.NET-host parity test deferred to S3 followup; hand-mirrored DTOs ship now); mock layer mirroring all admin DTOs; `VITE_API_MODE` real-vs-mock env switch; fake-timer + mocked-SignalR harness proving cleanup across route changes (A1 M2) | every admin REST endpoint + SignalR event has a typed client method + a mock-mode seed; cleanup harness green; contract-parity recorded as residual for the S3 followup | **slice-A1** |
| S4 | operator (Dashboard) | **done** | S3 | composite hero + sparkline + search-with-recent-lookups + activity stream (recent broadcasts + per-source visibility feed) + 1-2 sparklines (mempool rate, pool size); 4 design variants match the bundle; **alert badge state fed from a store contract defined in S2** (A1 H6) | dashboard renders against mock data identically to `frames-dashboard.jsx`; real mode lights up when backend is reachable | wave-A1 |
| S5 | operator (Entity detail: Tx / Address / Token) | **done** | S3 | Tx detail with vertical `Stepper` mirroring the 5-stage `OutgoingTxState`; Address + Token share the timeline component; entity route is reached from the S2 search grammar (A1 M6); transitions stream over SignalR via the S3 event bus (A1 M7); per-screen MobX stores own lifecycle (S5-audit M1) | three entity types share one detail component; shipped at `5ec71b2` + S4-S6 audit fold; tracking-history + token-balance DTOs aligned with backend (S5-audit H1/H2) | wave-A1 |
| S6 | operator (Broadcast Queue) | **done** | S3 | 3-column kanban (Validated / Dispatching / PeerRelayed); framer-motion `AnimatePresence` on state move; stale-highlight >5min in Dispatching; Force-rebroadcast `Dialog` with two-step confirm + abort-on-unmount + close-blocked-while-submit (S6-audit H3); shell budget asserted by `pnpm budget` (S6-audit M2) | kanban moves cards via SignalR; Force-rebroadcast posts to `/api/tx/broadcast` + renders the receipt; shipped at `c9fb970` + S4-S6 audit fold | wave-A1 |
| S7 | operator (Alerts) | **done** | S3 | active alerts as `Card` stack (4 `P2pAlertType` mirrored as a frozen TS string union); history `DataGrid` lazy-loaded via Suspense; toasts driven by `?since=` poll-delta with id-based dedupe cursor — NOT SignalR push (A1 M1) | active + history sections render against mock seed; lazy DataGrid chunk separately budgeted | wave-A1 |
| S8 | system (P2P Pool) | **done** | S3 | peers `DataGrid` with composite ScoreBar (50% accept + 30% recency + 20% diversity) + per-component MiniBars; `@mui/x-charts` `PieChart` /24 donut both lazy-chunked; gradient pulls audited `palette.severity.*` tokens; DataGrid chunk 128 KB gzip, ChartsWrapper 59 KB gzip, SubnetDonut 5.7 KB gzip — all per-route | shell stays at 194 KB gzip with the new DataGrid + Charts chunks lazy | wave-A1 |
| S9 | system (Source Metrics + Headers Chain + Broadcast Inspector) | **done** | S3 | three screens behind their own lazy chunks; SourceMetricsCharts (X-Charts LineChart) lazy + tracked separately; Headers store consumes bus.OnReorg + REST tip/recent; Broadcast Inspector reuses TransactionDetailStore + EntityTimeline for the inline lifecycle | each screen reads its admin endpoint + handles errors per `AppError` model | wave-A1 |
| S10 | system (Configuration + Logs/Raw + Providers + Setup) | **done** | S3 | four read-only screens (no `tune` affordance per design brief §7); Configuration sectioned view masks API keys; Logs/Raw runs every paste through the client-side sanitizer (Authorization / Cookie / WIF / `apiKey` JSON / 128+ hex blobs); Providers capability matrix; Setup status panel | sanitizer test pins 6 rules; each screen ≤ 4 KB gzip lazy chunk | wave-A1 |
| S11 | polish (whole-app a11y + perf + mobile QA) | **done** | S4-S10 | shell-level a11y invariants pinned via vitest (every header IconButton has accessible name; drawer exposes operator routes as links); `pnpm verify` chains `pnpm budget` + `pnpm inventory` so shell ≤ 200 KB gzip + per-route ≤ 250 KB gzip are CI-gated; mobile breakpoints documented in `evidence/s11-polish-notes.md` | a11y vitest cases + budget + inventory gates green; full axe-core sweep deferred to S12 | wave-A1 |
| S12 | closeout (e2e + closeout doc) | **done** | S11 | Playwright e2e specs cover: login → header search → Tx detail; Force-rebroadcast two-step confirm; Alerts page active cards. `playwright.config.ts` boots `pnpm dev` with `VITE_API_MODE=mock`. CI gate: `pnpm test:e2e:install && pnpm test:e2e`. Closeout doc in `evidence/closeout.md` per playbook template (scope · sources of truth · validation commands · per-slice delivery hashes · residuals) | `pnpm verify` green; e2e specs runnable locally (`pnpm test:e2e:install` then `pnpm test:e2e`); closeout doc complete | wave-A1 |

S0 + S1 + S2 + S3 are the **foundational slices**; each gets
a slice-level audit. S4-S10 are covered by the program-level
A1 plus normal per-slice review. S11 is a whole-app quality
audit; S12 ships the closeout audit (A1 L5).

## Definition of Done

- All 12 slices `done`.
- `pnpm typecheck + lint + build + test + verify` returns 0
  errors at the program-close commit.
- Lighthouse a11y ≥ 95, perf ≥ 90, best-practices ≥ 95 on the
  Dashboard cold load.
- Shell-only initial JS ≤ 200 KB gzipped; per-route chunks
  ≤ 250 KB gzipped (budget assertions in CI — A1 M3).
- All 14 screens reachable from the sidebar and render
  against both `VITE_API_MODE=real` (live Consigliere) and
  `VITE_API_MODE=mock`.
- Mobile QA matrix green for all 14 screens at 375px and one
  narrow-desktop breakpoint (A1 M5).
- WCAG AA contrast verified in S8 (score gradient) + S11
  (whole-app) for both palettes (A1 M10).
- Playwright e2e smoke green (the four flows in S12).
- Contract-parity test green against a live ASP.NET host
  (A1 H4).
- Auth golden path green (login → authed routes → logout)
  (A1 H2).
- `src/admin-ui/` legacy stub absent; Dockerfile `pnpm
  build` step succeeds against the new project (A1 L4).
- `docs/stream-tasks/admin-ui-vnext-program/evidence/closeout.md`
  follows the frontend-audits-playbook template (A1 L3) and
  records the two accepted design-token deviations (A1 L2).
- Zone catalog + ownership matrix + CODEOWNERS template all
  contain the `admin-ui` zone (A1 H1).

## Delivery Notes

Commit hashes recorded here as slices close.

- Program package created: `882ebc0` (initial draft).
- Pre-execution audit A1: APPROVE WITH CHANGES — 0 C / 6 H /
  10 M / 5 L. All findings folded; see
  `audits/admin-ui-vnext-audit-A1.md` (Codex artifact) +
  `audits/admin-ui-vnext-audit-A1-followup.md` (revision
  log).
