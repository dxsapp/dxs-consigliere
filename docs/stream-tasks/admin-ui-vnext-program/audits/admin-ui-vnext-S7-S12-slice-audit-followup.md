---
created: 2026-05-19
type: audit-followup
parent: admin-ui-vnext-S7-S12-slice-audit
status: applied
---

# Admin UI vNext — S7-S12 slice-audit followup (MAJOR REVISION → fixed)

Codex slice-audit on `0b89bc6`: MAJOR REVISION REQUIRED
(0C / 3H / 3M / 2L). All 8 findings closed in this commit.

## H1 — Playwright CI gate not wired

**Verified:** `.github/workflows/ci-tests.yml` only ran `pnpm
verify`; no Playwright install + run step.

**Revision applied:**

- New `admin-ui-e2e` workflow job: depends on `admin-ui`,
  installs Chromium with system deps (`pnpm test:e2e:install`
  → `playwright install chromium --with-deps`), runs
  `pnpm test:e2e` against both `chromium` + `mobile-chromium`
  projects, uploads `playwright-report` as an artifact on
  failure with 7-day retention.
- `pnpm test:e2e:install` now invokes
  `playwright install chromium --with-deps` so the CI runner
  also installs the system libs Playwright needs.

## H2 — Login-spec used a non-existent label

**Verified:** specs queried `getByLabel(/username/i)` but the
login form renders `<TextField label="Operator name">`
(`LoginPage.tsx:79`). Every Playwright spec timed out on the
first login step.

**Revision applied:**

- All four specs (`login-and-search`, `force-rebroadcast`,
  `alerts`, the new `alerts-toast`) now target
  `getByLabel(/operator name/i)`.

## H3 — Logs sanitizer leaked common secrets

**Verified:** `LogsPage.tsx:30` redacted only the first
non-whitespace token after `Authorization:` (`\S+`), leaving
`Bearer abc.def.ghi` intact. The API-key rule (`...:35`) only
covered double-quoted values, missing single-quoted JSON
fragments.

**Revision applied:**

- Authorization rule rewritten to redact through end-of-line
  (`/Authorization:[^\r\n]+/gi`).
- API-key rule split into double-quoted + single-quoted
  variants, both keeping the prefix verbatim so structured
  logs stay parseable.
- New regression tests pin both fixes: the bearer-token value
  must not appear in the output; a single-quoted apiKey value
  must not appear; the equals-form `api_key="…"` is also
  redacted while neighbouring `legacy=ok` survives.

## M1 — Alerts toast path was not covered by e2e

**Verified:** the only alerts spec asserted on the static
mock card. The audit prompt + S11 polish notes explicitly
required a toast e2e.

**Revision applied:**

- New `tests/e2e/alerts-toast.spec.ts`. In mock mode
  (`VITE_API_MODE=mock`) MockAdminClient bypasses HTTP, so a
  Playwright `page.route()` intercept can't inject a fake
  poll. The spec exercises the same code path: when the page
  mounts, the store reports the seeded alerts in
  `newSinceLastTick`, the page's drain effect spawns toasts,
  and the spec waits for `alert-toast-alert-001`.
- The spec covers both desktop + mobile projects (Pixel 5).

## M2 — S10 detail pages bypassed store-per-screen rule

**Verified:** Configuration / Providers / Setup made their
admin calls directly from `useEffect` with local React state.

**Revision applied:**

- New `screens/configuration/configuration.store.ts`,
  `screens/providers/providers.store.ts`,
  `screens/setup/setup.store.ts` — each follows the S5
  detail-store pattern: `{ data, status, error, start(),
  dispose() }`, abortable inflight, observable status.
- All three pages now `observer`-wrap the page, instantiate
  the store via `useMemo([admin])`, call `start()` in
  `useEffect`, and `dispose()` on unmount.
- New `configuration.store.test.ts` (3 cases) +
  `setup.store.test.ts` (2 cases). Providers shares the same
  shape as Configuration so its coverage rides on the
  Configuration tests.

## M3 — Playwright host alignment

**Verified:** `playwright.config.ts` waited on
`http://127.0.0.1:5173` while `pnpm dev` bound to the macOS
default `localhost`; the default Playwright run timed out on
server readiness.

**Revision applied:**

- New `HOST` + `PORT` constants drive both `webServer.command`
  and `webServer.url`/`use.baseURL`. The dev command is now
  `pnpm dev --host 127.0.0.1 --port 5173 --strictPort`, and
  the URL is built from the same constants so a regression
  cannot drift the host string.
- `fullyParallel: false` + `workers: 1` — the shared Vite dev
  server is a single mutable surface (MockAuthClient
  localStorage in particular). Serializing specs keeps the
  ~25 s wall clock and makes the suite deterministic.

## Store correctness — discovered in flight (also covered by M2 fix)

