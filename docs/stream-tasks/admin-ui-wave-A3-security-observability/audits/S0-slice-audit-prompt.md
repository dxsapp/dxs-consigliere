# wave-A3 S0 — slice-audit prompt

Audit target: wave-A3 S0 (TLS termination + cookie Secure
flag + ForwardedHeaders) on `codex/consigliere-vnext`.
Diff range: `b386725..<S0 commit>`.

---

You are auditing the **first slice of wave-A3**. It moves
public TLS termination from "not done" to "Caddy 2 in
compose, ACME for prod, internal CA for dev" without
putting any certificate-store code into the .NET
application. It also re-points cookie SecurePolicy through
a config knob (defaults `Always`) and wires the X-Forwarded-
{Proto,For} translation so downstream middleware (cookie
issuance, the S1 rate limiter) sees the right scheme + IP.

Read first:

- `docs/stream-tasks/admin-ui-wave-A3-security-observability/master.md`
  (S0 row marked done; Delivery Notes updated)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/slices.md`
  § S0 (intent · owned paths · exact task · what-not-to-do
  · validation · completion signal)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/launch-prompt.md`
  (wave constraints: NO C# cert code, Caddy owns ACME)

Cross-validate against the deliverable:

- `compose.yml` — Raven moved from `ports: 8080:8080` to
  `expose: 8080` (no host publishing). Consigliere also
  `expose: 5000` only. Two new Caddy services under
  `profiles: ["dev"]` and `profiles: ["prod"]`.
- `infrastructure/caddy/Caddyfile.dev` — `tls internal`,
  HTTP→HTTPS redirect on :80, HSTS via the shared snippet.
- `infrastructure/caddy/Caddyfile.prod` — `{$CADDY_DOMAIN}`
  + `email {$CADDY_EMAIL}` for ACME. No fallback path.
- `infrastructure/caddy/snippets/headers.caddy` — HSTS +
  X-Content-Type-Options + X-Frame-Options + Referrer-
  Policy + Permissions-Policy.
- `src/Dxs.Consigliere/Configs/ConsigliereAdminAuthConfig.cs`
  — new `CookieSecure` property (default `Always`).
- `src/Dxs.Consigliere/Setup/AdminAuthSetup.cs` — cookie
  options now read `Cookie.SecurePolicy` from the config
  via `ParseCookieSecurePolicy(adminAuthConfig.CookieSecure)`.
  Parser is conservative: any unknown/null/empty input maps
  to `CookieSecurePolicy.Always`.
- `src/Dxs.Consigliere/Setup/ProxyHeadersSetup.cs` — new
  extension `AddConsigliereForwardedHeaders()` configures
  `ForwardedHeadersOptions` with `XForwardedFor |
  XForwardedProto`, clears `KnownNetworks` + `KnownProxies`.
- `src/Dxs.Consigliere/Startup.cs` — adds
  `AddConsigliereForwardedHeaders()` to the service chain
  and calls `app.UseForwardedHeaders()` BEFORE
  `UseCors`/`UseRouting`.
- `src/Dxs.Consigliere/appsettings.json` — new
  `Consigliere.AdminAuth.cookieSecure: "Always"`.
- `src/Dxs.Consigliere/appsettings.Test.json` +
  `appsettings.DockerComposeE2E.json` — override to
  `SameAsRequest` (plain-HTTP loops).
- `tests/Dxs.Consigliere.Tests/Setup/
  ProxyHeadersSetupTests.cs` — unit test pinning the
  ForwardedHeaders wiring.
- `tests/Dxs.Consigliere.Tests/Setup/
  AdminAuthSetupCookieSecureTests.cs` — 8 Theory cases
  pinning the parser matrix (Always / SameAsRequest /
  None) + a 5-case "defaults to Always on anything else"
  block.
- `tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj`
  — adds `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
  so the test project can resolve ASP types.
- `docs/runbook.md` — new TLS section (dev profile / prod
  profile / cookie Secure / X-Forwarded plumbing /
  smoke-after-bring-up).

---

## What's in scope for this audit

1. **No C# certificate code.** Verify nothing under
   `src/Dxs.Consigliere/` touches `X509Certificate*`,
   `HttpsConnectionAdapterOptions`, or Kestrel TLS config.
   Caddy is the only certificate authority. Existing
   `RavenDbDocumentStore.cs` X509 usage is for RavenDB
   client cert (S5 territory), NOT the public TLS termination.
2. **Compose profiles are mutually exclusive.** A
   `docker compose --profile dev up` must not start
   `caddy-prod`, and vice-versa. Both Caddy services share
   the same `caddy-data` volume (cert + ACME state).
3. **Cookie SecurePolicy parser is conservative.** Unknown
   / null / empty inputs MUST default to `Always`. A typo
   in env config cannot silently drop the Secure flag in
   production.
4. **ForwardedHeaders order.** `UseForwardedHeaders()` must
   precede `UseCors()` + `UseRouting()` in `Startup.cs` so
   downstream middleware sees the rewritten
   `Request.Scheme` + `Connection.RemoteIpAddress`.
5. **No regression of the wave-A2 contract suite.**
   `pnpm test:contract` must still run 16/16 green
   (`AdminAuth.cookieSecure: "SameAsRequest"` in
   `appsettings.Test.json` keeps the local HTTP loop
   working).

## Verdict + finding format

Verdict line first:
- `APPROVE` — no changes required
- `APPROVE WITH CHANGES` — minor (L*) findings only
- `MAJOR REVISION REQUIRED` — at least one C/H/M

Findings tagged `C* | H* | M* | L*` (Critical / High /
Medium / Low). Each finding contains:
- file:line of the defect
- why it matters (security / correctness consequence)
- recommended fix (specific, not "consider re-architecting")

Out of scope for this audit (will be folded into later
slices in this wave): S1 rate limiting, S2 health probes,
S5 secrets, S6 Swashbuckle NRT, S7 runbook completion.

## Validation evidence I should produce

- `dotnet build -c Release` clean on Consigliere
- `pnpm verify` green on admin-ui
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract` green
- `dotnet test --filter "Setup.ProxyHeadersSetupTests|Setup.AdminAuthSetupCookieSecureTests"` 13/13 green
- `docker compose --profile dev config` parses without error
- (Manual) `curl -kI https://localhost/` returns HSTS after
  the dev profile is brought up.
