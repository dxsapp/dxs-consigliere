# Ownership Matrix

| Task type | Zone | Intake stream | Execution stream | Required handoff | Validation evidence |
|---|---|---|---|---|---|
| Add or change generic background-task/dataflow helper | `platform-common` | `operator` | `operator/platform` | notify dependent zones if public abstraction changes | build + affected consumer checks |
| Change DSTAS/STAS parser, script fields, spending semantics | `bsv-protocol-core` | `operator` | `operator/protocol` | handoff to `verification-and-conformance` and `indexer-state-and-storage` if derived state changes | parser tests + conformance vectors + state diff |
| Change node RPC, ZMQ, tx filtering, throughput behavior | `bsv-runtime-ingest` | `operator` | `operator/runtime` | handoff to `indexer-ingest-orchestration` when scheduling/retry behavior changes | ingest tests, replay sample, operational notes |
| Add or modify JungleBus/Bitails/WoC provider behavior | `external-chain-adapters` | `operator` | `operator/integration` | handoff to `indexer-ingest-orchestration` if payload semantics change | adapter tests or fixture replay + retry/rate-limit note |
| Change Raven projections, state recompute, backfill rules | `indexer-state-and-storage` | `operator` | `operator/state` | handoff to `verification-and-conformance` and `public-api-and-realtime` if externally visible state changes | query tests, sample before/after records, backfill plan |
| Change sync loop, block processing, mempool reconciliation | `indexer-ingest-orchestration` | `operator` | `operator/runtime` | handoff to `external-chain-adapters` when provider contract assumptions change | replay or integration check + operational rollback note |
| Change HTTP endpoints, DTOs, SignalR callbacks, broadcast UX | `public-api-and-realtime` | `operator` | `operator/api` | handoff to `indexer-state-and-storage` if new fields require persisted state | controller tests, DTO contract check, manual API proof |
| Change DI, config keys, startup, Docker/runtime packaging | `service-bootstrap-and-ops` | `operator` | `operator/platform` | handoff to all touched zones if registration or config shape changes | startup/build check + config migration note |
| Add or update tests, conformance vectors, verification harnesses | `verification-and-conformance` | `operator` | `operator/verification` | handoff to owning production zone if failure reveals contract mismatch | test output, fixture changes, expected-result justification |
| Change repository instructions, ownership docs, routing policy | `repo-governance` | `operator` | `operator/governance` | notify affected zone owners if boundaries changed | updated docs + path coverage review |

## Routing Rules

- If a change touches multiple zones, create one parent task and one child task per zone.
- Run zones in parallel only when their paths and acceptance criteria are disjoint.
- Sequence `bsv-protocol-core -> indexer-state-and-storage -> public-api-and-realtime -> verification-and-conformance` when a protocol field changes.
- Sequence `external-chain-adapters -> indexer-ingest-orchestration -> verification-and-conformance` when provider payload or retry behavior changes.
- Treat `service-bootstrap-and-ops` as a final integration zone when DI, config, or deployment wiring changes.

## Operator Model

- `operator` is the single accountable parent stream.
- `operator/<lane>` values define the preferred subagent lane for delegation, not separate human teams.
- When one person is working solo, the same operator may execute all lanes without delegation.
- Use `operator-task-intake.md` as the default parent-task template before creating child tasks by zone.

## Concrete Examples

- DSTAS 0.0.8 rollout:
  - `bsv-protocol-core`: parser and spending-type semantics
  - `indexer-state-and-storage`: derived state and backfill rules
  - `verification-and-conformance`: vector parity and regression tests
  - `public-api-and-realtime`: only if new fields become externally visible

- JungleBus payload drift:
  - `external-chain-adapters`: payload normalization
  - `indexer-ingest-orchestration`: replay and retry behavior
  - `verification-and-conformance`: fixture replay for regression proof
