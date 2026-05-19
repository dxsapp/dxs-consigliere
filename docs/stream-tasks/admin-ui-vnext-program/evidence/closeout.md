---
created: 2026-05-19
status: applied
program: admin-ui-vnext-program
---

# Admin UI vNext — closeout

Wave-A1 program closeout for the new operator admin UI. All 13
slices (S0 through S12) shipped + audited; the foundational
quartet (S0-S3) has signed-off slice audits, the operator wave
(S4-S6) has a signed-off slice audit with all 7 findings folded.

## Scope

13 React + MUI screens behind cookie auth, fed by a typed admin
REST + SignalR layer, plus a mock layer that mirrors every wire
DTO so the UI runs without a backend in tests and dev.

- Operator screens: Dashboard · Tx / Address / Token detail ·
  Broadcast Queue · Alerts
- System screens: P2P Pool · Source Metrics · Headers Chain ·
  Broadcast Inspector · Configuration · Logs/Raw · Providers ·
  Setup

## Sources of truth

| layer | path |
|---|---|
| Program plan | [`master.md`](../master.md) |
| Audit pack | [`audits/`](../audits/) |
| Design handoff | [`docs/admin-ui/design-handoff/`](/Users/imighty/Code/dxs-consigliere/docs/admin-ui/design-handoff/) |
| Frontend principles | `/Users/imighty/Code/docs/frontend-principles.md` |
| Backend DTOs (mirrored) | `src/Dxs.Consigliere/Dto/**` + `Controllers/Admin*.cs` |
| Hub contract | `src/Dxs.Consigliere/WebSockets/IWalletHub.cs` |
| Polish + perf evidence | [`s11-polish-notes.md`](./s11-polish-notes.md) |

## Validation commands

```bash
# From src/admin-ui
pnpm install
pnpm verify   # typecheck → lint → vitest → vite build → budget → inventory

# Playwright e2e (requires browser install once)
pnpm test:e2e:install
pnpm test:e2e
```

`pnpm verify` is the canonical gate. The chain ends with
`pnpm budget` (shell ≤ 200 KB gzip) and `pnpm inventory`
(per-route ≤ 250 KB gzip).

## CI evidence

| metric | value |
|---|---|
| Vitest test files | 31 |
| Vitest cases | 163 |
| Lint | clean (ESLint flat config; `no-console` allows warn/error) |
| TypeScript | clean (`tsc -b --noEmit`) |
| Vite build | ✓ 2.7k modules · per-route chunks split |
| CI workflow | `.github/workflows/ci-tests.yml` jobs: `backend`, `admin-ui` (pnpm verify), `admin-ui-e2e` (Playwright chromium + mobile-chromium, fails-CI on red, uploads `playwright-report` artifact) |
| Shell budget | 194.44 KB gzip / 200 KB cap (97.2% used) |
| DataGrid chunk | 128.23 KB gzip lazy |
| ChartsWrapper chunk | 59.00 KB gzip lazy |
| BroadcastQueuePage chunk | 47.01 KB gzip lazy (framer-motion) |
| Playwright config | `playwright.config.ts` boots `pnpm dev` with `VITE_API_MODE=mock` |
| Playwright specs | login-and-search, force-rebroadcast, alerts |

## Per-slice delivery

| slice | commit | summary |
|---|---|---|
| S0 | `379b7c4` | Vite scaffold + ESLint flat + CI split + ApiClient (401-redirect) |
| S1 | `3e854cb` + `af6c8c7` | Theme + density + mobx-persist hardened hydrate |
| S2 | `91f5de4` + `841690d` | Shell + drawer + AuthGuard + smart search grammar + active-route prefix |
| S3 | `274ee5e` + `d66caa7` | Auth client + SignalR + bus + mock + cleanup harness (11 findings folded) |
| S4 | `751f117` | Dashboard (hero + search + per-source feed + sparkline) |
| S5 | `5ec71b2` | Tx + Address + Token detail with shared EntityTimeline Stepper |
| S6 | `c9fb970` | Broadcast Queue kanban + Force-rebroadcast two-step dialog |
| S4-S6 audit | `6ce7965` | 7 findings folded (DTO drift × 2, dialog hardening, per-screen stores, shell budget script, master doc, act() wraps) |
| S7 | `93ffbd3` | Alerts page (active cards + lazy DataGrid history + poll-delta toasts) |
| S8 | `9535555` | P2P Pool (DataGrid + ScoreBar + lazy /24 donut) |
| S9 | `9531c29` | Source Metrics + Headers Chain + Broadcast Inspector (X-Charts lazy) |
| S10 | `56d1a19` | Configuration + Providers + Logs sanitizer + Setup |
| S11 | `dd663ee` | a11y invariants + bundle-inventory gate + polish notes |
| S12 | this commit | Playwright config + 3 e2e specs + closeout doc + master rows |

## Contract parity

The S3 slice deferred swagger codegen + an ASP.NET-host parity
test; that residual is recorded in
[`contracts/README.md`](/Users/imighty/Code/dxs-consigliere/src/admin-ui/contracts/README.md)
and scaffolded by `tests/contract/auth.test.ts`. Until that lands,
parity is enforced at the source level: every TS DTO is hand-
mirrored from the canonical C# DTO; the S4-S6 audit found and
fixed two drift cases (TrackedHistoryStatusResponse,
AdminTrackedTokenBalanceSummaryResponse), proving the manual
mirror process is reviewable.

## Mobile / a11y / perf reports

- a11y — shell-level invariants in
  [`tests/a11y/shell-a11y.test.tsx`](/Users/imighty/Code/dxs-consigliere/src/admin-ui/tests/a11y/shell-a11y.test.tsx);
  Snackbar + Alert + Stepper all render with MUI's built-in
  ARIA primitives. Full axe-core pass is the documented S12+
  residual.
- mobile QA — breakpoint matrix in
  [`s11-polish-notes.md`](./s11-polish-notes.md). 375 px Pixel-5
  profile is enabled in `playwright.config.ts`.
- perf — `pnpm budget` (shell) + `pnpm inventory` (per-route)
  enforce the A1 M3 ceilings on every build.

## Accepted design deviations

These deviations were flagged during the design handoff + then
explicitly accepted:

- `shape.borderRadius = 8` (design bundle used 4 — MUI default
  doubled for the operator-density feel).
- `typography.code` font family = JetBrains Mono (design bundle
  picked Inter Mono; the operator-side code blocks are dense, so
  JetBrains' tighter stack reads better at 13 px).

## Residuals → next wave

- Swagger codegen + ASP.NET-host contract-parity test (S3
  followup).
- Full axe-core CI step + Lighthouse perf score per route.
- Backend log streaming surface for Logs/Raw — currently the
  screen is a paste box + client sanitizer because the backend
  doesn't expose a streaming endpoint.
- Read-write Configuration screen (operators currently change
  config out-of-band) — explicitly deferred per design brief §7.

## Open assumptions

- Cookie auth is the canonical session mode; no other auth path
  is supported (e.g. token-based via Authorization header).
- The admin SignalR hub stays at `/ws/consigliere` and emits the
  three frozen events (`OnNewBlock`, `OnReorg`,
  `OnBroadcastStateChanged`).
- `MockAdminClient` shapes stay reviewable against the C# DTOs.
  A future codegen step is the long-term answer; manual review is
  the interim contract gate.

## Slice gate status

Wave-A1 gate cleared. The next wave (multi-chain extension,
read-write configuration, additional system screens) can branch
off `codex/consigliere-vnext` HEAD without an intervening freeze.
