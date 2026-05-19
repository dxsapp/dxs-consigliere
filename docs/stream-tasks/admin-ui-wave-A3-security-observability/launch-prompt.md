# Launch — Wave A3 (Security + observability baseline)

## Mission
Turn the wave-A2 admin UI + backend into a system that
can ship to a real paying customer behind a public DNS
name: TLS termination, brute-force-resistant auth, k8s-
grade health probes, an immutable audit trail for
destructive ops, a live log surface in the UI, secrets
that never enter git, a tightened contract gate, and a
written operator runbook.

## Package path
`docs/stream-tasks/admin-ui-wave-A3-security-observability/`

Source of truth: `master.md` (ledger), `slices.md`
(decomposition). Update `master.md` Delivery Notes as
slices close.

Upstream context (read first):
- `docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/
  evidence/closeout.md` — the wave this one builds on,
  including the four residuals it closes
- `docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/
  master.md` — the audit cadence + product-decision
  style we copy
- `docs/stream-tasks/admin-ui-vnext-program/evidence/
  closeout.md` — the original 13-slice wave that
  shipped the UI
- `compose.yml` — the existing service topology we add
  Caddy to in S0
- `src/Dxs.Consigliere/Setup/PublicApiSetup.cs` —
  where rate limiting + Swashbuckle filter slot in

## Constraints (frozen)
- **No backend DTO renames or removals.** S3 + S6 add
  new shapes (`AuditLogEntry`, `AdminAuditLogResponse`)
  but every existing DTO stays byte-identical on the
  wire.
- **No back-compat shims** for the wave-A2 provider-
  config Raven document (S5 deletes it, doesn't co-
  habit). Both producer + consumer are in-repo; the
  shim has nothing to defend against.
- **TLS termination has ZERO C# certificate-store
  code.** Caddy owns ACME + certificates.
- **Audit log writes are fail-stop.** A destructive op
  whose audit write fails must NOT proceed.
- **Health endpoints are anonymous** (k8s probes can't
  ship a cookie) but expose ONLY binary up/down +
  failing-check names.
- **Rate limit defaults err conservative.** 5 login/min,
  100 me/min, 1 broadcast/sec. Override via env vars;
  document in the runbook.
- **Wave-A1 store discipline applies.** No permanent
  `disposed` flag; idempotency via inflight controller;
  StrictMode-safe.
- **Frontend prod-compile gate is `pnpm verify`** (which
  ends with `pnpm contracts:check`). `pnpm typecheck`
  alone misses composite-project strictness.
- **Backend prod-compile gate is `dotnet build -c
  Release`.** Each slice that touches the .csproj or
  C# code must run it locally before commit.

## Execution order
Strictly sequential. See `slices.md` § "Dependency Order"
for the rationale.

1. **S0 — TLS termination + cookie Secure flag** —
   local
2. **S2 — Health endpoints** — local; can land in
   parallel with S1 if you have bandwidth
3. **S1 — Rate limiting on auth + broadcast** — local
4. **S5 — Secrets at rest** — local; needs S0's Caddy
   ENV plumbing
5. **S3 — Audit log for destructive ops** — local
6. **S4 — Backend log streaming** — local; shares the
   SignalR auth pattern with S3
7. **S6 — Swashbuckle NRT inference + screen
   migration** — local; the wave-A2 codegen gate
   carries the heavy lifting
8. **S7 — Operator runbook** — local; codifies what
   shipped

Use `/execution-operator` semantics: one ledger,
bounded subagents via the `Agent` tool (max 3
parallel), close completed background agents promptly
with `TaskStop`. For this wave, slices are sequential
— parallel fan-out is allowed only INSIDE a slice
(e.g. parallel reads of multiple C# files during
audit + impl).

Per-slice cadence (the wave-A1 / wave-A2 pattern):
1. Read the slice section in `slices.md` end-to-end.
2. Implement; run local validation per slice.
3. Draft an audit prompt at
   `audits/S<n>-slice-audit-prompt.md`.
4. Codex slice audit runs externally — record findings
   in `audits/S<n>-slice-audit.md`.
5. Fold findings; record in
   `audits/S<n>-slice-audit-followup.md`.
6. Commit with `feat(<area>): wave-A3 S<n> — <summary>`
   OR `fix(<area>): wave-A3 S<n> audit fold — <count
   findings>`. One ledger row updated per commit.

## Validation
- **S0**: `docker compose --profile dev up --build` →
  `curl -kI https://localhost/` returns HSTS header;
  setup-wizard works through Caddy; cookie has
  `Secure; HttpOnly`
- **S1**: 10-attempt login burst → 5×200/302 + 5×429
  with `Retry-After` header; integration test pins
  the limits
- **S2**: anonymous `curl /health/{live,ready,startup}`
  return the expected codes under happy + degraded
  states (Raven stopped, providers unreachable)
- **S3**: e2e walks force-rebroadcast → /audit-log →
  finds the new row; backend unit test pins the
  fail-stop on audit write failure
- **S4**: e2e opens `/logs`, sees live entries scroll;
  backend sanitizer unit tests fire red on the wave-A1
  regex set
- **S5**: `grep` gate exits 0 against committed
  appsettings; migration unit test moves a seeded
  Raven doc → file + deletes the doc
- **S6**: `pnpm verify` + `pnpm test:contract` green;
  `grep -rn "interface Admin\|interface P2p\|interface
  Source\|interface Setup" src/admin-ui/src/types/{admin,
  auth}.ts` returns ZERO matches
- **S7**: a non-engineer reads ONLY the runbook + stands
  up the stack < 45 min stopwatch

End-to-end: fresh `git clone` + `docker compose
--profile prod up --build` + reading `docs/runbook.md`
puts a new operator at a working, audit-trail-equipped,
TLS-fronted Consigliere.

## Closeout
- All ledger rows `done` or `not_opened`.
- Commit hashes recorded in `master.md` Delivery
  Notes.
- Create `audits/A1.md` (what landed, what was
  validated, residuals).
- Create `evidence/closeout.md` (result, key files,
  behavioral summary, honest residuals, before/after
  install + monitoring screenshots).
- Update `docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/
  evidence/closeout.md`: tick the four wave-A2
  residuals this wave closed:
  - Backend log streaming endpoint
  - Swashbuckle NRT-aware required-field inference
  - Screen migration onto `api.generated.ts`
  - `appsettings.Test.json` background-task disable
    (handled tangentially in S5 via the `DisableAll`
    knob OR documented as still-open)

## Commit / report expectations
- One commit per completed slice; one commit per
  audit-fold pass.
- Co-authored trailer:
  `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`
- Final report (in chat at closeout): result first,
  zones done, validation runs, real residuals.
