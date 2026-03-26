# Cache Follow-up Wave Slices

## Scope

Three bounded steps:
- cache metrics and ops surface
- cache coverage for remaining query surfaces touched by projection-backed reads
- address-history performance hardening and benchmark proof

## Slices

| slice | zone | goal |
|---|---|---|
| F03 | platform-common | add cache metrics/state contracts and in-process implementation hooks |
| F04 | public-api-and-realtime | expose cache status/metrics via ops surface |
| F05 | service-bootstrap-and-ops | wire cache metrics into DI/startup diagnostics |
| F06 | indexer-state-and-storage | finish cache-aware read coverage and consistency across remaining query surfaces |
| F07 | verification-and-conformance | add coverage for ops surface and cache consistency |
| F08 | indexer-state-and-storage | optimize address-history projection path |
| F09 | verification-and-conformance | benchmark and regression proof for address-history optimization |
| A1 | repo-governance | quality/reuse/AI-first audit |
| F10 | repo-governance | closeout docs and evidence |

## Hard rules

- No Azos runtime adoption in this wave.
- Invalidation remains projection-driven only.
- Address-history optimization must preserve current response semantics.
