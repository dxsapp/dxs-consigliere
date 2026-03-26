# Cache Wave Master Ledger

## Header

- Parent task: Consigliere event-invalidated read cache wave
- Branch: `codex/consigliere-vnext`
- Current status: `planned`
- Slice plan: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/cache-wave-slices.md`
- Main architectural context:
  - `/Users/imighty/Code/dxs-consigliere/doc/platform-api/vnext-rollout-notes.md`
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/master.md`

## Goal

Deliver projection-backed read caching for hot query surfaces:
- address history
- address balances
- address UTXO set
- token history
- token balances
- token UTXO set

The cache must be:
- invalidation-first
- projection-driven
- replay/reorg safe
- backend-agnostic
- benchmarked

`Azos Pile` is explicitly treated as a second backend decision wave, not as the initial cache foundation.

## Non-Goals

- No cache in the write path.
- No cache as system of record.
- No journal, readiness, dependency-engine, or raw payload persistence in cache.
- No controller-local or route-local ad hoc caches.
- No correctness model based on TTL.

## Active Zones

| zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|
| repo-governance | operator/governance | todo | - | docs review | durable task package, evidence paths, and launch prompt exist |
| platform-common | operator/platform | todo | repo-governance | build + unit tests | cache contracts and in-process backend exist behind narrow abstractions |
| indexer-state-and-storage | operator/state | todo | platform-common | service/integration tests | address/token read paths are cached and projection-driven invalidation exists |
| service-bootstrap-and-ops | operator/platform | todo | platform-common,indexer-state-and-storage | startup/config tests | cache wiring and config bind cleanly without API drift |
| verification-and-conformance | operator/verification | todo | indexer-state-and-storage,service-bootstrap-and-ops | unit/integration/benchmark suites | correctness, replay/reorg safety, and comparative cache evidence are captured |

## Execution Wave

- Active wave: `not_started`
- Critical-path slice: `C01`
- Parallel sidecar slices: `none`
- Current hard stop status: `none`

## Evidence Paths

- Audits: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/audits/`
- Benchmarks: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/benchmarks/`
- Remediation: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/remediation/`
- Closeout: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/evidence/`

## Audit Gates

| gate | trigger | status | evidence | remediation_required |
|---|---|---|---|---|
| A1 | C10 | not_opened | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/audits/A1.md` | yes |
| A2 | C13 | not_opened | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/audits/A2.md` | yes |

## Hard Stop Criteria

The wave must stop and open remediation before further downstream work when any of the following is observed:
- stale reads after apply/revert or replay/reorg
- cache invalidation breadth that forces effectively global resets for local mutations
- duplicate cache key construction logic diverging across services
- cache correctness depending on TTL rather than explicit invalidation
- benchmark regressions above agreed thresholds without a compensating correctness gain
- `Azos` backend introducing material behavior drift versus baseline backend

Autonomous remediation rule:
- If the issue can be fixed without user input, open a remediation slice, implement the fix, validate it, record evidence, and continue.
- Escalate only for real product-scope decisions or destructive operational choices.

## API Compatibility Notes

- This wave must preserve outward endpoint contracts unless a measured cache concern forces a narrow internal refactor.
- Controllers must not become cache owners.
- Public API remains projection-backed; cache is an internal acceleration layer only.

## Performance Notes

Target evidence to capture:
- hot-read latency for each cached surface
- miss latency for each cached surface
- invalidation cost for localized address/token mutations
- allocation and memory growth under repeated polling
- replay/reorg cache safety under churn
- memory backend vs `Azos` backend comparative evidence

## Open Handoffs

| handoff_id | from_zone | to_zone | blocked_by | expected_output | status |
|---|---|---|---|---|---|

## Active Agents

| agent | owned_slice | zone | status | close_when |
|---|---|---|---|---|

## Evidence Log

| date | slice | type | path_or_commit | note |
|---|---|---|---|---|
| 2026-03-26 | C01 | plan | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/cache-wave-slices.md` | initial cache-wave slice decomposition prepared |
| 2026-03-26 | C01 | prompt | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/cache-wave-launch-prompt.md` | execution-operator launch prompt prepared |

## Remediation Slices

| remediation | triggered_by | status | owner | must_finish_before |
|---|---|---|---|---|

## Closeout Notes

- Completed slices:
- Current risks:
  - cache invalidation can easily drift from projection semantics if event publication is placed too high in the pipeline
  - address/token read shapes may still hide duplicate key normalization logic until `C03` is finished
  - `Azos` backend may improve GC pressure while worsening operational complexity; that decision must stay evidence-driven
- Next slice to open: `C01`
