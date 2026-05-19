---
created: 2026-05-19
type: wave-slices
parent: docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/master.md
---

# Wave A2 — Slice decomposition

## Overview

Three slices, strictly sequential. S0 ships the wizard
against the existing hand-mirrored types. S1 swaps those
hand-mirrored types for generated ones (no behavioral
change). S2 hardens the contract gate with a real backend
boot.

Each slice is shipped via the wave-A1 audit cadence:
prompt → execute → audit → followup fold → commit. Audit
prompts live at `audits/S<n>-slice-audit-prompt.md`.

---

## S0 — Public `/setup` wizard

### Intent
Replace the hand-typed `curl POST /api/setup/complete` step
with a 4-step form that any operator can finish in <2 min.

### Owned paths
- `src/admin-ui/src/screens/setup-wizard/SetupWizardPage.tsx`
- `src/admin-ui/src/screens/setup-wizard/steps/Step1AdminAccess.tsx`
- `src/admin-ui/src/screens/setup-wizard/steps/Step2Providers.tsx`
- `src/admin-ui/src/screens/setup-wizard/steps/Step3BlockSync.tsx`
- `src/admin-ui/src/screens/setup-wizard/steps/Step4Review.tsx`
- `src/admin-ui/src/screens/setup-wizard/setup-wizard.store.ts`
- `src/admin-ui/src/screens/setup-wizard/setup-wizard.store.test.ts`
- `src/admin-ui/src/screens/setup-wizard/SetupWizardPage.test.tsx`
- `src/admin-ui/src/app/App.tsx` (new `<Route path="/setup">` outside `AuthGuard`)
- `src/admin-ui/src/lib/admin/admin-client.ts` (new `getSetupOptions(signal)` + `completeSetup(req, signal)`)
- `src/admin-ui/src/types/admin.ts` (new TS mirrors for `SetupOptionsResponse` + `SetupCompleteRequest` + nested DTOs)
- `src/admin-ui/src/lib/api/routes.ts` (new `SETUP_OPTIONS_PATH`, `SETUP_COMPLETE_PATH`)
- `src/admin-ui/tests/e2e/setup-wizard.spec.ts` (new)
- `src/admin-ui/src/lib/mock/admin.ts` + `admin-systems-seed.ts` (mock `completeSetup` that flips `setupRequired=false`)

### Exact task
1. Mirror `SetupOptionsResponse` (already used by wave-A1's
   `SetupStatusResponse`) + add the nested
   `SetupDefaultsResponse`, `SetupAllowedOptionsResponse`,
   `SetupJungleBusBlockSyncDefaultsResponse`,
   `SetupProviderFormDefaultsResponse` + per-provider
   variants from `src/Dxs.Consigliere/Dto/Responses/Setup/
   SetupStatusResponse.cs`.
2. Mirror `SetupCompleteRequest` + nested
   `SetupAdminAccessRequest`, `SetupProviderSelectionRequest`,
   `SetupJungleBusBlockSyncRequest`,
   `SetupNodeRealtimeConfigRequest`, plus the existing
   `AdminBitailsProviderConfigUpdateRequest`,
   `AdminRestProviderConfigUpdateRequest`,
   `AdminJungleBusProviderConfigUpdateRequest` from
   `src/Dxs.Consigliere/Dto/Requests/SetupCompleteRequest.cs`
   + `AdminProviderConfigUpdateRequest.cs`.
3. Build `SetupWizardStore` (MobX, autoBind, abortable
   inflight — wave-A1 detail-store pattern, NO permanent
   `disposed` flag per S4-S6 audit fold). Fields:
   - `status: "loading" | "ready" | "submitting" | "submitted" | "error"`
   - `options: SetupOptionsResponse | null`
   - `step: 1 | 2 | 3 | 4`
   - per-step form state objects
   - `error: string | null`
   - `submittedStatus: SetupStatusResponse | null`
   - methods: `start()` (fetch options), `setStep(n)`,
     `setAdminField(k,v)`, `setProvidersField(k,v)`,
     `setBlockSyncField(k,v)`, `submit()`, `dispose()`
