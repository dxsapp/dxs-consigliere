# Wave-A4 — slice decomposition

## Overview

Three slices. S1 is the headline (the turnkey-local gap). S3 is
pure-code (NRT sweep, finishes A3-S6). S2 is GA validation on
real infra. Execution order: **S1 → S3 → S2** — S1 unblocks a
reviewer rehearsing the flow, S3 needs no infra, S2 gates GA
sign-off and partly depends on real hardware.

The current first-run reality (verified, wave-A3 HEAD `41942b3`):

- `compose.yml` `consigliere` service: `build: context: .`,
  env `DOTNET_ENVIRONMENT=DockerComposeE2E`, `expose: 5000`
  (not published), secrets volume mounted.
- `appsettings.DockerComposeE2E.json`: `BackgroundTasks.DisableAll:
  true`, `ScanMempoolOnStart: false` → dead observer.
- `appsettings.json` (base): `Network: Mainnet`,
  `ScanMempoolOnStart: true`, 8 `EnabledTasks`,
  `RavenDb.Urls=[http://localhost:8080]`,
  `cookieSecure: "Always"` → the real working config, but its
  Raven URL + cookie mode don't fit a plain-HTTP localhost
  container.
- `compose.prod.yml`: overrides `consigliere` env to
  `ASPNETCORE_ENVIRONMENT=Production` + Raven URL/DB +
  secrets dir; adds `caddy-prod` (mandatory CADDY_DOMAIN +
  CADDY_EMAIL). The only live-ingest path today.
- README "Docker Setup": a bare `docker run dxs/consigliere:latest`
  with node+ZMQ env (BYO Raven, curl to add addresses) +
  "Docker Compose E2E Smoke ... not for live chain ingest".

---

## S1 — Local turnkey run mode

### Intent
A competent operator pulls the published image, runs one
compose command, opens `http://localhost:5000`, completes the
wizard, and has a live-indexing product — no domain, no ACME,
no cert warning, no curl, zero mandatory env.

### Owned paths
- `compose.local.yml` (new) — image-based local overlay
- `src/Dxs.Consigliere/appsettings.Local.json` (new) — real
  ingest config for the local container (or a documented reuse
  of base + env overrides; prefer an explicit file)
- `compose.yml` — ONLY if the local overlay needs a hook on the
  shared `consigliere` service; do not change its E2E defaults
- `README.md` — "Run locally" quickstart section
- (read-only reference) `appsettings.json`,
  `appsettings.DockerComposeE2E.json`, `compose.prod.yml`,
  `src/Dxs.Consigliere/Setup/AdminAuthSetup.cs` (cookie policy),
  `src/Dxs.Consigliere/Program.cs` (env→appsettings load order)

### Exact task
1. **`compose.local.yml`** overlay. Brings up `ravendb` (from
   base) + a `consigliere` that:
   - `image: dxs/consigliere:${TAG:-latest}` (override the base
     `build:` — compose merges; set `image` + leave `build`
     unused via `-f` ordering, OR define the local service to
     not inherit build; verify the merge yields image-pull not
     build).
   - env: a LOCAL environment name (e.g.
     `DOTNET_ENVIRONMENT=Local` / `ASPNETCORE_ENVIRONMENT=Local`)
     that loads `appsettings.Local.json`; `RavenDb__Urls__0=
     http://ravendb:8080`; `RavenDb__DbName=Consigliere`;
     `Consigliere__Secrets__Dir=/var/lib/consigliere/secrets`;
     `Consigliere__AdminAuth__cookieSecure=SameAsRequest`.
   - `ports: ["5000:5000"]` — published to host (the local
     front door). No Caddy service in this overlay.
   - keeps the `consigliere-secrets` volume.
2. **`appsettings.Local.json`** — real ingest config: the base
   `EnabledTasks` list (8 tasks), `ScanMempoolOnStart: true`,
   `BlockCountToScanOnStart` a small value (e.g. 2), Mainnet.
   Do NOT inline secrets (secrets-lint must stay green) — the
   wizard writes provider keys to the secrets file at runtime.
   `cookieSecure: "SameAsRequest"` (so the local env file is
   self-consistent even without the compose env override).
