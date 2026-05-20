---
created: 2026-05-20
type: slice-audit-followup
parent: docs/stream-tasks/admin-ui-wave-A3-security-observability/audits/S7-slice-audit-prompt.md
status: applied
---

# wave-A3 S7 slice-audit followup (MAJOR REVISION REQUIRED)

Codex slice-audit on `b58fbc3`: MAJOR REVISION REQUIRED
(0C / 1H / 1M / 1L). All three findings folded.

## H1 — Prod bring-up actually launched the DockerComposeE2E overlay

**Verified:** the shared `compose.yml` hard-codes
`DOTNET_ENVIRONMENT=DockerComposeE2E` +
`ASPNETCORE_ENVIRONMENT=DockerComposeE2E` on the
`consigliere` service. `compose.prod.yml` only added the
Caddy ACME service; it did NOT override the consigliere env.
Following the runbook's §1.5 bring-up command in good faith,
the operator would silently run with the E2E overlay in
production — background tasks DISABLED via
`BackgroundTasks.DisableAll=true`, cookie policy set to
`SameAsRequest`, and `appsettings.Production.json` never
loaded. The host would have been technically up + reachable
through Caddy but functionally a stub: no block-sync, no
realtime ingest, no audit-log expiration, and the cookie
flag the whole TLS posture depends on quietly downgraded.

**Revision applied:**

- `compose.prod.yml` gains an explicit `consigliere`
  service block under the prod profile that overrides:
  - `DOTNET_ENVIRONMENT=Production` +
    `ASPNETCORE_ENVIRONMENT=Production` — loads
    `appsettings.Production.json`, re-enables background
    tasks, flips the cookie policy to `Always`.
  - `RavenDb__Urls__0=http://ravendb:8080` +
    `RavenDb__DbName=Consigliere` — the actual values
    `appsettings.Production.json` was meant to convey via
    bogus `${RAVENDB_URL}` placeholders.
  - `Consigliere__Secrets__Dir=/var/lib/consigliere/secrets`
    — matches the volume mount in `compose.yml`.
- `appsettings.Production.json` drops the misleading
  `${RAVENDB_URL}` / `${RAVENDB_NAME}` placeholders
  (Microsoft.Extensions.Configuration does NOT auto-
  expand env vars inside JSON values — those literals
  would have been read as `"${RAVENDB_URL}"` strings).
  The file now keeps only the legitimately-prod-only
  values (`DisabledTasks`, `ScanMempoolOnStart`,
  `BlockCountToScanOnStart`, cookie + admin auth
  config); env-driven config lives in `compose.prod.yml`.
- Header comment in `compose.prod.yml` calls out S0-audit
  M1 + S7-audit H1 by name so a future override-file
  refactor doesn't lose either responsibility.
- Manually verified post-fix with
  `docker compose -f compose.yml -f compose.prod.yml
  --profile prod config | grep -E "ASPNETCORE_ENVIRONMENT|
  RavenDb__"`:
  - `ASPNETCORE_ENVIRONMENT: Production` ✓
  - `RavenDb__DbName: Consigliere` ✓
  - `RavenDb__Urls__0: http://ravendb:8080` ✓

## M1 — `RAVEN_PASSWORD` documented as a working secret but never wired

**Verified:** `compose.yml`'s `ravendb` service runs with
`RAVEN_Security_UnsecuredAccessAllowed: PrivateNetwork`
and no password env. The Raven container intentionally
accepts unauthenticated connections from peer docker
containers on the internal network — the actual security
posture for the bundled stack. The runbook fictioned a
`RAVEN_PASSWORD` in the four-secret `.env` template + a
rotation procedure that wouldn't have done anything; a
diligent operator would have spent rotation-day debugging
why their new password silently didn't take effect.

**Revision applied:**

- §1.4 retitled from "four production secrets" to "three
  production secrets". `RAVEN_PASSWORD` entry removed.
- A callout block under the new three-secret template
  explains the actual posture: bundled Raven runs
  unsecured on the docker private network, nothing
  publishes a Raven port to the host, Caddy is the only
  public surface. For cert-based external Raven, the
  callout points at Appendix A for the env-var path
  (`RavenDb__ClientCertificate`).
- §7.3 (Rotation → RavenDB credentials) replaced with an
  accurate "nothing to rotate; external-cluster path is
  out of scope" entry that routes to escalate-to-
  engineering for cluster-specific rotation procedures.

## L1 — Design-brief still referenced the deleted stub

**Verified:** `docs/admin-ui/design-handoff/00-design-brief.md`
line 531 referenced `03-prod-runbook.md` in the "Domain
context (optional deep-dive)" section. The S7 commit
deleted that file + rewrote the design-handoff README's
entry #4, but missed the design-brief reference.

**Revision applied:**

- The bullet now points at
  `[docs/runbook.md](../../runbook.md)` with a
  one-sentence explanation that this is the wave-A3
  replacement for the wave-6 stub.

## Validation

- `dotnet build Dxs.Consigliere.sln -c Release`: clean
  (50 pre-existing warnings, 0 errors).
- `pnpm verify` (from `src/admin-ui`): green.
- `pnpm test:contract`: 24/24 green.
- `bash scripts/secrets-lint.sh`: exits 0.
- `docker compose -f compose.yml -f compose.prod.yml
  --profile prod config` (with `CADDY_DOMAIN` +
  `CADDY_EMAIL` set): materialises the consigliere
  service with the corrected env vars.
- `grep -nE "wave-A[0-9]\|S[0-9]+-audit" docs/runbook.md`:
  no matches (operator-facing body remains clean).

## Result

3 of 3 findings folded. The runbook's §1.5 bring-up
command now actually runs Consigliere with the production
overlay, the secret list is honest about what the bundled
stack needs, and every cross-link from the design-handoff
brief / README / wave closeouts resolves.