4. Validation rules (client-side, NOT server-side — the
   backend already validates):
   - Step 1: username ≥ 3 chars, password ≥ 8 chars,
     confirm matches.
   - Step 2: each selected provider must be in
     `options.allowed.*Providers`; bitailsTransport in
     `options.allowed.bitailsTransports`; URLs (when
     populated) parse as `new URL()`.
   - Step 3: `blockSubscriptionId` non-empty (mirrors
     backend rule at `SetupWizardService.cs:111`).
   - Step 4: review is read-only; "Confirm" enables only
     if all prior steps pass.
5. App.tsx wiring: a NEW top-level route at `/setup` OUTSIDE
   `AuthGuard` (but INSIDE `ThemeProvider`). On mount,
   probe `GET /api/setup/status`; redirect to `/login` if
   `setupCompleted === true`.
6. LoginPage extension: when the auth status surfaces
   `setupRequired: true`, render an explicit "Go to setup"
   button that navigates to `/setup`. Existing
   `Setup required — finish first-run configuration before
   signing in` banner stays, but now has an actionable CTA.
7. Mock layer (`MockAdminClient`): track an in-memory +
   localStorage-persisted `setupCompleted` flag; on
   `completeSetup()` flip it; on `getSetupStatus()` return
   the flipped state.
8. Tests:
   - `setup-wizard.store.test.ts` (8+ cases): hydrate
     options, step navigation guards, validation each
     branch, submit success, submit network error, submit
     `409 setup_already_completed`, dispose mid-flight
     aborts.
   - `SetupWizardPage.test.tsx` (4+ cases): step renders,
     can advance, submit calls admin client, redirects on
     success.
   - `tests/e2e/setup-wizard.spec.ts`: fresh-state test
     (clears localStorage), visits `/`, gets bounced to
     `/setup`, walks through all 4 steps, submits, lands
     on `/login`, completes login with the new admin
     account.

### What not to do
- Don't change `POST /api/setup/complete` shape. Don't
  rename fields. Don't add fields the backend doesn't read.
- Don't move the wizard inside `AuthGuard`. It MUST be
  public — that's the chicken-and-egg fix.
- Don't add a "skip setup" affordance. The form is the
  install procedure; skipping it means the operator
  bypassed admin-auth, which is a security bug.
- Don't delete the existing `/setup` SCREEN under
  `src/admin-ui/src/screens/setup/` (the wave-A1 read-only
  status panel). That's reached via the drawer post-auth
  and is a different artifact. Rename the new screen if
  needed to disambiguate (`screens/setup-wizard/`).

### Validation
- `pnpm verify` (typecheck + lint + vitest + build + budget
  + inventory) green
- `pnpm test:e2e` — new spec passes on chromium + mobile-
  chromium projects
- `docker compose down -v && docker compose up --build` →
  fresh RavenDB volume → visit `http://localhost:5000` →
  bounced to `/setup` → walk wizard → land on `/login` →
  log in successfully

### Completion signal
First-run install from `git clone` to "signed in" with
zero curl invocations + zero docs outside the form.

### Audit
Slice audit prompt at
`audits/S0-slice-audit-prompt.md`; fold findings in
`audits/S0-slice-audit-followup.md`.

---

## S1 — Swagger codegen

### Intent
Move every TS DTO that mirrors a C# DTO under
`api.generated.ts`. Make hand-edits of that file impossible
without a CI red.

### Owned paths
- `src/admin-ui/contracts/swagger.json` (gitignored runtime
  artifact; committed snapshot lives next to it as
  `swagger.snapshot.json` for the drift gate)
- `src/admin-ui/contracts/openapi-typescript.config.ts`
- `src/admin-ui/src/types/api.generated.ts`
- `src/admin-ui/src/types/admin.ts` (refactored — re-exports
  generated wire DTOs; hand-mirrored helpers stay)
- `src/admin-ui/src/types/auth.ts` (same)
- `src/admin-ui/scripts/check-contracts.mjs`
- `src/admin-ui/package.json` scripts: `contracts:generate`,
  `contracts:check`
- `src/Dxs.Consigliere/Program.cs` (new `--emit-swagger`
  CLI arg — exits after writing spec)