3. **Verify wizard → live ingest is restart-free.** Trace
   `AppInitBackgroundTask`, `JungleBusBlockSyncMonitorBackgroundTask`,
   `RealtimeIngestBackgroundTask` + `BlockProcessExecutor`: they
   call `IAdminProviderConfigService.GetEffectiveSourcesConfigAsync`
   per tick (confirmed in wave-A3), so a wizard-written provider
   config SHOULD be picked up on the next tick without a
   restart. CONFIRM this end-to-end. If a startup-only path
   (e.g. mempool scan, block-subscription bootstrap) needs the
   config at boot and therefore a restart, either make it
   re-read on config-change or document the one-line
   `docker compose ... restart consigliere` step in the
   quickstart — honestly, not silently.
4. **README "Run locally" quickstart.** Replace the "E2E smoke
   / not for live chain ingest" framing. New flow: get a free
   JungleBus subscription id → `docker compose -f compose.yml
   -f compose.local.yml up -d` → open `http://localhost:5000`
   → wizard (admin account, providers, block-sync sub id,
   confirm) → add a watched address → see it index. Keep the
   existing `docker run` + prod-compose sections; add the local
   one as the recommended default for self-hosters.

### What not to do
- Don't add Caddy to the local overlay. Plain HTTP on
  `localhost:5000` is the decided front door.
- Don't weaken the prod / DockerComposeE2E posture. The
  cookie/TLS/background-task downgrades live ONLY in the local
  env name + overlay.
- Don't inline provider API keys into `appsettings.Local.json`
  — the wizard owns secrets; secrets-lint guards this.
- Don't satisfy the slice with a green health probe over a
  disabled-task observer. Live ingest is the bar.

### Validation
- From a clean checkout: `docker compose -f compose.yml -f
  compose.local.yml up -d` → `docker compose ... config`
  confirms the local service uses the IMAGE (not build) +
  `ports 5000:5000` + the Local env.
- `http://localhost:5000` serves the wizard with no cert
  warning; after completing it the admin cookie is set over
  plain HTTP (login persists).
- With a real JungleBus sub id, a watched address transitions
  to indexed state (live ingest proven), no manual restart
  (or restart documented).
- `docker compose -f compose.yml -f compose.prod.yml --profile
  prod config` STILL shows `ASPNETCORE_ENVIRONMENT=Production`
  + `cookieSecure` not downgraded → prod posture intact.
- `bash scripts/secrets-lint.sh` exits 0 (no plaintext in the
  new appsettings).

### Completion signal
A self-hoster who never read the source pulls the image, runs
one compose command, and is watching a live-indexing address
through the browser wizard in minutes.

---

## S3 — NRT hand-mirrored-interface sweep (finishes A3-S6)

### Intent
The admin UI consumes generated wire types end-to-end. The
hand-mirrored interfaces in `types/{admin,auth}.ts` become
generated re-exports, so a backend DTO rename surfaces as a
screen-side TS compile error, not just a CI-gate diff.

### Owned paths
- `src/Dxs.Consigliere/**/Dto/**/*.cs` (response DTOs) —
  `#nullable enable` + per-property annotation
- `src/admin-ui/contracts/swagger.json` (regenerated)
- `src/admin-ui/src/types/api.generated.ts` (regenerated)
- `src/admin-ui/src/types/admin.ts`, `types/auth.ts` — collapse
  to re-exports
- screen-side imports stay pointed at `@/types/admin` /
  `@/types/auth` (the re-export bridge keeps the cutover inert)

### Exact task
1. For each remaining hand-mirrored response DTO (the ~30 not
   yet migrated; wave-A3 S6 did only `AdminAuditLog*`): add
   `#nullable enable` to the `.cs` file, annotate each property
   `string` (NotNull) vs `string?` (nullable), and initialise
   NotNull reference props (`= string.Empty` / `= []`) so the
   compiler is satisfied.
2. `pnpm contracts:generate` — the `RequiredFromNrtFilter`
   (A3-S6) populates `required` + flips `nullable: false`.
3. Replace each hand-mirrored interface in `types/admin.ts` /
   `types/auth.ts` with `export type X = components["schemas"]["X"];`.
   Keep the non-wire helpers (e.g. `OutgoingTxState` union
   literal, `SOURCE_KEYS`) — those are not generated shapes.
4. `pnpm verify` after each file (or small batch) — fix any
   newly-surfaced screen-side `T | undefined` / `T | null`
   cascades by handling the field properly, NOT by reverting
   the type.

### What not to do
- Don't sprinkle `[Required]` attributes — NRT annotations are
  the channel (A3-S6 decision).
- Don't fix a compile error by reverting the generated type;
  fix the screen-side handling.
