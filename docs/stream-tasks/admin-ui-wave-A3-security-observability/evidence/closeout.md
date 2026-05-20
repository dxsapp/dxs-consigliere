---
created: 2026-05-20
type: wave-closeout
parent: docs/stream-tasks/admin-ui-wave-A3-security-observability/master.md
related: docs/stream-tasks/admin-ui-wave-A3-security-observability/audits/A1.md
status: closed
---

# Wave-A3 closeout — security + observability baseline

## Result

Eight slices, eight green ledger rows, one A1 audit doc.
The Consigliere admin surface now ships with public TLS
termination, IP-keyed rate limiting on its load-bearing
endpoints, anonymous orchestrator-friendly health probes,
a fail-stop audit trail for destructive operations, a live
SignalR-backed log tail (with the sanitizer regex set
moved to the backend as primary + retained client-side as
defence in depth), a chmod-600 on-disk secret store that
replaces the plaintext RavenDB document the wave-A2
install used, a Swashbuckle filter that tightens generated
TS via NRT inference, and a single operator-facing
handbook (`docs/runbook.md`) covering deploy / monitor /
rotate / recover end-to-end.

The wave closed at HEAD `b58fbc3` on
`codex/consigliere-vnext`.

## Per-slice commit map

See `audits/A1.md` § "What landed" for the audit verdict
+ fold commit per slice. Headline commits:

| Slice | What                                        | Initial    | Audit fold |
|-------|---------------------------------------------|------------|------------|
| S0    | Caddy TLS + cookie Secure + ForwardedHeaders | `ade41b0` | `58b0684` |
| S2    | `/health/{live,ready,startup}` probes        | `5efdff2` | `37a5b31` |
| S1    | Rate limiting + frontend `Retry-After` once  | `4c3e7ae` | — (clean) |
| S5    | Secrets at rest + grep-lint                  | `a087036` | `d6bc356` |
| S3    | Audit log for destructive ops                | `b12fb2f` | `86a1f40` |
| S4    | Backend log streaming + admin-ui live tail   | `1db7948` | `abad57a` |
| S6    | Swashbuckle NRT filter + audit-log re-export | `a36e2a9` | `1aa3089` |
| S7    | Operator runbook + cross-links               | `91562bb` | — (audit pending) |

## Key files touched

| Area                            | New / modified                                                                                                |
|---------------------------------|---------------------------------------------------------------------------------------------------------------|
| TLS + proxy                     | `compose.yml`, `compose.prod.yml` (new), `infrastructure/caddy/{Caddyfile.dev,Caddyfile.prod,snippets/headers.caddy}` (new) |
| Cookie + forwarded headers      | `src/Dxs.Consigliere/{Setup/AdminAuthSetup.cs,Setup/ProxyHeadersSetup.cs,Startup.cs}`, `appsettings.{json,Production,Test,DockerComposeE2E}.json` |
| Health probes                   | `src/Dxs.Consigliere/Health/{LogEventDto-ish family},Setup/HealthChecksSetup.cs`, `Setup/SignalRSetup.cs`     |
| Rate limiting                   | `src/Dxs.Consigliere/{Configs/RateLimitingConfig.cs,Setup/RateLimiterPolicies.cs}`, controller attributes, `src/admin-ui/src/lib/api/client.ts` |
| Secrets at rest                 | `src/Dxs.Consigliere/{Configs/ConsigliereSecretsConfig.cs,Data/Runtime/SecretsFileStore.cs}`, `scripts/secrets-lint.sh`, `.github/workflows/ci-tests.yml`, `appsettings.Production.json` |
| Audit log                       | `src/Dxs.Consigliere/{Data/Models/Audit/AuditLogEntry.cs,Services/Audit/{IAuditLogger.cs,AuditLogger.cs}}`, `src/Dxs.Consigliere/Controllers/AdminAuditController.cs`, `src/admin-ui/src/screens/audit-log/{audit-log.store.ts,AuditLogPage.tsx}` |
| Log streaming                   | `src/Dxs.Consigliere/Logging/{LogEventDto.cs,LogSanitizer.cs,LogStreamBuffer.cs,LogStreamProvider.cs}`, `src/Dxs.Consigliere/WebSockets/LogStreamHub.cs`, `src/admin-ui/src/screens/logs/{LogsPage.tsx,logs.store.ts,sanitizer.ts}` |
| NRT inference                   | `src/Dxs.Consigliere/Swagger/RequiredFromNrtFilter.cs`, `src/admin-ui/contracts/swagger.json`, `src/admin-ui/src/types/api.generated.ts`, `src/admin-ui/src/types/admin.ts` |
| Operator runbook                | `docs/runbook.md`, `README.md`, `docs/admin-ui/design-handoff/README.md`                                       |

## Behavioral summary (operator-facing)

A first-time operator on a fresh Ubuntu VM:

1. Sets four env vars in `.env` (`CADDY_DOMAIN`,
   `CADDY_EMAIL`, `BSV_NODE_RPC_PASSWORD`,
   `RAVEN_PASSWORD`).
2. Runs `docker compose -f compose.yml -f compose.prod.yml
   --profile prod up -d --build`.
3. Watches Caddy obtain a Let's Encrypt cert (90 s
   typical).
4. Visits `https://<domain>/setup`, completes the four-
   step wizard, signs in.

Reaches a working, audit-trail-equipped, TLS-fronted
Consigliere with no further intervention. Subsequent
operational concerns (monitoring, secret rotation, DR)
follow numbered checklists in `docs/runbook.md`.

The admin UI gains two new screens (`/audit-log` +
live-tail `/logs`) and replaces the wave-A1 paste-box logs
view with a SignalR-backed live tail.

## Honest residuals (carried to wave-A4)

| Residual                                                                                | Why deferred |
|-----------------------------------------------------------------------------------------|--------------|
| Wholesale hand-mirrored-interface → re-export sweep (~30 `Dto.cs` files)                | Each file needs `#nullable enable` + per-property `string` vs `string?` review — risk per property, not per slice. S6 lands the filter + demonstrates the pattern on the freshly-tracked S3 DTOs. |
| Multi-instance HA: rate-limit counters + log-stream ring + audit retention bundle cluster awareness | Per-process state by design in wave-A3; cluster awareness is a wave-A4 platform-level concern. |
| Full axe-core CI step + per-route Lighthouse score                                       | Wave-A1 leftover. Not in S7's scope; no operator-facing impact yet. |
| Read-write Configuration screen                                                          | Wave-A1 leftover deferred per the design brief §7. Operators currently change config through `/providers` + the setup wizard. |
| Stopwatch validation of the runbook (non-engineer reads ONLY the runbook, stands up the stack < 45 min) | S7 completion signal; one-shot ops-side verification that I cannot run as engineering. Documented in A1.md as a residual the customer's first SRE engagement closes. |

## Before / after install + monitoring screenshots

(none — local manual smoke validates each slice. The
operator-side stopwatch validation in `docs/runbook.md` §1
+ §2 is the verification path for screenshots; that's a
customer-side artifact.)

## Validation at close

- `dotnet build Dxs.Consigliere.sln -c Release`: clean (0
  errors, 50 pre-existing warnings unchanged).
- `pnpm verify`: green.
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract`:
  24/24 green.
- `bash scripts/secrets-lint.sh`: exits 0.
- Per-slice unit suites green per the A1.md per-slice
  validation block.

## Gate status

Wave-A3 closed. Next wave can branch off
`codex/consigliere-vnext` HEAD without an intervening
freeze.
