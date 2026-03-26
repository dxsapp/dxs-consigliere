# Cache Wave Closeout

- measured_at_utc: 2026-03-26T15:15:00.0000000+00:00
- branch: `codex/consigliere-vnext`
- status: completed

## Outcome

The cache wave landed projection-backed, invalidation-first read caching for:
- address history
- address balances
- address UTXO set
- token history
- token balances
- token UTXO set

## Key Decisions

- memory-backed in-process cache is the adopted runtime backend
- cache invalidation remains projection-driven and post-commit
- `Azos` was benchmarked as a spike backend and rejected for mainline adoption in this wave

## Validation

- focused cache/read-path test pack passed
- cache benchmark pack passed
- full `Dxs.Consigliere.sln` release build passed

## Evidence

- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/audits/A1.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/benchmarks/C11-C13-projection-cache-benchmarks.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/audits/A2.md`

## Residual Notes

- `Azos` remains available only as a test/benchmark spike implementation in `/Users/imighty/Code/dxs-consigliere/tests/Shared/AzosProjectionReadCacheSpike.cs`
- product runtime stays free of `Azos` package or config dependency