- `src/Dxs.Consigliere/Setup/PublicApiSetup.cs` (Swashbuckle
  wiring; only when in `Test` / `Development` env OR when
  `--emit-swagger` is passed)
- `src/Dxs.Consigliere/Dxs.Consigliere.csproj` (add
  `Swashbuckle.AspNetCore` package reference)

### Exact task
1. Add `Swashbuckle.AspNetCore` (latest, currently 7.x) to
   `Dxs.Consigliere.csproj`.
2. Wire Swashbuckle in `PublicApiSetup.cs` behind a config
   flag — production build does NOT expose `/swagger` UI.
3. Add `--emit-swagger <path>` CLI arg in `Program.cs`:
   builds the host, calls
   `app.Services.GetRequiredService<Swashbuckle.AspNetCore.
   Swagger.ISwaggerProvider>().GetSwagger("v1")`, writes
   JSON to the given path, exits 0.
4. Bake the swagger emit into `pnpm contracts:generate`:
   - `dotnet run --project src/Dxs.Consigliere -- --emit-swagger src/admin-ui/contracts/swagger.json`
   - `npx openapi-typescript src/admin-ui/contracts/swagger.json -o src/admin-ui/src/types/api.generated.ts`
5. `pnpm contracts:check` script
   (`scripts/check-contracts.mjs`):
   - re-runs emit + typegen into a temp dir
   - byte-diffs against the checked-in
     `src/admin-ui/src/types/api.generated.ts`
   - exits 1 on mismatch, printing the smallest meaningful
     diff
6. Refactor `types/admin.ts` + `types/auth.ts`:
   - re-export each wire-shape DTO from
     `api.generated.ts` via `export type { X } from './api.generated'`
   - keep hand-mirrored helpers (e.g. `OutgoingTxState`
     union literal, `TX_HAPPY_PATH` constant) — these are
     NOT wire shapes, they're domain helpers
   - delete the hand-mirrored DUPLICATES (e.g.
     `AdminTrackedAddressResponse` interface bodies)
7. Extend `pnpm verify` chain to end with
   `pnpm contracts:check`.

### What not to do
- Don't expose `/swagger` UI in production. The OpenAPI
  endpoint is for codegen tooling, not for runtime
  introspection.
- Don't delete the hand-mirrored helpers in `types/`. They
  layer business semantics (state machines, enum unions,
  derived constants) on top of wire shapes.
- Don't auto-regenerate `api.generated.ts` in
  `pre-commit`. The file is committed; CI fails on drift.
  Auto-regen would silently hide intentional rejections.
- Don't change ANY runtime behavior. S1 is a refactor with
  a new CI gate. Existing screens compile + test green
  without touching them (modulo type imports).

### Validation
- `pnpm contracts:generate` followed by `git diff
  src/admin-ui/src/types/api.generated.ts` shows zero diff
- `pnpm contracts:check` exits 0
- Manually edit `api.generated.ts` (add a fake field) →
  `pnpm contracts:check` exits 1 with a clear diff
- `pnpm verify` green
- Bundle inventory unchanged — generated types are
  erased; no shell-budget impact

### Completion signal
`grep -rn "interface Admin\|interface P2p\|interface
Source\|interface Setup" src/admin-ui/src/types/{admin,auth}.ts`
returns ZERO matches — every wire DTO comes from
`api.generated.ts`.

### Audit
Slice audit prompt at
`audits/S1-slice-audit-prompt.md`; fold findings in
`audits/S1-slice-audit-followup.md`.

---

## S2 — ASP.NET-host parity test

### Intent
A backend rename / type change must fail CI **on a real
backend boot**, not just on a TS-compile check.

### Owned paths
- `src/admin-ui/tests/contract/auth.test.ts` (un-skipped +
  expanded)
- `src/admin-ui/tests/contract/admin.test.ts` (new — covers
  every admin endpoint surface)
- `src/admin-ui/tests/contract/_host-harness.ts` (new —
  spawn/teardown helper)
- `src/admin-ui/vitest.contract.config.ts` (existing
  scaffold — extend to inject host port + setup hooks)
- `.github/workflows/ci-tests.yml` (new
  `admin-ui-contracts` job that depends on `backend` +
  `admin-ui`)

