# Launch — Wave A2 (First-run UX + DTO safety)

## Mission
Close the two highest-friction wave-A1 residuals: ship a
public `/setup` wizard so operators no longer hand-type a
curl POST to install Consigliere, AND lock down TS-vs-C# DTO
parity behind a CI gate so the next backend wave can't
silently break the admin UI.

## Package path
`docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/`

Source of truth: `master.md` (ledger), `slices.md`
(decomposition). Update `master.md` Delivery Notes as
slices close.

Upstream context (read first):
- `docs/stream-tasks/admin-ui-vnext-program/master.md`
  (wave-A1 closed at HEAD `c4d50ce`)
- `docs/stream-tasks/admin-ui-vnext-program/evidence/closeout.md`
  (the residuals this wave closes)
- `src/admin-ui/contracts/README.md` (the scaffold for the
  codegen step + parity test)
- `src/Dxs.Consigliere/Controllers/SetupController.cs` (the
  setup endpoint contract)
- `src/Dxs.Consigliere/Dto/Requests/SetupCompleteRequest.cs`
  + `Responses/Setup/SetupStatusResponse.cs` (DTOs the
  wizard mirrors)

## Constraints (frozen)
- **No backend DTO shape changes.** Mirror what exists at
  HEAD `c4d50ce`. New endpoints in future waves go through
  the standard slice-audit pattern.
- **No back-compat shims.** Once the wizard ships, the
  manual `curl POST /api/setup/complete` route is no longer
  documented for operators. The endpoint stays anonymous so
  automated installers can drive it.
- **`/setup` is public** (outside `AuthGuard`). It must be
  reachable from a fresh, anonymous browser.
- **`api.generated.ts` is generated, never edited.** CI
  fails on hand-edits.
- **Wave-A1 store discipline applies** (per S4-S6 audit
  fold): no permanent `disposed` flag; idempotency uses
  the inflight controller; `start()` is StrictMode-safe.
- **Frontend prod-compile gate is `pnpm build`** (which runs
  `tsc -b && vite build`). `pnpm typecheck` alone misses
  composite-project strictness; the master Validation
  section explicitly requires `pnpm verify` end-to-end.
- **Backend prod-compile gate is `dotnet build -c Release`.**
  The slice that touches `Dxs.Consigliere.csproj` must run
  it locally before commit.

## Execution order
1. **S0 — Public `/setup` wizard** — local.
   Read `slices.md` § S0 for the file list + validation.
2. **S1 — Swagger codegen** — local; depends on S0.
   Reads `slices.md` § S1.
3. **S2 — ASP.NET-host parity test** — local; depends on
   S1.
   Reads `slices.md` § S2.

Use `/execution-operator` semantics: one ledger, bounded
subagents via the `Agent` tool (max 3 parallel), close
completed background agents promptly with `TaskStop`. For
this wave, slices are sequential — there is no parallel
fan-out except inside a slice (e.g. parallel grep + read of
the backend DTOs).

Per-slice cadence (the wave-A1 pattern):
1. Read the slice section in `slices.md` end-to-end.
2. Implement; run local validation per slice.
3. Draft an audit prompt at
   `audits/S<n>-slice-audit-prompt.md`.
4. Codex slice audit runs externally — record findings in
   `audits/S<n>-slice-audit.md`.
5. Fold findings; record in
   `audits/S<n>-slice-audit-followup.md`.
6. Commit with `feat(admin-ui): wave-A2 S<n> — <summary>`
   OR `fix(admin-ui): wave-A2 S<n> audit fold — <count
   findings>`. One ledger row updated per commit.

## Validation
- S0: `pnpm verify` + `pnpm test:e2e` + manual fresh-volume
  install (`docker compose down -v && docker compose up
  --build`)
- S1: `pnpm contracts:generate` produces byte-identical
  output; `pnpm contracts:check` exits 0; `pnpm verify`
  chain ends with `pnpm contracts:check`
- S2: `pnpm test:contract` boots real backend on random
  port, validates every admin endpoint payload against
  generated types; new `admin-ui-contracts` CI job green
- End-to-end: a fresh `git clone` + `docker compose up
  --build` against a clean RavenDB volume lands the
  operator at `/setup`, the wizard completes, sign-in
  succeeds, admin shell renders.

## Closeout
- All ledger rows `done` or `not_opened`.
- Commit hashes recorded in `master.md` Delivery Notes.
- Create `audits/A1.md` (what landed, what was validated,
  residuals).
- Create `evidence/closeout.md` (result, key files,
  behavioral summary, honest residuals, before/after
  install-flow screenshots).
- Update `docs/stream-tasks/admin-ui-vnext-program/
  evidence/closeout.md`: tick "Swagger codegen +
  ASP.NET-host parity test" + "first-run setup UX gap" as
  closed.

## Commit / report expectations
- One commit per completed slice; one commit per
  audit-fold pass.
- Co-authored trailer:
  `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`
- Final report (in chat at closeout): result first, zones
  done, validation runs, real residuals.
