# Cache Follow-up Wave Closeout

## Completed slices

- `F03` cache telemetry contracts and in-process stats
- `F04` `/api/ops/cache` runtime surface
- `F05` runtime wiring through existing cache registration
- `F06` cache coverage for tx lifecycle and tracked readiness reads
- `F07` focused coverage for cache ops surface and invalidation correctness
- `F08` address-history fast path via denormalized application envelope
- `F09` benchmark proof for address-history optimization
- `A1` audit gate
- `F10` closeout

## Key product changes

- Added projection cache telemetry in `/Users/imighty/Code/dxs-consigliere/src/Dxs.Common/Cache/*`.
- Added runtime cache status endpoint in `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Controllers/OpsController.cs`.
- Extended cache coverage to:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Transactions/TxLifecycleProjectionReader.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/TrackedEntityReadinessService.cs`
- Added invalidation at mutation seams for tracked lifecycle and registration.
- Hardened address-history query path to avoid eager `MetaTransaction` / `MetaOutput` graph loading when the applied projection document already has a denormalized envelope.

## Evidence

- Focused tests:
  - `Passed: 25`
- Address-history benchmark pack:
  - `Passed: 2`
- Full release build:
  - `Succeeded`
- Measured history optimization:
  - optimized query elapsed: `89 ms`
  - legacy fallback elapsed: `109 ms`
  - ratio: `1.22x`

## Remaining watch items

- Address-history still falls back to the legacy expensive path for older applied documents until they are naturally rewritten by the rebuilder.
- The existing nullable warnings in `AddressProjectionRebuilder` were pre-existing and are unchanged in behavior.
