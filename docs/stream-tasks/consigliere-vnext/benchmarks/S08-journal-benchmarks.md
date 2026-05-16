# S08 Journal Benchmarks

## Scope

- Benchmark the Raven-backed observation journal introduced in `S07`.
- Capture append throughput, replay throughput, and duplicate-observation behavior.
- Keep evidence runnable from the existing benchmark project.

## Validation

Validated with:

```bash
PATH=/usr/local/share/dotnet:$PATH DOTNET_HOST_PATH=/usr/local/share/dotnet/dotnet /usr/local/share/dotnet/dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Benchmarks/Dxs.Consigliere.Benchmarks.csproj --filter JournalBenchmarkSmokeTests -c Release -v minimal
```

## Evidence

- `append throughput`: measured in `JournalBenchmarkHarness.MeasureAppendAsync`
- `replay throughput`: measured in `JournalBenchmarkHarness.MeasureReplayAsync`
- `duplicate observations`: measured in `JournalBenchmarkHarness.MeasureDuplicateAsync`

## Notes

- The benchmark harness is intentionally deterministic and xunit-runnable.
- It uses the same Raven-backed journal primitives that production code will use.
