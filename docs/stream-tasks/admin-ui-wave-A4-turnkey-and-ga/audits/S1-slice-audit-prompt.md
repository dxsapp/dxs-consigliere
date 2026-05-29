# wave-A4 S1 — slice-audit prompt

Audit target: wave-A4 S1 (Local turnkey run mode) on
`codex/consigliere-vnext`. Diff range: `301146f..<S1 commit>`.

---

You are auditing the **first slice of wave-A4** — the slice
that closes the "no coherent pull → run → wizard → working
product" gap the wave-A3 re-audit surfaced.

Read first:
- `docs/stream-tasks/admin-ui-wave-A4-turnkey-and-ga/master.md`
- `docs/stream-tasks/admin-ui-wave-A4-turnkey-and-ga/slices.md` § S1
- `docs/stream-tasks/admin-ui-wave-A4-turnkey-and-ga/launch-prompt.md`

Cross-validate against the deliverable:

- `compose.local.yml` (new) — STANDALONE (not an overlay on
  compose.yml). `consigliere` uses
  `image: dxs/consigliere:${CONSIGLIERE_TAG:-latest}` (pull,
  NOT build), `ports: 5000:5000` (published), env
  `DOTNET_ENVIRONMENT=Local` + `RavenDb__Urls__0=http://ravendb:8080`
  + `RavenDb__DbName=Consigliere` +
  `Consigliere__Secrets__Dir=/var/lib/consigliere/secrets` +
  `Consigliere__AdminAuth__cookieSecure=SameAsRequest`. Brings
  its own `ravendb` (expose-only) + `consigliere-secrets`
  volume. No Caddy. Zero mandatory env vars.
  - NOTE the deviation from the package's example invocation:
    slices.md floated `-f compose.yml -f compose.local.yml`, but
    a standalone file was chosen to avoid the build+image merge
    ambiguity (overriding base `build:` with `image:` yields a
    service carrying BOTH unless you use `!reset`). Invocation is
    `docker compose -f compose.local.yml up -d`. Verify this is
    the cleaner choice + the README matches it.
- `src/Dxs.Consigliere/appsettings.Local.json` (new) — minimal:
  only `Consigliere:AdminAuth:cookieSecure = SameAsRequest`. The
  working config (Mainnet, 8 EnabledTasks, ScanMempoolOnStart)
  is INHERITED from base `appsettings.json` (env=Local loads
  base then this overlay). No secrets inlined.
- `README.md` — new "### Run locally (recommended for
  self-hosting)" as the FIRST docker option; the old bare
  `docker run` relabelled "Advanced: bring-your-own RavenDB +
  BSV node (ZMQ)"; the "Docker Compose E2E Smoke" section
  reframed as contributors/CI (builds from source, tasks
  disabled, not a product run).

## What's in scope

1. **Pull-not-build.** `docker compose -f compose.local.yml
   config` must show `image: dxs/consigliere:...`, no `build:`.
2. **Plain-HTTP front door works.** Over `http://localhost:5000`
   the admin cookie sets WITHOUT the `Secure` attribute
   (cookieSecure=SameAsRequest). The operator-run evidence
   (below) shows `Set-Cookie: consigliere_admin=…; path=/;
   samesite=lax; httponly` — no `secure`.
3. **Live config = real ingest, not the E2E dead observer.**
   env=Local inherits base (tasks ON, mempool scan ON), NOT
   DockerComposeE2E (DisableAll). Confirm the env name resolves
   to base + overlay, not the E2E file.
4. **Prod posture untouched.** `docker compose -f compose.yml
   -f compose.prod.yml --profile prod config` still shows
   `ASPNETCORE_ENVIRONMENT=Production`; prod cookie stays
   `Always` (appsettings.Production.json). The local plain-HTTP
   downgrade is confined to the local profile.
5. **Restart-free wizard→ingest claim is true.** The README
   says no restart is needed. Basis:
   `JungleBusBlockSyncMonitorBackgroundTask` builds a config
   signature (BaseUrl + ApiKey + BlockSubscriptionId) and
   recycles its runtime task on change; `RealtimeIngestBackgroundTask`
   does the same with the mempool sub id. Verify this trace —
   if a startup-only bind exists that the wizard can't reach
   live, the README must document the restart instead.
6. **No secrets in the new appsettings.** `secrets-lint` green.

## Known residuals (already documented, not defects to fold)

- **No published image exists yet.** `dxs/consigliere:latest`
  is not on Docker Hub — the release workflow only fires on a
  `vX.Y.Z` tag and none has been cut. So the literal "pull our
  image" step is gated on a maintainer cutting a first release.
  S1 was validated against a locally-built+tagged image. This
  is an operator/maintainer action, tracked in A1, not a code
  defect.
- **Real mainnet ingest with a real JungleBus sub** is
  operator-run (needs a real sub id + a mainnet block to
  arrive). The wizard→config→live-recycle mechanism is
  code-traced + the front door is proven; the actual index of a
  watched address on mainnet is evidence-pending.

## Validation evidence produced (this run, local-built image)

- `docker compose -f compose.local.yml config`: image-based,
  `published: 5000`, Local env, cookieSecure SameAsRequest. ✓
- `docker compose -f compose.local.yml up -d` →
  `GET /api/setup/status` 200 `{setupRequired:true}` →
  `GET /health/live` 200. ✓
- `POST /api/setup/complete` 200 → `POST /api/admin/auth/login`
  → `Set-Cookie` has NO `secure` attr. ✓
- prod `compose config` still `ASPNETCORE_ENVIRONMENT=Production`. ✓
- `bash scripts/secrets-lint.sh` exits 0. ✓
- `dotnet build Dxs.Consigliere.sln -c Release` clean. ✓

## Verdict + finding format

Verdict first (`APPROVE` / `APPROVE WITH CHANGES` / `MAJOR
REVISION REQUIRED`); findings tagged `C*|H*|M*|L*` with
file:line, why it matters, and a specific fix.
