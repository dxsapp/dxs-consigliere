---
created: 2026-05-19
type: slice-audit-followup
parent: docs/stream-tasks/admin-ui-wave-A3-security-observability/audits/S2-slice-audit-prompt.md
status: applied
---

# wave-A3 S2 slice-audit followup (MAJOR REVISION REQUIRED)

Codex slice-audit on `5efdff2`: MAJOR REVISION REQUIRED
(0C / 0H / 1M / 0L). The single finding is folded here.

## M1 — `ProviderReachabilityCheck` saw the static config, not the effective config

**Verified:** the check read
`IOptionsMonitor<ConsigliereSourcesConfig>` directly, which
surfaces the appsettings + env layer only. Every other code
path in the project that consumes provider config goes
through
`IAdminProviderConfigService.GetEffectiveSourcesConfigAsync()`
(per `OpsController.cs:44`, `BlockProcessExecutor.cs:99`,
`SetupWizardService.cs:32`, etc.) — that's the contract
that applies Raven-stored operator overrides. The
readiness probe was therefore validating whichever URLs
the static config carried, ignoring operator-applied
provider overrides + the `Enabled` flag entirely. A
disabled provider would still get probed; a customised
URL would never get probed.

**Revision applied:**

- Constructor changed from
  `IOptionsMonitor<ConsigliereSourcesConfig>` to
  `IAdminProviderConfigService`.
- `CheckHealthAsync` now `await`s
  `providerConfigService.GetEffectiveSourcesConfigAsync(
  cancellationToken)`.
- Target collection filters by `provider.Enabled &&
  !string.IsNullOrWhiteSpace(BaseUrl)` — disabled providers
  are dropped from the probe set.
- If `GetEffectiveSourcesConfigAsync` throws (typically
  because Raven is down), the probe returns
  `HealthCheckResult.Degraded("config unavailable")`. Per
  the audit recommendation: don't double-count the same
  failure. The `RavenHealthCheck` carries the authoritative
  `Unhealthy` signal; aggregating that with a second
  `Unhealthy` from the provider check would just inflate
  the noise.
- Unit tests updated to stub `IAdminProviderConfigService`
  instead of `IOptionsMonitor`. Verdict matrix preserved;
  two new test cases added:
  - `Disabled_providers_are_not_probed` — proves disabled
    providers are skipped (the http client only receives
    one request, for the one enabled provider).
  - `Effective_config_throws_returns_Degraded_with_terse_message`
    — pins the new "config unavailable" failure mode.
- The constraint that descriptions never leak hostnames /
  status codes is preserved — the catch block returns the
  generic string `"config unavailable"`.

## Validation

- `dotnet build -c Release`: clean (warnings unchanged).
- `dotnet test --filter Health.HealthChecksSetupTests|
  Health.ProviderReachabilityCheckTests`: 11/11 green (was
  9; +2 for the M1 fix).
- `pnpm test:contract`: 20/20 green.
- `pnpm verify`: green.

## Result

The readiness probe now keys on the same view of provider
config the rest of the application uses. Disabling a
provider through the admin UI now correctly removes it
from the readiness probe set, and operator-customised URLs
are the ones actually being probed.
