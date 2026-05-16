# C11-C13 Projection Cache Benchmark Evidence

- measured_at_utc: 2026-03-26T15:12:57.9569730+00:00
- scenario: projection-cache-comparison
- address_count: 32
- history_transaction_count: 64
- utxo_count_per_address: 4
- query_iterations: 400

| backend | history_qps | balance_qps | utxo_qps | token_history_qps | invalidation_cycles_qps | cache_entries |
|---|---:|---:|---:|---:|---:|---:|
| memory | 218710.70 | 49360.17 | 69346.93 | 2711864.41 | 95.62 | 2 |
| azos | 2882.51 | 29880.63 | 17207.04 | 23108.83 | 227.92 | 2 |

- note: `Azos` benchmark uses the same `IProjectionReadCache` abstraction and compares only the backend implementation.
