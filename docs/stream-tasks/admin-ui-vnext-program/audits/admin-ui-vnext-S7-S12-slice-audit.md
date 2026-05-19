# Admin UI vNext — S7-S12 + closeout slice audit

Reviewer: Codex
Date: 2026-05-19
Audit target: S7-S12 at `0b89bc6` on `codex/consigliere-vnext`

Scope: system-screens wave, polish, Playwright e2e, and closeout
evidence. The branch head includes this prompt at `fde41e3`; audit
checks use the requested S7-S12 range `93ffbd3^..0b89bc6`.

Zones:

- `admin-ui`: `src/admin-ui/**`
- `verification-and-conformance`: `src/admin-ui/src/**/*.test.*`,
  `src/admin-ui/tests/**`
- `repo-governance`: `docs/**`, `.github/**`

## Verification

- `git diff --name-only 93ffbd3^..0b89bc6 -- src/Dxs.Consigliere`:
  empty. S7-S12 did not touch backend C# code.
- `pnpm verify` from `src/admin-ui`: pass. 29 vitest files / 156
  cases passed; typecheck, lint, build, budget, and inventory passed.
- `pnpm inventory`: pass. Shell `index-C8_W-Eq1.js` is 194.44 KB
  gzip; single shared DataGrid chunk is 125.22 KB gzip by inventory
  gzip (Vite build report: 128.23 KB); single shared ChartsWrapper
  chunk is 57.62 KB by inventory gzip (Vite build report: 59.00 KB).
- `pnpm test:e2e:install`: pass after downloading Chromium.
- `E2E_BASE_URL=http://localhost:5173 pnpm exec playwright test
  --project=chromium`: fail. All three specs time out waiting for
  `getByLabel(/username/i)`.
- A default Playwright run also failed to reach specs in this local
  environment because the webServer became reachable on `localhost`
  while config waited on `127.0.0.1`.
- Commit hash spot-check: all closeout per-slice hashes resolve.
- Legacy admin reference check: no new screen imports or references
  `wwwroot/legacy-admin/**`.

## Checks That Passed

- S7/S8/S10 DTO mirrors for alerts, peers, providers, and setup match
  the referenced backend shape at source level:
  `P2pAlertEventDto`, `P2pAlertResponse`, all four `P2pAlertType`
  enum members, peer date fields as `string | null`,
  `AdminProvidersResponse` nested config/catalog fields, and
  `SetupStatusResponse`.
- Alerts polling is REST-only: `AlertsStore` advances a `since`
  cursor, dedupes by alert id, and `consumeNewAlerts()` drains the
  toast queue.
- P2P scoring implements the requested `0.5*accept + 0.3*recency +
  0.2*diversity` formula with 24h linear recency decay and binary
  `/24` diversity.
- Headers store subscribes to `OnReorg`, caps captured events at 50,
  and unwires the bus subscription in `dispose()`.
- Source metrics delta series returns `[]` with fewer than two
  snapshots and clamps negative deltas to zero.
- Broadcast Inspector creates the `TransactionDetailStore` only after
  receipt txid exists, disposes it when txid changes, and passes
  `store.stagesView` into the shared `EntityTimeline`.
- Configuration, Providers, Logs, and Setup are read-only surfaces;
  Broadcast Inspector is explicitly non-destructive per design brief
  §7.
- `makeAutoObservable(this, {}, { autoBind: true })` is used by the
  new S7-S9 stores.
- App route lazy imports and nested DataGrid/X-Charts imports are
  covered by Suspense boundaries.

## Findings

### H1 — The claimed S12 Playwright CI gate is not wired

Severity: HIGH
Slice: S12 / CI

Issue: `master.md` says the S12 CI gate is
`pnpm test:e2e:install && pnpm test:e2e`, and `closeout.md` lists
Playwright as part of the closeout evidence. The actual CI workflow
only installs dependencies and runs `pnpm verify`
(`.github/workflows/ci-tests.yml:52-56`). `pnpm verify` does not run
Playwright (`src/admin-ui/package.json:20`), and there is no
`pnpm test:e2e:install` step in CI.

Impact: The closeout can pass CI while every e2e spec is broken or
while the browser binary is missing. This directly invalidates the
S12 closeout gate.

Recommended fix: Extend the `admin-ui` job with
`pnpm test:e2e:install` followed by `pnpm test:e2e`, or add an
explicit e2e job. Keep the install step before test execution.

### H2 — The S12 e2e specs fail after browser install

Severity: HIGH
Slice: S12

Issue: After `pnpm test:e2e:install`, running
`E2E_BASE_URL=http://localhost:5173 pnpm exec playwright test
--project=chromium` starts the app and executes the specs, but all
three fail at the login step. The specs search for
`getByLabel(/username/i)` (`tests/e2e/login-and-search.spec.ts:18`,
`tests/e2e/force-rebroadcast.spec.ts:16`,
`tests/e2e/alerts.spec.ts:14`), while the actual login field label is
`Operator name` (`src/screens/login/LoginPage.tsx:78-80`).

Impact: None of the claimed S12 golden paths are currently proven:
login/search/Tx, Force-rebroadcast confirmation, or Alerts.

Recommended fix: Either change the login field label to include
"Username" or update the specs to target the actual accessible label,
for example `getByLabel(/operator name/i)`. Re-run Playwright in CI
after the H1 workflow fix.

### H3 — Logs sanitizer leaks common secrets

