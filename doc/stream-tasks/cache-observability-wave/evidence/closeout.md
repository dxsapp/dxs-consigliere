# Cache Observability and History Paging Wave Closeout

## Completed slices

- `H02` cache observability exposed in both `/api/ops/cache` and `/api/admin/cache/status`
- `H03` runtime/build coherence validated through release build
- `H04` address-history selective paging/count fast path over denormalized envelopes
- `H05` focused coverage for admin cache metrics and paged envelope history behavior
- `H06` benchmark evidence for selective paging improvement
- `A1` audit gate
- `H07` closeout

## Key product changes

- Added admin cache status endpoint in `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Controllers/AdminController.cs` while reusing the existing cache telemetry DTO.
- Hardened `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Addresses/AddressHistoryProjectionReader.cs` so paged history queries count and materialize only the requested window when applied projection envelopes are present.
- Preserved the legacy materialization fallback for older applied documents that do not yet carry the denormalized envelope.
- Added regression coverage in:
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Controllers/AdminControllerTests.cs`
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Services/Impl/AddressHistoryServiceProjectionTests.cs`
- Redirected benchmark evidence for the new paging strategy to `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-observability-wave/benchmarks/H06-address-history-selective-paging-benchmarks.md`.

## Evidence

- Focused cache/read-path tests:
  - `Passed: 11`
- Address-history benchmark pack under `~/.dotnet-vnext`:
  - `Passed: 2`
- Release build:
  - `Succeeded`
- Measured selective paging result:
  - optimized query elapsed: `277 ms`
  - legacy fallback elapsed: `590 ms`
  - ratio: `2.13x`

## Remaining watch items

- Older applied address projection documents without denormalized envelopes still use the legacy fallback path until they are rewritten.
- Existing nullable warnings in `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Addresses/AddressProjectionRebuilder.cs` remain pre-existing and unchanged in behavior.
