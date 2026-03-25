# Tests

## Run

```bash
dotnet test ./Dxs.Consigliere.sln -c Release /m:1 -v minimal
```

## Suites

- `tests/Dxs.Bsv.Tests`
  - DSTAS one-pass locking parser coverage.
  - Unlocking `spendingType` parsing coverage.
  - DSTAS token-id extraction coverage.
  - P2MPKH detection and address compatibility coverage.
  - SDK conformance vectors fixture parsing and baseline assumptions.

- `tests/Dxs.Consigliere.Tests`
  - `MetaOutput` DSTAS protocol fields mapping coverage.
  - `TransactionStore.UpdateStasAttributesQuery` contract guard tests for:
    - DSTAS/P2MPKH types.
    - redeem blocking on frozen/confiscation state.
    - `spendingType -> eventType` mapping.
    - optional data continuity checks.
  - `TransactionController.ValidateStasTransaction` response and DSTAS fields coverage.

- `tests/Dxs.Consigliere.Benchmarks`
  - minimal replay harness scaffold for baseline observation-stream samples.
  - fixture-backed smoke coverage for replay loading and metrics aggregation.
  - Raven-backed observation journal benchmarks for append, replay, and duplicate-observation paths.
  - Raven-backed suites require a local `.NET 8` runtime; the current benchmark workflow uses `DOTNET_ROOT=/Users/imighty/.dotnet-vnext`.

## Remaining high-priority work

- Full integration tests that execute RavenDB patch logic end-to-end for `UpdateStasAttributes`.
- Backfill endpoint integration tests (`POST /api/admin/manage/stas/backfill`).
- Historical dataset replay tests for indexer/SDK parity checks.
