# Consigliere VNext Rollout Notes

## Intent

This note captures the final operator-facing rollout shape for `Consigliere vnext`.

Release-readiness status for the now-simplified `v1` product lives in:
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/v1-release-readiness.md`

The service now supports four explicit runtime cutover modes through `VNextRuntime:CutoverMode`:

- `legacy`
- `mirror_write`
- `shadow_read`
- `vnext_default`

## Recommended Progression

Use the modes in this order unless a deployment is explicitly meant to jump straight to the final path:

1. `legacy`
2. `mirror_write`
3. `shadow_read`
4. `vnext_default`

Meaning:

- `legacy`: current production path only
- `mirror_write`: legacy path remains authoritative while observations also enter the journal
- `shadow_read`: journal-driven projections and runtime paths are active but not yet the public default
- `vnext_default`: public reads and realtime default to vnext internals

## Configuration Surface

Minimum operator-visible knobs:

- `VNextRuntime:CutoverMode`
- `Consigliere:Sources`
- `Consigliere:Storage`

Default base config remains conservative:

- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/appsettings.json`
  - `VNextRuntime:CutoverMode = legacy`

Example vnext configs now declare the final-mode intent explicitly:

- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/appsettings.vnext.node.example.json`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/appsettings.vnext.hybrid.example.json`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/appsettings.vnext.provider-only.example.json`

## Startup Diagnostics

On startup the service now logs:

- cutover mode
- routing mode
- primary source
- fallback sources
- verification source
- enabled providers
- raw payload storage backend

The log source is:

- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Setup/VNextStartupDiagnostics.cs`

## Realtime Behavior

In `shadow_read` and `vnext_default`:

- `OnRealtimeEvent` becomes the default client-facing realtime contract
- legacy `OnTransactionFound` and `OnBalanceChanged` push callbacks are suppressed by default
- compatibility-only delete callbacks stay available for older consumers

## Public Read Behavior

In `vnext_default`:

- address and token state reads use vnext projections
- readiness gating remains strict
- address history now reads from projection-backed state instead of the legacy Raven history index path

## Validation / Build Proof

Validated locally with:

```bash
PATH=/Users/imighty/.dotnet-vnext:$PATH DOTNET_HOST_PATH=/Users/imighty/.dotnet-vnext/dotnet \
  /Users/imighty/.dotnet-vnext/dotnet build /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false -v minimal

PATH=/Users/imighty/.dotnet-vnext:$PATH DOTNET_HOST_PATH=/Users/imighty/.dotnet-vnext/dotnet \
  /Users/imighty/.dotnet-vnext/dotnet build /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Benchmarks/Dxs.Consigliere.Benchmarks.csproj -c Release -p:UseAppHost=false -v minimal

PATH=/Users/imighty/.dotnet-vnext:$PATH DOTNET_HOST_PATH=/Users/imighty/.dotnet-vnext/dotnet \
  /Users/imighty/.dotnet-vnext/dotnet publish /Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj -c Release -p:UseAppHost=false -o /tmp/dxs-consigliere-vnext-publish
```

And runtime-facing validation suites:

- `VNextFullSystemValidationTests`
- `VNextFullSystemBenchmarkSmokeTests`
- `VNextFullSystemBenchmarkEvidenceTests`

## Docker Packaging

The Docker build now publishes with:

- `ARG BUILD_CONFIGURATION=Release`
- `UseAppHost=false`

File:

- `/Users/imighty/Code/dxs-consigliere/Dockerfile`

Example build:

```bash
docker build -t dxs-consigliere:vnext /Users/imighty/Code/dxs-consigliere
```

## Remaining Compatibility Watch

- `OnTransactionDeleted` remains a compatibility stream during the final migration packaging wave.
- Projection-backed address history currently materializes address-scoped applied transactions and shapes rows in-memory; this is acceptable for managed selective scope, but not a claim of explorer-grade history economics.

## Backlog Follow-Up

The current JungleBus-first assurance surface is intentionally diagnostics-first and non-blocking for rollout.
Two useful follow-ups remain in backlog:

1. Secondary chain-tip cross-check
- goal: allow `assuranceMode = cross_checked` instead of `single_source`
- candidate sources:
  - node RPC
  - alternate external chain-tip source
- priority: medium

2. Assurance-driven remediation
- goal: react to:
  - `stalled_control_flow`
  - `stalled_local_progress`
- likely shapes:
  - explicit operator alerts
  - reconnect/restart hints
  - bounded repair or backfill triggers
- priority: medium

These are useful but not required for the current JungleBus-first rollout shape.

3. Dedicated admin `History` section in `v2`
- goal: move history-heavy workflows out of address/token detail pages and into their own admin surface
- rationale:
  - current detail pages should stay focused on current managed state and readiness
  - historical backfill semantics need more room for caveats and progress visibility
  - deep-history UX should not pretend to be cheap or universally complete
- likely contents:
  - queued history upgrades
  - historical backfill progress
  - scoped completeness explanations
  - operator warnings about provider limits, disk usage, and long-running sync cost
- priority: medium
