MAJOR REVISION REQUIRED

# wave-A3 S7 slice audit

Audit range checked: `1aa3089..91562bb` on
`codex/consigliere-vnext`. Validation ran at HEAD `07594f5`.

## Findings

### H1 - Production bring-up starts the backend in DockerComposeE2E mode

file: `docs/runbook.md:98`

The runbook's first-time production command starts
`docker compose -f compose.yml -f compose.prod.yml --profile prod up -d --build`,
but that compose graph still gives the `consigliere` container
`DOTNET_ENVIRONMENT=DockerComposeE2E` and
`ASPNETCORE_ENVIRONMENT=DockerComposeE2E` (`compose.yml:29-30`). The
DockerComposeE2E overlay disables all background tasks and sets the admin
cookie policy to `SameAsRequest`
(`src/Dxs.Consigliere/appsettings.DockerComposeE2E.json:2-20`). The
Production overlay, which re-enables runtime work and sets
`cookieSecure=Always`, is never loaded by the documented production path.

Why it matters: a non-engineer following the runbook can get a TLS-fronted
stack that appears to start, but it is not a production indexer: block/mempool
background tasks are disabled, and the cookie policy is the E2E/dev policy.
That defeats the slice's core completion signal: reading only the runbook and
standing up a real production-ready Consigliere.

Recommended fix: make the prod compose override set the backend environment
to `Production`, and provide the actual production Raven configuration via
environment variables (`RavenDb__Urls__0=http://ravendb:8080`,
`RavenDb__DbName=Consigliere`) or replace the inert `${RAVENDB_URL}` /
`${RAVENDB_NAME}` placeholders in `appsettings.Production.json` with values
that the documented compose stack really supplies. Add a `docker compose ...
config` validation note that asserts the `consigliere` service materializes
`DOTNET_ENVIRONMENT: Production` before operators continue.

### M1 - Raven password rotation is documented but not wired

file: `docs/runbook.md:84`

The runbook asks operators to create `RAVEN_PASSWORD` in `.env`, then later
states that `RAVEN_PASSWORD` is read by the RavenDB image at startup
(`docs/runbook.md:458`). The compose service does not pass that variable to
RavenDB at all; its Raven environment is limited to EULA, setup mode,
unsecured private-network access, and public server URL (`compose.yml:6-10`).

Why it matters: operators are told they have set and can rotate a RavenDB
password, but the deployed Raven container ignores it. That is a false
security procedure in the operator handbook and will waste outage/rotation
time because the documented restart cannot change an unused credential.

Recommended fix: either wire RavenDB authentication/password setup for the
compose deployment and document the exact env vars that Raven actually
consumes, or remove `RAVEN_PASSWORD` from the four-secret `.env` and replace
§7.3 with the current truth: Raven is private-network-only in this stack and
has no password rotation step.

### L1 - Current design-handoff brief still points at the deleted runbook stub

file: `docs/admin-ui/design-handoff/00-design-brief.md:531`

S7 deletes `docs/admin-ui/design-handoff/03-prod-runbook.md` and rewrites the
handoff README entry to point at `docs/runbook.md`, but the current
design-handoff brief still lists `03-prod-runbook.md` as optional domain
context. The prompt allows historical `design-bundle/` references to remain,
but this file is in `docs/admin-ui/design-handoff/`, whose README says
references inside the brief resolve to sibling files.

Why it matters: the design handoff remains internally inconsistent. A future
designer or implementer following the current handoff bundle gets a broken
reference exactly where S7 was supposed to replace the stub with the new
operator source of truth.

Recommended fix: update the brief's reference to point at
`docs/runbook.md` (repo root) or remove it from the sibling-file list; leave
the historical `design-bundle/` upload copies untouched.

## Positive Checks

- `docs/runbook.md` contains all 8 mandated sections plus appendices A-C.
- The placeholder convention is established at the top of the runbook and
  the body does not contain `wave-A*` / `S*-audit` leakage.
- The runbook includes concrete escalation points for disk-pressure pruning
  and half-restored Raven state.
- README has an Ops section pointing at `docs/runbook.md`.
- The obsolete `docs/admin-ui/design-handoff/03-prod-runbook.md` file is
  deleted.
- Wave A1 and wave A2 closeouts both gained an "Ops source of truth" section
  pointing at `docs/runbook.md`.
- The S7 diff is limited to governance/docs paths.

## Validation Evidence

- `git diff --check 1aa3089..91562bb`: passed.
- `dotnet build Dxs.Consigliere.sln -c Release`: passed, 106 warnings, 0
  errors.
- `pnpm verify` from repo root: failed because the root has no `package.json`.
  Re-run from `src/admin-ui`: passed (typecheck, lint, 195 unit tests, build,
  budget, inventory, contracts check).
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract` from `src/admin-ui`:
  passed 24/24.
- `bash scripts/secrets-lint.sh`: passed.
- `grep -nE "wave-A[0-9]|S[0-9]+-audit" docs/runbook.md`: no matches.
- `CADDY_DOMAIN=example.invalid CADDY_EMAIL=ops@example.invalid docker compose -f compose.yml -f compose.prod.yml --profile prod config --services`:
  materialized `ravendb`, `consigliere`, `caddy-prod`.

## Residual Risk

The stopwatch test was not run. It requires a teammate/operator who has not
seen the codebase to follow only `docs/runbook.md` on a fresh VM and complete
the production bring-up in under 45 minutes.