- Don't migrate types that have no generated equivalent
  (helper unions, enums-as-string).

### Validation
- `grep -rn "interface Admin\|interface P2p\|interface
  Source\|interface Setup" src/admin-ui/src/types/{admin,auth}.ts`
  → ZERO matches.
- `pnpm verify` green (composite `tsc -b` strictness, not just
  `tsc --noEmit`).
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract` still
  24/24.
- `dotnet build Dxs.Consigliere.sln -c Release` clean.

### Completion signal
Every wire DTO under `types/{admin,auth}.ts` is a generated
re-export; the contract gate now also catches dropped/renamed
fields at screen compile time.

---

## S2 — Real prod bring-up + soak + runbook stopwatch (GA validation)

### Intent
GA sign-off backed by real infrastructure, not only tests + dev
smoke. The one slice engineering cannot fully self-certify.

### Owned paths
- `docs/stream-tasks/admin-ui-wave-A4-turnkey-and-ga/evidence/`
  — cert chain, soak metrics, stopwatch timing, screenshots
- a small soak-watch script if useful (e.g. polls
  `/health/ready` + a memory/ingest metric over 24h)
- `docs/runbook.md` — corrections if the stopwatch surfaces a
  broken/missing step

### Exact task
1. Provision a real VM, point a real DNS A/AAAA record at it.
2. `export CADDY_DOMAIN=... CADDY_EMAIL=...` →
   `docker compose -f compose.yml -f compose.prod.yml --profile
   prod up -d --build`. Confirm Caddy obtains a real Let's
   Encrypt cert (capture the chain). Complete the wizard over
   the public HTTPS URL. Confirm a broadcast reaches real BSV
   peers.
3. ≥24h soak: background tasks don't leak memory, ingest stays
   live, the audit-log `@expires` bundle is enabled + holds.
   Capture metrics.
4. Runbook stopwatch: a person who has NOT seen the codebase
   stands the stack up reading ONLY `docs/runbook.md`. Time it;
   target <45 min. Every broken/ambiguous step → fix the
   runbook.

### What not to do
- Don't fabricate evidence. If the VM/domain isn't available at
  execution time, mark each infra-dependent sub-step
  "operator-run, evidence pending" in `evidence/` and the
  ledger — do not claim it passed.
- Don't treat a local-mode (S1) run as a substitute for the
  real ACME/prod path — S1 rehearses the UX, S2 proves the
  prod posture.

### Validation
- Real Let's Encrypt cert chain captured for the domain.
- 24h soak metrics show stable memory + continuous ingest.
- Stopwatch timing recorded (<45 min, or the overrun + cause
  recorded honestly).

### Completion signal
A real operator on a real box, reading only the runbook, has a
TLS-fronted, audit-trail-equipped, live Consigliere — and the
evidence is in `evidence/`.

---

## Dependency order

```
S1 (local turnkey)  ──▶  S3 (NRT sweep, pure-code)  ──▶  S2 (GA validation on real infra)
```

S1 first: it's the headline gap and lets a reviewer rehearse
the wizard→ingest flow locally before the real-infra pass. S3
next: no infra, no overlap with S1's compose/appsettings files
(different ownership zone). S2 last: gates GA sign-off and
partly needs real hardware.

## Validation matrix

| Gate | S1 | S3 | S2 |
|---|---|---|---|
| `dotnet build Dxs.Consigliere.sln -c Release` | ✓ | ✓ | ✓ (prod image) |
| `pnpm verify` | ✓ | ✓ (primary) | — |
| `pnpm test:contract` 24/24 | ✓ | ✓ | — |
| `bash scripts/secrets-lint.sh` | ✓ | — | ✓ |
| local `compose config` uses image + plain-HTTP :5000 | ✓ | — | — |
| prod `compose config` still TLS-always + cookieSecure=Always | ✓ | — | ✓ |
| wizard → live ingest (real JungleBus sub) | ✓ | — | ✓ (prod) |
| `grep interface … types/{admin,auth}.ts` == 0 | — | ✓ | — |
| real Let's Encrypt cert + 24h soak + <45min stopwatch | — | — | ✓ |

## Closeout requirements

- `audits/A1.md` — what landed per slice, validation run,
  residuals (incl. any S2 "operator-run, evidence pending").
- `evidence/closeout.md` — result, key files, before/after
  install story, honest residuals.
- Delivery Notes hashes backfilled in a SEPARATE commit.
