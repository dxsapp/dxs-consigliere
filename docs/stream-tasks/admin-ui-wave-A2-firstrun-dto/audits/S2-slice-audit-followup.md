---
created: 2026-05-19
type: slice-audit-followup
parent: docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/audits/S2-slice-audit-prompt.md
status: applied
---

# wave-A2 S2 slice-audit followup (MAJOR REVISION REQUIRED)

Codex slice-audit on `fbdff38`: MAJOR REVISION REQUIRED
(0C / 2H / 2M / 2L). Five findings closed in this fold;
L2 logged as a wave-A3 residual.

## H1 — Host lifetime was unreliable: `process.on("beforeExit")`

**Verified:** `_session.ts` registered a `beforeExit`
listener to stop the dotnet child process. When the dotnet
child keeps the Node event loop alive (e.g. lingering
sockets, IPC pipes), `beforeExit` never fires and the
backend leaks across vitest runs. The auditor reproduced
the leak with `pkill -0 -f Dxs.Consigliere.dll` after a
clean exit.

**Revision applied:**

- New `tests/contract/_global-setup.ts` exposes vitest's
  `setup` / `teardown` hooks. It spawns the ASP.NET host in
  the main process and calls `provide("contractHostBaseUrl",
  baseUrl)` so every fork can `inject(...)` the URL without
  re-spawning.
- `vitest.contract.config.ts` registers the file via
  `globalSetup: ["./tests/contract/_global-setup.ts"]`.
  Vitest's globalTeardown contract is reliable across all
  exit paths (incl. SIGINT / failed tests) — proven by the
  vitest source's `try { ... } finally { teardown() }`
  wrapper.
- `_session.ts` no longer spawns the backend. It only reads
  `inject("contractHostBaseUrl")`, mints a fresh cookie jar,
  and the per-fork `stop` is a no-op (lifetime belongs to
  globalSetup).
- Manually verified post-fix: `pgrep -f Dxs.Consigliere.dll`
  reports zero processes after `pnpm test:contract` exits
  (clean, failing-test, or Ctrl-C).

## H2 — Tracked-address/token shape gate was vacuous on a fresh DB

**Verified:** `admin.test.ts` lines 76 + 86 called
`/api/admin/tracked/addresses` + `/api/admin/tracked/tokens`
against a clean RavenDB; both returned `[]` so the
`expectShape(...)` loops never ran. Worse, the screens the
UI actually mounts (TrackedAddressesPage, TrackedTokensPage)
hit the **detail** endpoints `/api/admin/tracked/address/
{address}` + `/api/admin/tracked/token/{tokenId}` — neither
was covered at all. A backend rename of either DTO would
land green.

**Revision applied:**

- New `ensureSeededEntities(host)` helper in `_session.ts`
  posts a tracked address + tracked token via the real
  `POST /api/admin/manage/address` + `POST /api/admin/
  manage/stas-token` endpoints. Idempotent against re-runs
  (409 = "already registered" → success).
- Address fixture: `1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa`
  (shared with the C# test suite — Dxs.Bsv.Tests,
  Dxs.Consigliere.Tests).
- TokenId fixture: `33...33` (40 hex chars) — shared with
  DSTAS C# fixtures.
- `admin.test.ts` now has 4 describes covering tracked
  entities: list (both list endpoints assert non-empty,
  so `expectShape` runs on at least one element) AND
  detail (`/address/{address}` + `/token/{tokenId}`).
- HistoryPolicy mode set to `forward_only` so the seed
  doesn't queue a full-history backfill.

## M1 — Login response shape was not contract-tested

**Verified:** `_session.ts` calls `POST /api/admin/auth/
login` during setup but discards the response body. The
auth shape was only checked via the cookie-renew path
(`GET /api/admin/auth/me`). A backend rename inside the
login response would not fail the gate.

**Revision applied:**

- New describe in `auth.test.ts`:
  `POST /api/admin/auth/login returns AdminAuthStatusResponse`.
  Uses a fresh anonymous `HostHandle` (empty cookie jar)
  so we exercise the login path, not a cookie renew.
- Asserts `expectShape("AdminAuthStatusResponse", body)`
  + `body.authenticated === true` +
  `body.username === ADMIN_CREDENTIALS.username`.

## M2 — Setup wizard responses were not contract-tested

**Verified:** `_session.ts` called `GET /api/setup/options`
and `POST /api/setup/complete` but threw the bodies away.
The first call is the wizard's *first wire* — a rename of
`defaults` / `providerConfig` would silently break wizard
render without failing this gate.

**Revision applied:**

- `completeSetupIfNeeded` now reads the options body first
  + captures it into module-scoped `setupOptionsResponse`.
- The complete-call body is captured into
  `setupCompletionResponse`.
- New describe in `auth.test.ts` — `setup contract parity`:
  - `GET /api/setup/options matches SetupOptionsResponse`
  - `POST /api/setup/complete returns SetupStatusResponse`
- Bodies are exported via `setupOptionsBody()` +
  `setupCompletionBody()` so we don't have to seed a
  second admin per spec.

## L1 — `EnabledTasks: []` did not clear the inherited array

**Verified:** `.NET configuration array binding merges by
INDEX`, not by replacement — `appsettings.Test.json`'s
`EnabledTasks: []` adds zero new entries but leaves the
base appsettings.json's 8-entry list intact. Every periodic
task was actually running against the contract-test
RavenDB, slowing the suite + producing noisy logs.

**Revision applied:**

- `Dxs.Common.BackgroundTasks.BackgroundTasksConfig` gains
  a `DisableAll: bool` kill switch. Default `false` → no
  behaviour change for prod.
- `PeriodicTask.IHostedService.StartAsync` short-circuits
  to `Task.CompletedTask` when `DisableAll` is set.
- `appsettings.Test.json` + `appsettings.DockerComposeE2E.
  json` both replaced `EnabledTasks: []` with
  `DisableAll: true`.

## L2 — AJV schemas permissive (logged as wave-A3 residual)

**Verified:** the AJV gate registers schemas with
`strict: false` and the Swashbuckle-emitted schemas
themselves carry no `required` array (Swashbuckle only
infers `required` from `[Required]` attributes, which the
C# DTOs don't currently use — NRT annotations don't yet
propagate). Practically this means a field that vanishes
from the response is NOT caught — only renames /
type-changes are.

**Status:** **NOT folded in S2.** Wave-A3 S6 is the
proper landing site (`Swashbuckle NRT inference + screen
migration`); the filter that walks NRT annotations to
populate `required` arrays IS the work that closes this.

Recorded as a residual in the wave-A2 closeout + carried
into wave-A3 ledger.

## Result

5 of 6 findings folded inside the same `codex/
consigliere-vnext` branch. The contract-parity gate now:
- spawns + tears down the backend in the main process
  (no orphan dotnet processes)
- shape-checks the populated tracked-address + tracked-
  token list AND detail endpoints
- shape-checks `POST /api/admin/auth/login` directly
  (anonymous round-trip, no cookie reuse)
- shape-checks `GET /api/setup/options` +
  `POST /api/setup/complete`
- runs against a backend with all 8 periodic tasks
  actually disabled

L2 (AJV `required` inference) is wave-A3 S6's mandate.
