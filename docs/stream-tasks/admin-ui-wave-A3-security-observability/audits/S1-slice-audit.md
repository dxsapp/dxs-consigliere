# wave-A3 S1 slice audit

APPROVE

Audit range checked: `1ce4d24..4c3e7ae` on `codex/consigliere-vnext`.

## Findings

None.

## Positive Checks

- Conservative defaults match the launch prompt: `login` = 5/min, `me` = 100/min, `broadcast` = 1/sec in `RateLimitingConfig`.
- `PermitsPerSecond` takes precedence over `PermitsPerMinute`; broadcast uses a 1-second fixed window.
- The three policies are IP-keyed fixed-window partitions, and the partition key includes the policy name, so `/login` exhaustion does not consume `/me` quota.
- Rejections use framework lease metadata to emit the standard `Retry-After` header.
- `AddConsigliereRateLimiting(configuration)` is registered after health checks and before the zone service registrations in `Startup.ConfigureServices`.
- `UseRateLimiter()` runs after authentication/authorization and before endpoint dispatch, so `[EnableRateLimiting]` attributes apply and future identity-aware partitioning has the resolved principal available.
- All three health endpoints chain `.AllowAnonymous().DisableRateLimiting()`.
- `GET /api/admin/auth/me`, `POST /api/admin/auth/login`, and `POST /api/tx/broadcast` are attributed with the named policy constants.
- Test appsettings loosen limits for shared contract-host traffic, while the S1 contract test uses `envOverrides` only on its dedicated strict-limit host.
- Admin UI `ApiClient` retries exactly once when a 429 includes `Retry-After`, clamps the wait to 65 seconds, and throws `RateLimited` on a second 429 or on missing/malformed `Retry-After`.
- Swagger/generated TypeScript contracts include the new 429 response annotations for login and broadcast.
- Runbook documents policy defaults, environment overrides, health endpoint opt-out, and the client retry contract.

## Validation Evidence

- `dotnet build Dxs.Consigliere.sln -c Release` passed with existing warnings, 0 errors.
- `dotnet test Dxs.Consigliere.sln -c Release --filter RateLimiting.RateLimiterPoliciesTests --no-build` passed 6/6.
- `pnpm verify` passed:
  - typecheck
  - lint
  - unit tests 182/182, including the 4 new `ApiClient` 429 tests
  - production build
  - bundle budget and inventory
  - contracts check
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract` passed 23/23, including the 3 S1 rate-limit contract describes.
- Manual strict-limit host burst against `POST /api/admin/auth/login` produced 5 auth-layer responses followed by 5 rate-limit responses:
  - first 5: `401` from auth validation
  - last 5: `429` with `Retry-After: 60`

## Residual Risk

- The limiter remains per-process, as intended for wave-A3; multi-instance distributed quota is explicitly deferred to wave-A4.
- Existing build warnings remain outside the S1 diff and were not treated as S1 findings.