Folding the audit findings uncovered a related defect in all
the new S7-S10 stores + the prior S5 detail stores: a
permanent `disposed: boolean` field caused a permanent lockout
under React StrictMode (double-mount: first effect calls
`dispose()` → flag set → second effect's `start()` bailed →
page never received data).

- The `disposed` field is removed from
  `AlertsStore`, `P2pStore`, `HeadersStore`,
  `SourceMetricsStore`, `AddressDetailStore`,
  `TokenDetailStore`, `ConfigurationStore`, `ProvidersStore`,
  `SetupStore`.
- Idempotency for the polling stores now uses "timer
  set?" (DashboardStore pattern); detail stores rely on the
  `inflight?.abort()` race-guard alone.
- `start()` becomes safely re-runnable after `dispose()`.
- Verified live by the e2e suite: every authed page now
  hydrates against the mock seed under StrictMode.

## L1 — Closeout claims didn't match HEAD

**Verified:** `closeout.md` said "1.5k modules"; actual build
transforms 2.7k. It also claimed a Playwright e2e CI gate
that didn't exist (covered as H1).

**Revision applied:**

- Module count updated to 2.7k.
- CI workflow row added to the evidence table that names the
  three jobs (`backend`, `admin-ui`, `admin-ui-e2e`) and the
  Playwright artifact upload behaviour.

## L2 — A11y test didn't pin the alert count

**Verified:** AppHeader's alerts IconButton carried
`aria-label="alerts"`; the count only lived in the tooltip
title. The shell-a11y test asserted on `/alerts/i` which
matched the static label too.

**Revision applied:**

- AppHeader IconButton now sets
  `aria-label={`alerts (${alertCount} active)`}` so the
  accessible name surfaces the live count for screen readers
  in addition to the tooltip.
- Shell-a11y test now asserts the literal
  `/alerts \(0 active\)/i` pattern; a regression that drops
  the count from the label fails CI.

## MockAuthClient persistence (e2e prerequisite)

**Discovered while validating M3:** every `page.goto()` to an
authed route was a full SPA reload → fresh `RootStore.build()`
→ fresh in-memory `MockAuthClient` → AuthGuard bounced back to
/login. The e2e specs only worked when login + downstream
navigation happened on the same JS lifetime, which contradicts
the audit prompt's "navigate via `page.goto`" expectation.

**Revision applied:**

- `MockAuthClient` now persists `{ authenticated, username }`
  in localStorage (`consigliere-admin/mock-auth/v1`). State
  survives full reloads, mirroring real cookie-mode behaviour.
- `src/test-setup.ts` clears localStorage in an `afterEach`
  hook so unit tests stay hermetic across cases.

---

## Summary

| Layer | Change |
|---|---|
| `.github/workflows/ci-tests.yml` | NEW `admin-ui-e2e` job + report upload (H1) |
| `package.json` | `test:e2e:install` flag now `--with-deps` (H1) |
| `tests/e2e/*.spec.ts` | `getByLabel(/operator name/i)` (H2); explicit Suspense waits + Autocomplete-ambiguity click (H2) |
| `tests/e2e/alerts-toast.spec.ts` | NEW — toast lifecycle gate (M1) |
| `playwright.config.ts` | Host/port pinned via constants + `--strictPort` (M3); `fullyParallel: false` + workers: 1 |
| `src/screens/logs/LogsPage.tsx` | Authorization redacts through EOL (H3); api-key rule split into single + double quoted forms (H3) |
| `src/screens/logs/LogsPage.test.tsx` | New regressions for the bearer + single-quote + equals-form cases (H3) |
| `src/screens/configuration/configuration.store.{ts,test.ts}` | NEW (M2) |
| `src/screens/providers/providers.store.ts` | NEW (M2) |
| `src/screens/setup/setup.store.{ts,test.ts}` | NEW (M2) |
| `src/screens/configuration/ConfigurationPage.tsx` | observer + store-driven (M2) |
| `src/screens/providers/ProvidersPage.tsx` | observer + store-driven (M2) |
| `src/screens/setup/SetupPage.tsx` | observer + store-driven (M2) |
| `src/screens/*/{*.store.ts}` × 8 | `disposed` field removed; idempotency via timer/inflight (StrictMode fix) |
| `src/lib/mock/auth.ts` | localStorage persistence (e2e prerequisite) |
| `src/test-setup.ts` | `afterEach` clears localStorage (hermetic vitest) |
| `src/components/shell/AppHeader.tsx` | aria-label includes alert count (L2) |
| `tests/a11y/shell-a11y.test.tsx` | asserts on `/alerts \(N active\)/` regex (L2) |
| `docs/.../evidence/closeout.md` | module count + CI row updated (L1) |

Tests: 156 → 163 vitest green (31 files); Playwright 0 → 8
green (chromium + mobile-chromium); shell 194.44 KB gzip.

S7-S12 slice-gate cleared; wave-A1 closeout stands.
