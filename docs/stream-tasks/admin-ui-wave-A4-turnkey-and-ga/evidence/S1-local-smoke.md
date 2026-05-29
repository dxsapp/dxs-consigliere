# S1 — local turnkey smoke (captured)

Run during wave-A4 S1 execution against a **locally-built+tagged**
`dxs/consigliere:latest` (no published image exists yet — see the
S1 residual), brought up via `docker compose -f compose.local.yml
up -d` with the dev RavenDB the stack ships itself.

| Step | Command | Result |
|---|---|---|
| Build image | `docker build -t dxs/consigliere:latest .` | success (sha256:143cca44…) |
| Stack up | `docker compose -f compose.local.yml up -d` | ravendb + consigliere started |
| Config shape | `docker compose -f compose.local.yml config` | `image: dxs/consigliere:latest` (no build), `published: 5000`, `DOTNET_ENVIRONMENT=Local`, `cookieSecure=SameAsRequest`, `RavenDb__Urls__0=http://ravendb:8080` |
| Setup status | `GET http://localhost:5000/api/setup/status` | `200` → `{"setupRequired":true,"setupCompleted":false,"adminEnabled":false,"adminUsername":""}` |
| Liveness | `GET http://localhost:5000/health/live` | `200` |
| Wizard complete | `POST /api/setup/complete` (admin + providers + junglebus block sub) | `200` |
| **Plain-HTTP cookie** | `POST /api/admin/auth/login` → inspect `Set-Cookie` | `consigliere_admin=…; expires=…; path=/; samesite=lax; httponly` — **NO `secure` attribute** → cookie sets over plain HTTP as designed |
| Prod posture intact | `docker compose -f compose.yml -f compose.prod.yml --profile prod config` | still `ASPNETCORE_ENVIRONMENT=Production` (local downgrade confined to the local profile) |
| secrets-lint | `bash scripts/secrets-lint.sh` | exits 0 |
| backend build | `dotnet build Dxs.Consigliere.sln -c Release` | clean |

## What this proves
The full front door works end-to-end on the image path: pull-
equivalent image → one compose command → browser-reachable
wizard on plain `http://localhost:5000` → wizard completes →
admin login cookie sets over plain HTTP (no Secure). The
prod profile is untouched.

## What remains operator-run (evidence pending)
- **Real mainnet index of a watched address** with a real
  JungleBus subscription id. The wizard→config→live-ingest
  recycle mechanism is code-traced
  (`JungleBusBlockSyncMonitorBackgroundTask` /
  `RealtimeIngestBackgroundTask` watch a provider-config
  signature and recycle on change → restart-free), but
  observing an address actually transition to indexed needs a
  real sub id + a live mainnet block. Run: complete the wizard
  with your JungleBus sub id → add a watched address → confirm
  it reaches an indexed/authoritative state.
- **Published image.** This smoke used a local build because
  `dxs/consigliere:latest` is not on Docker Hub yet — see
  `S2-ga-checklist.md` § "Cut the first release".