Severity: HIGH
Slice: S10

Issue: The sanitizer rule for Authorization only replaces the first
non-space token after the header name:
`/Authorization:\s*\S+/gi` (`src/screens/logs/LogsPage.tsx:30`).
For a realistic header `Authorization: Bearer abc.def.ghi`, the
output is `Authorization: *** abc.def.ghi`, leaving the bearer token
visible. The API key rule also only redacts double-quoted values
(`src/screens/logs/LogsPage.tsx:35`), so a single-quoted log fragment
like `{'apiKey':'abcdef'}` is not redacted, despite the audit prompt
requiring single + double quoted coverage.

Impact: The S10 screen is explicitly meant to sanitize traces before
sharing. These misses can leak auth tokens or API keys in exactly the
inputs operators are likely to paste.

Recommended fix: Redact the full Authorization header value to end of
line, and support both quote styles around key names and values. Add
tests that assert the bearer token and single-quoted `apiKey` value do
not remain in output.

### M1 — Alerts e2e does not cover the required poll-delta toast

Severity: MEDIUM
Slice: S12 / S7

Issue: The S11 notes list "Alerts toast on injected poll-delta" as an
S12 residual, and the audit prompt asks to flag if the toast spec is
missing. The only alerts e2e spec is
`alerts page renders active cards`; it checks the page heading, Active
and History sections, and static mock card `alert-001`
(`tests/e2e/alerts.spec.ts:12-25`). It never injects or waits for a
poll delta and never asserts a Snackbar.

Impact: The central S7 behavior, "toasts driven by `?since=` poll
delta", is covered by unit tests but not by the closeout smoke that
claims to cover operator golden paths.

Recommended fix: Add a Playwright route/mock step that returns an
empty first alerts page, then a newer alert on the next poll, and
assert `data-testid="alert-toast-..."` appears and auto-closes.

### M2 — S10 data screens bypass the MobX screen-store discipline

Severity: MEDIUM
Slice: S10

Issue: `ConfigurationPage`, `ProvidersPage`, and `SetupPage` call
`admin.getProviders()` / `admin.getSetupStatus()` directly from
page-level `useEffect` with local React state
(`ConfigurationPage.tsx:22-41`, `ProvidersPage.tsx:21-40`,
`SetupPage.tsx:26-45`). This repeats the S4-S6 audit's store-layer
concern and diverges from the program's layered architecture and
one-store-per-screen rule.

Impact: The pages do abort on unmount, so this is not an immediate
leak. The risk is architectural drift: no idempotent store entrypoint,
no reusable async state machine, and no focused store tests for these
three S10 screens.

Recommended fix: Extract read-only `ConfigurationStore`,
`ProvidersStore`, and `SetupStore` or one shared provider-config store
with explicit `start()/dispose()` and focused tests. Pages should only
render observable state.

### M3 — Playwright default baseURL/webServer pairing is brittle

Severity: MEDIUM
Slice: S12

Issue: `playwright.config.ts` waits on
`http://127.0.0.1:5173` by default (`playwright.config.ts:18`,
`:30`), but the webServer command is just `pnpm dev`
(`playwright.config.ts:29`), which starts Vite on its default
`localhost` binding. In this macOS environment Vite listened on
`[::1]:5173`, and the default Playwright run timed out waiting for
`127.0.0.1:5173`. Overriding `E2E_BASE_URL=http://localhost:5173`
got past server startup.

Impact: The e2e command is not reliably runnable with default config
on the same developer workstation used for the audit.

Recommended fix: Make the server and URL use the same host. For
example, set `baseURL`/`webServer.url` to `http://localhost:5173`, or
run `vite --host 127.0.0.1` from the webServer command.

### L1 — Closeout evidence has stale or over-claimed validation text

Severity: LOW
Slice: S12 docs

Issue: `closeout.md` says the Vite build is "1.5k modules"
(`closeout.md:62`), but the current `pnpm verify` build transforms
2737 modules. The same closeout/master evidence claims an e2e CI gate
that does not exist (covered as H1).

Impact: The document is directionally useful, but not every assertion
verifies against HEAD as required by the prompt.

Recommended fix: Update the module count from current build output
and either remove or implement the claimed CI e2e gate.

### L2 — Shell a11y test does not pin the alert count in the accessible name

Severity: LOW
Slice: S11

Issue: The S11 a11y test comment says the alert badge IconButton
carries the live count in its accessible name via tooltip
(`tests/a11y/shell-a11y.test.tsx:17-20`), but the actual assertion
only checks for `/alerts/i` (`tests/a11y/shell-a11y.test.tsx:67-70`),
and the button's explicit `aria-label` is just `"alerts"`
(`src/components/shell/AppHeader.tsx:104-105`). The tooltip title
does contain the count, so this is a test/document sharpness issue,
not a visible UI failure.

Impact: A regression could remove the count from the accessible label
path without failing the current S11 test.

Recommended fix: Either include the count in `aria-label`, or update
the S11 test/comment to assert only the tooltip text if that is the
intended contract.

## Verdict

Verdict: MAJOR REVISION REQUIRED
Critical findings: 0
High findings: 3
Medium findings: 3
Low findings: 2
Headline: DTOs, stores, bundle gates, and lazy chunks mostly hold, but wave-A1 cannot close while Playwright is not CI-gated, the shipped specs fail, and the Logs sanitizer leaks common secrets.
