---
created: 2026-05-21
type: wave-audit
status: closed
parent: docs/stream-tasks/admin-ui-wave-A3-security-observability/audits/A1.md
---

# wave-A3 — A2 hardening pass

The four Low findings from the wave-closeout re-audit
(`audits/S3-slice-audit-2-followup.md` § "Re-audit findings NOT
folded"), folded in one pass. None was exploitable as-shipped;
all are defense-in-depth / honesty fixes that close the gap to a
deployment that isn't behind the bundled Caddy + unpublished
Kestrel topology.

## L1 + L2 (S0/S1) — working trust knob for X-Forwarded-* + honest comment

**Problem.** `ProxyHeadersSetup` cleared `KnownNetworks` +
`KnownProxies`, so Kestrel trusted `X-Forwarded-For` from any
immediate caller. Safe as-shipped (Caddy overwrites the header;
Kestrel is `expose`-only), but there was **no working knob to
tighten it** — the framework lists are `IList<IPAddress>` /
`IList<IPNetwork>`, neither of which round-trips through the .NET
config binder. The source comment (and originally the runbook)
advertised `ForwardedHeadersOptions__KnownProxies__0=...`, which
does nothing.

**Fix.**
- New `ForwardedHeadersTrustConfig` with **string** `KnownNetworks`
  (CIDRs) + `KnownProxies` (IPs) — strings DO bind from env:
  `Consigliere__ForwardedHeaders__KnownNetworks__0=172.16.0.0/12`.
- `AddConsigliereForwardedHeaders(IConfiguration)` parses them
  (`TryParseNetwork` CIDR → `Microsoft.AspNetCore.HttpOverrides.IPNetwork`,
  malformed entries skipped, not fatal) and populates the
  framework lists. When no list is configured it keeps the
  permissive default (documented fallback).
- Source comment + runbook Appendix A rewritten to point at the
  **real** string knob.
- Pins: `ProxyHeadersSetupTests` gains no-trust-list-default,
  configured-list-populates, malformed-entry-skipped, and a
  `TryParseNetwork` matrix (6 cases incl. IPv6 + over-length
  prefix).

## L3 (S2) — anonymous `/health/ready` amplification

**Problem.** `/health/ready` is anonymous + `DisableRateLimiting()`
and fires one outbound HEAD per enabled provider on every hit —
an unauthenticated flood amplifies into 3× outbound probes +
connection-pool/threadpool pressure.

**Fix.** `ProviderReachabilityCheck` now caches its result for a
5s TTL behind a single-flight `SemaphoreSlim`. A flood — serial
or concurrent — collapses to at most one provider-probe-set per
window. Registered as a DI singleton (`HealthChecksSetup`) so the
cache persists across requests (`AddCheck<T>` resolves the
singleton via `GetServiceOrCreateInstance`). Pins: two new tests
(serial two-call → one probe-set; 10 concurrent → one probe-set).
Readiness lags recovery by ≤5s, which is within the typical k8s
poll cadence.

## L4 (S5) — secrets tmp-file world-readable window

**Problem.** `SecretsFileStore.WriteFileAsync` used `File.Create`
(default umask, typically 0644) and wrote the plaintext provider
secrets into the tmp file BEFORE the chmod 600. Brief
world-readable window on a multi-user host.

**Fix.** Reordered: create the empty tmp file, chmod 600 **while
empty**, THEN serialize the secrets into the already-locked file.
The create→chmod gap now only ever exposes an empty file. Also
chmod the containing secrets directory to 700 (owner-only) as
belt-and-suspenders. The existing `SecretsFileStoreTests`
chmod-600 assertion still holds.

## Validation

- `dotnet build Dxs.Consigliere.sln -c Release`: clean.
- `dotnet test --filter Setup.ProxyHeadersSetupTests|
  Health.ProviderReachabilityCheckTests|Secrets`: 26 passed,
  4 skipped (RavenTestDriver migration — no .NET 8 runtime
  locally; runs in CI).
- `pnpm verify`: green.
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract`: 24/24.
- `bash scripts/secrets-lint.sh`: exits 0.

## Residual after this pass

Hardening Lows closed. The standing wave-A3 residuals that
remain are NOT defects — they're scope deferred to wave-A4:
- wholesale hand-mirrored-interface → generated re-export sweep
  (S6 partial);
- multi-instance HA (rate-limit counters / log ring / audit
  retention bundle are per-process);
- the operator-side stopwatch + first real prod bring-up (ACME
  against real DNS, soak) — the one validation no engineering
  pass can self-certify.
