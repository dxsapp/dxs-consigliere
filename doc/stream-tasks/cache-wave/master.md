# Cache Wave Master Ledger

## Header

- Parent task: Consigliere event-invalidated read cache wave
- Branch: `codex/consigliere-vnext`
- Current status: `completed`
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
| repo-governance | operator/governance | done | - | docs review | durable task package, evidence paths, launch prompt, audits, and closeout exist |
| platform-common | operator/platform | done | repo-governance | build + unit tests | cache contracts and in-process backend exist behind narrow abstractions |
| indexer-state-and-storage | operator/state | done | platform-common | service/integration tests | address/token read paths are cached and projection-driven invalidation exists |
| service-bootstrap-and-ops | operator/platform | done | platform-common,indexer-state-and-storage | startup/config tests | cache wiring and config bind cleanly without API drift |
| verification-and-conformance | operator/verification | done | indexer-state-and-storage,service-bootstrap-and-ops | unit/integration/benchmark suites | correctness, replay/reorg safety, and comparative cache evidence are captured |

## Execution Wave

- Active wave: `closed`
- Critical-path slice: `none`
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
| A1 | C10 | passed | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/audits/A1.md` | no |
| A2 | C13 | passed | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/audits/A2.md` | yes |

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
| 2026-03-26 | C02-C09 | validation | working tree | projection cache contracts, reader integration, invalidation, and config wiring implemented |
| 2026-03-26 | C10 | validation | focused `Dxs.Consigliere.Tests` pack | stale-read, apply/revert, and selective invalidation coverage passed |
| 2026-03-26 | A1 | audit | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/audits/A1.md` | first audit gate passed without remediation |
| 2026-03-26 | C11-C13 | benchmark | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/benchmarks/C11-C13-projection-cache-benchmarks.md` | memory vs `Azos` comparative cache evidence captured |
| 2026-03-26 | A2 | audit | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/audits/A2.md` | `Azos` rejected for runtime adoption in this wave |
| 2026-03-26 | C14 | closeout | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/evidence/closeout.md` | cache wave closed with memory backend as adopted default |

## Remediation Slices

| remediation | triggered_by | status | owner | must_finish_before |
|---|---|---|---|---|
| R-A2-01 | A2 | done | operator/platform | C14 |

## Closeout Notes

- Completed slices:
  - C01
  - C02
  - C03
  - C04
  - C05
  - C06
  - C07
  - C08
  - C09
  - C10
  - C11
  - C12
  - C13
  - C14
- Current risks:
  - cache invalidation remains coupled to projection mutation coverage; future projection types must continue to publish localized invalidation tags
  - address history remains one of the costlier read shapes and should stay under benchmark watch as tracked scope grows
- Next slice to open: `none`
