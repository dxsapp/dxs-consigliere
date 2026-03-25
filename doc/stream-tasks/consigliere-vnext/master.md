# Consigliere VNext Master Ledger

## Header

- Parent task: Consigliere vnext rollout
- Branch: `codex/consigliere-vnext`
- Main plan: `/Users/imighty/Code/dxs-consigliere/doc/platform-api/vnext-implementation-slices.md`
- Current cutover mode: `legacy`
- Current audit gate status: `A1 passed`

## Active Wave

- Active wave: `Wave C: Source And Routing Control Plane`
- Critical-path slice: `S10`
- Parallel sidecar slices: `-`
- Current hard stop status: `none`

## Slice Table

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| S01 | repo-governance | operator/governance | done | - | docs review | durable ledger and evidence path exist |
| S02 | service-bootstrap-and-ops | operator/platform | done | S01 | config binding tests + startup check | new option models bind without behavior change |
| S03 | verification-and-conformance | operator/verification | done | S01 | benchmark project runs | baseline harness exists |
| S04 | service-bootstrap-and-ops | operator/platform | done | S02 | startup validation rejects malformed source/storage config | startup can reject invalid config without changing old behavior |
| S05 | indexer-state-and-storage | operator/state | done | S02,S04 | targeted unit tests + compile validation | raw tx payloads store once and load independently of legacy hex store |
| S06 | platform-common | operator/platform | done | S03 | compile validation + projection primitive tests | stable journal interfaces exist without Raven coupling |
| S07 | indexer-state-and-storage | operator/state | done | S05,S06 | append/replay integration tests + compile validation | observation events append and replay in sequence order with dedupe |
| S08 | verification-and-conformance | operator/verification | done | S07 | benchmark project runs with journal append/replay cases | baseline journal perf numbers are captured |
| S09 | external-chain-adapters | operator/integration | done | A1 | external adapter build + app build | providers report capability availability and basic health via adapter diagnostics |
| S10 | indexer-ingest-orchestration | operator/runtime | in_progress | S09 | targeted runtime tests + app build | runtime answers source-by-capability without producer rewrites |

## Open Handoffs

| handoff_id | from_zone | to_zone | blocked_by | expected_output | status |
|---|---|---|---|---|---|

## Active Agents

| agent | owned_slice | zone | status | close_when |
|---|---|---|---|---|

## Evidence Log

| date | slice | type | path_or_commit | note |
|---|---|---|---|---|
| 2026-03-26 | S01 | ledger | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/master.md` | created durable vnext task package |
| 2026-03-26 | S01 | directories | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/` | created audits, benchmarks, and remediation evidence paths |
| 2026-03-26 | S03 | validation | `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Benchmarks/Dxs.Consigliere.Benchmarks.csproj` | dotnet test passed for benchmark scaffold |
| 2026-03-26 | S02 | validation | `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Setup/ConsigliereConfigBindingTests.cs` | config binding tests passed after adding new source/storage option models |
| 2026-03-26 | S04 | validation | `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Setup/CorePlatformSetup.cs` | options validation wired into DI and invalid config rejected deterministically |
| 2026-03-26 | S05 | validation | `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Data/RavenRawTransactionPayloadStoreIntegrationTests.cs` | raw payload Raven store stores once, reloads by tx id, and rejects conflicting writes |
| 2026-03-26 | S06 | commit | `25e59da` | storage-agnostic journal primitives landed in `Dxs.Common` with clean Release build |
| 2026-03-26 | S07 | validation | `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Data/Journal/RavenObservationJournalIntegrationTests.cs` | Raven observation journal appends, dedupes, and replays in sequence order |
| 2026-03-26 | S08 | commit | `6dba6c2` | benchmark harness added append, replay, and duplicate-observation coverage for Raven journal |
| 2026-03-26 | S08 | evidence | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/benchmarks/S08-journal-benchmarks-evidence.md` | measured baseline journal throughput with invariant formatting |
| 2026-03-26 | S09 | validation | `build:Dxs.Infrastructure + build:Dxs.Consigliere` | provider descriptor and diagnostics surface compiles cleanly through app wiring |

## Audit Gates

| gate | trigger | status | evidence | remediation_required |
|---|---|---|---|---|
| A1 | S08 | passed | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/audits/A1.md` | yes |
| A2 | S16 | not_opened | - | no |
| A3 | S23 | not_opened | - | no |
| A4 | S29 | not_opened | - | no |
| A5 | S31 | not_opened | - | no |

## Remediation Slices

| remediation | triggered_by | status | owner | must_finish_before |
|---|---|---|---|---|
| R-A1-01 | A1 | done | operator/verification | Wave C |
| R-A1-02 | A1 | done | operator/verification | Wave C |

## API Compatibility Notes

| surface | current_contract | vnext_handling | compatibility_goal | approval_required | status |
|---|---|---|---|---|---|
| `GET /api/tx/get/{id}` | return raw tx hex for known tx | preserve route | preserve where cheap | yes, if response shape changes | seeded |
| `GET /api/tx/batch/get` | batch raw tx hex lookup | preserve route | preserve where cheap | yes | seeded |
| `GET /api/tx/by-height/get` | assist-style block tx query | preserve or wrap | evolve only if perf/cost justifies | yes | seeded |
| `POST /api/tx/broadcast/{raw}` | broadcast route exists | preserve route, evolve semantics | preserve route, improve lifecycle semantics | yes, if route or body changes | seeded |
| `GET /api/tx/stas/validate/{id}` | STAS validation route exists | preserve route | preserve and strengthen semantics | yes | seeded |
| SignalR tx/balance events | existing realtime callbacks | additive-first evolution | preserve where cheap | yes, if event shape breaks consumers | seeded |

## Performance Notes

- Current baseline commits:
  - `58008fe`
  - `6dba6c2`
- Current benchmark paths: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/benchmarks/`
- Current known bottlenecks:
  - `TransactionStore` hot-path Raven fanout
  - Raven index fanout in address/token projections
  - token revalidation cascades
- Current budget violations:
  - none recorded at A1 baseline

## Closeout Notes

- Completed slices:
  - `S01`
  - `S02`
  - `S03`
  - `S04`
  - `S05`
  - `S06`
  - `S07`
  - `S08`
- Current risks:
  - provider capability routing is not implemented yet
  - journal benchmark workflow depends on `/Users/imighty/.dotnet-vnext`
- Next slice to open: `S10`
