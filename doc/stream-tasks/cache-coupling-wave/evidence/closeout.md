# Cache Coupling and Envelope Backfill Wave Closeout

## Completed slices

- `J02` state-side invalidation telemetry, projection lag, and backfill status readers
- `J03` richer cache runtime status on ops/admin surfaces
- `J04` bounded background rewrite task for legacy address-history envelopes
- `J05` focused tests plus benchmark evidence for backfill recovery
- `A1` audit gate
- `J06` closeout

## Key product changes

- Added runtime status primitives in:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Cache/ProjectionCacheRuntimeTelemetry.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Cache/ProjectionCacheRuntimeStatusReader.cs`
- Added shared envelope hydration/backfill seams in:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Addresses/AddressHistoryEnvelopeHelper.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Addresses/AddressHistoryEnvelopeBackfillService.cs`
- Added hosted rewrite task:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/AddressHistoryEnvelopeBackfillBackgroundTask.cs`
- Expanded cache runtime responses via:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dto/Responses/ProjectionCacheStatusResponse.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dto/Responses/ProjectionCacheStatusResponseFactory.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Controllers/OpsController.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Controllers/AdminController.cs`
- Existing mutation seams now record invalidation telemetry in:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Addresses/AddressProjectionRebuilder.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tokens/TokenProjectionRebuilder.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Transactions/TxLifecycleProjectionRebuilder.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tracking/TrackedEntityRegistrationStore.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/TrackedEntityLifecycleOrchestrator.cs`

## Evidence

- Release build:
  - `Succeeded`
- Focused state/api test pack:
  - `Passed: 14`
- Address-history benchmark pack under `~/.dotnet-vnext`:
  - `Passed: 4`
- Envelope recovery benchmark:
  - legacy query elapsed: `794 ms`
  - query after backfill elapsed: `485 ms`
  - recovered vs legacy ratio: `1.64x`

## Remaining watch items

- The backfill task currently uses a fixed batch size and task-name gating only; there is no dedicated task-specific config yet.
- `TryGetHistoryFromEnvelopeAsync` still falls back for any batch containing an unrewritten document, so the full benefit grows with envelope coverage.