### Exact task
1. Backend test mode (small backend change, allowed in
   this slice because it's verification-and-conformance
   scope):
   - `Program.cs` new arg `--test-mode --port <N>` that
     boots a hermetic in-memory backend with an embedded
     RavenDB (use `Raven.Embedded` if already a dep, OR
     point to an env-var-configured RavenDB).
   - Skip background tasks in test-mode (the harness only
     needs the HTTP surface to introspect DTOs).
2. Host harness (`_host-harness.ts`):
   - `beforeAll`: `child_process.spawn("dotnet", ["run",
     "--project", ..., "--test-mode", "--port", "0"])`,
     parse stdout for the assigned port.
   - `afterAll`: SIGTERM + wait for exit.
   - Exposes `baseUrl: string`.
3. Per-endpoint parity test (one `describe` block per
   endpoint, ~10 endpoints total):
   - Hit endpoint with a known fixture (login first for
     authed endpoints).
   - Parse response JSON.
   - Validate against generated TS type via a runtime
     type-guard (`ajv` against the OpenAPI schema, OR a
     hand-written `assertShape<T>` helper that walks the
     generated type definition).
   - Test fails red on extra fields, missing required
     fields, wrong nullability.
4. CI job `admin-ui-contracts`:
   - Depends on `backend` + `admin-ui` (so both have
     restored deps).
   - `pnpm contracts:check`
   - `pnpm test:contract`
   - Uploads `swagger.json` + `api.generated.ts` as
     artifacts for inspection.

### What not to do
- Don't share state across endpoints. Each test boots a
  fresh database OR resets state before it runs.
- Don't validate response BODIES — only shapes. The wave-A1
  DTO drift was structural; field values are out of scope
  for parity.
- Don't run the parity test in `pnpm verify`. It needs a
  spawned backend, which is too heavy for the local
  iteration loop. Keep it in `pnpm test:contract` + the
  dedicated CI job.
- Don't add a `pnpm test:contract:install` script.
  `dotnet` is assumed present on the CI runner (it
  already is for the `backend` job).

### Validation
- `pnpm test:contract` against a freshly built backend
  passes locally
- CI's new `admin-ui-contracts` job is green
- Manually break a DTO (e.g. rename
  `P2pHealthDto.PoolSize` → `Size` in C#) → CI's
  `admin-ui-contracts` job goes red with a clear
  diff/assertion

### Completion signal
Wave-A1's S4-S6 audit H1 + H2 drift defects could not be
introduced again without CI failing first.

### Audit
Slice audit prompt at
`audits/S2-slice-audit-prompt.md`; fold findings in
`audits/S2-slice-audit-followup.md`.

---

## Dependency Order

```
S0 (wizard) ─→ S1 (codegen) ─→ S2 (parity test)
```

- S1 depends on S0 only weakly — S0 introduces new TS DTOs
  for the wizard, which S1 then makes generated. Could be
  inverted (codegen first, wizard second), but landing S0
  first lets us validate the wave's headline UX with a
  human before swapping the type system underneath.
- S2 depends on S1 — the parity test needs the generated
  types as the assertion target.

## Validation Matrix

| slice | local validation | CI gate |
|---|---|---|
| S0 | `pnpm verify` + `pnpm test:e2e` + manual fresh-volume install | existing `admin-ui` + new `admin-ui-e2e` jobs |
| S1 | `pnpm contracts:generate` byte-clean + `pnpm contracts:check` exits 0/1 | new `pnpm contracts:check` step in `admin-ui` job |
| S2 | `pnpm test:contract` against locally-built backend | new `admin-ui-contracts` job |

## Closeout Requirements

- All 3 slices `done`; each has a corresponding
  `audits/S<n>-slice-audit-followup.md` recording verdict
  + folded findings.
- `audits/A1.md` summarises wave-level closeout: what
  landed, validation evidence, residuals.
- `evidence/closeout.md` follows the wave-A1 playbook
  template (scope · sources of truth · validation commands
  · per-slice delivery hashes · residuals).
- Update `docs/stream-tasks/admin-ui-vnext-program/evidence/
  closeout.md` residuals list: tick "Swagger codegen +
  ASP.NET-host parity test" as closed; tick "first-run
  setup UX gap" as closed.
