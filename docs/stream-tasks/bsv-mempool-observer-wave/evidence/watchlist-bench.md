# Watchlist matcher benchmark — Wave 2 S7

- Host: Unix 26.4.0 / 10 CPU(s) / .NET 9.0.0
- Corpus seed: 42, corpus size: 500000, watched size: 250000
- Lookup samples: 1000000 (50/50 watched/unwatched mix)
- Stopwatch.Frequency: 1 000 000 000 ticks/sec

## Matcher construction (in-memory only, no Raven I/O)

- wall-clock: **547,3 ms**
- threshold (reference class): ≤ 2000 ms → PASS

## Matcher lookup hot path

- p50: **0,2 ns**
- p95: **0,7 ns**
- p99: **1,0 ns**
- threshold (reference class): p99 ≤ 100 ns → PASS

## Verdict

Reference thresholds are tied to the DigitalOcean Premium AMD (1 vCPU)
class declared in slices.md §S7. On non-reference hosts the result is
recorded as OBSERVATION; canonical pass requires running on the reference
class. Both numbers above are reproducible from this seed (Random(42))
and corpus shape.
