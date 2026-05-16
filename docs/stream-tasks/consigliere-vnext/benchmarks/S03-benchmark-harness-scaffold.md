# S03 Benchmark Harness Scaffold

## Purpose

Create a minimal replay and benchmark harness scaffold for `Consigliere vnext` so future baseline samples can be measured without first touching production code.

## Scope

- `tests/Dxs.Consigliere.Benchmarks/**`

## Evidence

- Fixture loader for replay scenario JSON
- Minimal replay harness and metrics model
- Smoke test proving the scaffold loads and executes

## Validation

Validated with:

```bash
PATH=/usr/local/share/dotnet:$PATH /usr/local/share/dotnet/dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Benchmarks/Dxs.Consigliere.Benchmarks.csproj -c Release -v minimal
```

Result:
- Passed: 1
- Failed: 0
- Total: 1
- Duration: 20 ms

## Notes

- This is scaffold only.
- Real baseline samples will be added in later slices once journal and ingest hooks exist.
