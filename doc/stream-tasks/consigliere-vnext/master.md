# Consigliere VNext Master Ledger

## Header

- Parent task: Consigliere vnext rollout
- Branch: `codex/consigliere-vnext`
- Main plan: `/Users/imighty/Code/dxs-consigliere/doc/platform-api/vnext-implementation-slices.md`
- Current cutover mode: `legacy`
- Current audit gate status: `A4 passed`

## Active Wave

- Active wave: `Wave I: Packaging, Cutover, And Validation`
- Critical-path slice: `S31`
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
| S10 | indexer-ingest-orchestration | operator/runtime | done | S09 | targeted runtime tests + app build | runtime answers source-by-capability without producer rewrites |
| S11 | public-api-and-realtime | operator/api | done | S09,S10 | API tests + app build | ops API shows provider-first status with nested capability state |
| S12 | bsv-runtime-ingest | operator/runtime | done | S07,S10,S11 | targeted ingest tests + app build | every current tx event written into legacy flow is also observable in the journal |
| S13 | indexer-ingest-orchestration | operator/runtime | done | S12 | replay/integration checks + app build | block and reorg semantics can be reconstructed from journal facts |
| S14 | indexer-state-and-storage | operator/state | done | S12,S13 | projection tests + app build | tx lifecycle projection can be rebuilt from journal sequence and read by tx id |
| S15 | public-api-and-realtime | operator/api | done | S14 | API tests + app build | current tx hex clients still work and new tx lifecycle semantics are available |
| S16 | verification-and-conformance | operator/verification | done | S14,S15 | replay tests + perf evidence | lifecycle projection is deterministic under replay, duplicate/out-of-order handling is explicit, and first tx-state perf baseline is captured |
| S17 | indexer-state-and-storage | operator/state | done | A2 | tracked entity registration tests + app build | registration creates deterministic public tracked docs and internal status docs without cutting off legacy watch seeding |
| S18 | indexer-ingest-orchestration | operator/runtime | done | S17 | lifecycle orchestrator tests + app build | tracked lifecycle stays conservative: registration enters backfilling, live requires backfill completion + realtime attach + gap closure, and degraded semantics remain explicit |
| S19 | public-api-and-realtime | operator/api | done | S17,S18 | readiness controller tests + app build | readiness endpoints exist and tracked pre-live address reads are denied with readiness payloads instead of state data |
| S20 | indexer-state-and-storage | operator/state | done | S18 | dependency store integration tests + app build | direct token-validation dependency facts and reverse dependent lookup exist without relying on broad Raven fanout scans |
| S21 | bsv-protocol-core | operator/protocol | done | S20 | STAS lineage evaluator tests + app build | token lineage and validation verdicts can be computed from explicit reusable inputs instead of only through implicit TransactionStore patch logic |
| S22 | indexer-ingest-orchestration | operator/runtime | done | S20,S21 | dependency revalidation coordinator tests + app build | lineage change now drives targeted automatic revalidation via explicit dependency facts instead of FoundMissing index fanout being the primary coordinator |
| S23 | verification-and-conformance | operator/verification | done | S20,S21,S22 | cascade/reorg tests + token lineage benchmark evidence | explicit token lineage path has worst-case correctness and throughput evidence and is ready for A3 audit review |
| S24 | indexer-state-and-storage | operator/state | done | S18,S22,S23 | address projection integration tests + `build:Dxs.Consigliere` | address balances and UTXOs now rebuild from journal-driven mutation facts and can be served without the legacy Raven hot mutation indexes |
| S25 | indexer-state-and-storage | operator/state | done | S24 | token projection integration tests + `build:Dxs.Consigliere` | token state, token history, and token-centric UTXO/stats reads now rebuild from journal-driven state instead of legacy implicit STAS index semantics |
| S26 | public-api-and-realtime | operator/api | done | S24,S25 | controller projection tests + `build:Dxs.Consigliere` | additive address/token GET surfaces now read from vnext projections with strict readiness gating while legacy POST routes remain controlled compatibility wrappers |
| S27 | public-api-and-realtime | operator/api | done | S26 | realtime notifier tests + `build:Dxs.Consigliere` | websocket/signalr streams now expose additive vnext realtime envelopes, token subscriptions, and lifecycle/readiness event alignment while legacy callbacks stay available |
| S28 | service-bootstrap-and-ops | operator/platform | done | S27 | config binding/startup diagnostics tests + `build:Dxs.Consigliere(useapphost=false)` | vnext startup examples, strict source validation, and startup diagnostics are shipped for operators without requiring code inspection |
| S29 | verification-and-conformance | operator/verification | done | S26,S27,S28 | `vstest:VNextFullSystemValidationTests` + `vstest:VNextFullSystemBenchmarkSmokeTests|VNextFullSystemBenchmarkEvidenceTests` | vnext has measured correctness and throughput evidence for replay, reorg, and soak flows under the bounded full-system harness |
| S30 | indexer-state-and-storage | operator/state | done | S26,S29 | `build:Dxs.Consigliere.Tests(useapphost=false)` + `vstest:TransactionStoreIntegrationTests` | legacy `TransactionStore` no longer relies on per-output/per-input patch fanout to persist compatibility state |

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
| 2026-03-26 | S10 | validation | `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Services/Impl/SourceCapabilityRoutingTests.cs` | capability routing resolves legacy defaults, overrides, and verification source correctly |
| 2026-03-26 | S11 | validation | `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Controllers/OpsControllerTests.cs` | provider ops endpoint returns provider-first status with nested capability activity and rate-limit hints |
| 2026-03-26 | S12 | validation | `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/BackgroundTasks/TxObservationJournalMirrorBackgroundTaskTests.cs` | tx bus observations mirror into the journal with source identity, payload references, and drop filtering |
| 2026-03-26 | S13 | validation | `build:Dxs.Consigliere + tests:BlockObservationJournalMirrorBackgroundTaskTests|TxObservationJournalMirrorBackgroundTaskTests` | block bus events and orphaned-block detection now emit journal facts for connected and disconnected chain observations |
| 2026-03-26 | S14 | validation | `build:Dxs.Consigliere + tests:RavenObservationJournalIntegrationTests|TxLifecycleProjectionRebuilderIntegrationTests` | mixed journal replay filters typed reads correctly and tx lifecycle projections rebuild deterministically from tx+block facts |
| 2026-03-26 | S15 | validation | `build:Dxs.Consigliere + tests:TransactionControllerStateTests|TransactionQueryServiceLifecycleTests` | additive tx state endpoint is available while legacy raw-tx retrieval remains intact |
| 2026-03-26 | S16 | validation | `tests:TxLifecycleProjectionRebuilderIntegrationTests|TransactionQueryServiceLifecycleTests|TxLifecycleBenchmarkSmokeTests|TxLifecycleBenchmarkEvidenceTests` | tx lifecycle replay stayed deterministic after page-level batching and benchmark evidence was captured without Raven session request exhaustion |
| 2026-03-26 | S16 | evidence | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/benchmarks/S16-tx-lifecycle-benchmarks-evidence.md` | first tx lifecycle rebuild/query perf baseline recorded for audit gate A2 |
| 2026-03-26 | A2 | audit | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/audits/A2.md` | tx lifecycle journal semantics, replay determinism, and out-of-order handling passed with watch-items documented |
| 2026-03-26 | S17 | validation | `tests:TrackedEntityRegistrationStoreIntegrationTests + build:Dxs.Consigliere` | tracked address/token docs and status docs are created idempotently and wired into the existing admin registration flow |
| 2026-03-26 | S18 | validation | `tests:TrackedEntityRegistrationStoreIntegrationTests|TrackedEntityLifecycleOrchestratorIntegrationTests + build:Dxs.Consigliere` | tracked lifecycle transitions are now explicit and conservative, with runtime wiring moving fresh registrations into backfilling instead of falsely advertising readiness |
| 2026-03-26 | S19 | validation | `tests:ReadinessControllerTests|AddressControllerReadinessTests + build:Dxs.Consigliere` | readiness endpoints and pre-live read denial are now active for tracked address reads without breaking existing controller surface |
| 2026-03-26 | S20 | validation | `tests:TokenValidationDependencyStoreIntegrationTests + build:Dxs.Consigliere` | explicit direct-edge token validation dependencies and reverse dependents now persist separately from legacy implicit MissingTransactions coordination |
| 2026-03-26 | S21 | validation | `tests:Dxs.Bsv.Tests/StasLineageEvaluatorTests + build:Dxs.Consigliere` | explicit STAS/DSTAS lineage evaluation contract now reproduces freeze/redeem/missing-dependency/issue verdicts without Raven patch-script coupling |
| 2026-03-26 | S22 | validation | `tests:StasDependencyRevalidationCoordinatorIntegrationTests + build:Dxs.Consigliere` | runtime revalidation now cascades by explicit direct dependents and startup recovery seeds from unresolved MetaTransactions rather than FoundMissing reduce-output coordination |
| 2026-03-26 | S23 | validation | `tests:StasDependencyRevalidationCascadeIntegrationTests + benchmarks:TokenLineageBenchmarkSmokeTests|TokenLineageBenchmarkEvidenceTests` | chain/delete cascade behavior is covered and the first token-lineage evaluator + revalidation burst baseline is recorded |
| 2026-03-26 | A3 | audit | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/audits/A3.md` | token lineage correctness, dependency discipline, and storm performance passed without remediation |
| 2026-03-26 | S24 | validation | `tests:AddressProjectionRebuilderIntegrationTests|UtxoSetManagerProjectionTests + build:Dxs.Consigliere` | journal-driven address balance and UTXO projection landed with stored debit/credit mutation facts, service read-path cutover, and revert coverage after legacy tx deletion |
| 2026-03-26 | S25 | validation | `tests:TokenProjectionRebuilderIntegrationTests|UtxoSetManagerProjectionTests + build:Dxs.Consigliere` | token state and history now rebuild from journal facts while token-centric UTXO and token stats read from the new projection path instead of legacy STAS indexes |
| 2026-03-26 | S26 | validation | `tests:AddressControllerStateTests|TokenControllerTests|AddressControllerReadinessTests + build:Dxs.Consigliere` | additive address/token GET endpoints now expose projection-backed state with `not_tracked` and `scope_not_ready` readiness semantics while legacy POST reads remain intact |
| 2026-03-26 | S27 | validation | `tests:ManagedScopeRealtimeNotifierTests|AddressControllerStateTests|TokenControllerTests|AddressControllerReadinessTests + build:Dxs.Consigliere` | additive realtime envelopes, token subscriptions, tx lifecycle events, and scope status transitions now flow through SignalR without breaking legacy websocket callbacks |
| 2026-03-26 | S28 | validation | `tests:ConsigliereConfigBindingTests|VNextStartupDiagnosticsTests + build:Dxs.Consigliere(useapphost=false)` | shipped vnext config templates now bind against runtime options, disabled provider references are rejected at startup, and diagnostics print effective source/storage shape for operators |
| 2026-03-26 | S29 | validation | `vstest:VNextFullSystemValidationTests|VNextFullSystemBenchmarkSmokeTests|VNextFullSystemBenchmarkEvidenceTests` | full-system replay/reorg/soak validation passed and benchmark evidence was recorded after direct-load harness simplification and bounded scenario tuning |
| 2026-03-26 | S29 | evidence | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/benchmarks/S29-full-system-benchmarks-evidence.md` | replay/query/soak throughput captured for the bounded vnext full-system harness |
| 2026-03-26 | A4 | audit | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/audits/A4.md` | full-system correctness/perf review passed with mandatory remediation for projection request amplification before S30 |
| 2026-03-26 | R-A4-01 | validation | `vstest:VNextFullSystemBenchmarkSmokeTests|VNextFullSystemBenchmarkEvidenceTests` | address/token projection batching and deferred token recompute now allow the full-system benchmark harness to pass again at `TransferCount = 4` |
| 2026-03-26 | S30 | validation | `build:Dxs.Consigliere.Tests(useapphost=false) + vstest:TransactionStoreIntegrationTests` | legacy transaction persistence now batches raw/meta/output mutations in one Raven session and preserves `NotModified`/delete compatibility semantics without per-doc patch fanout |

## Audit Gates

| gate | trigger | status | evidence | remediation_required |
|---|---|---|---|---|
| A1 | S08 | passed | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/audits/A1.md` | yes |
| A2 | S16 | passed | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/audits/A2.md` | no |
| A3 | S23 | passed | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/audits/A3.md` | no |
| A4 | S29 | passed | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/audits/A4.md` | no |
| A5 | S31 | not_opened | - | no |

## Remediation Slices

| remediation | triggered_by | status | owner | must_finish_before |
|---|---|---|---|---|
| R-A1-01 | A1 | done | operator/verification | Wave C |
| R-A1-02 | A1 | done | operator/verification | Wave C |
| R-A4-01 | A4 | done | operator/state | S30 |

## API Compatibility Notes

| surface | current_contract | vnext_handling | compatibility_goal | approval_required | status |
|---|---|---|---|---|---|
| `GET /api/tx/get/{id}` | return raw tx hex for known tx | preserve route | preserve where cheap | yes, if response shape changes | seeded |
| `GET /api/tx/batch/get` | batch raw tx hex lookup | preserve route | preserve where cheap | yes | seeded |
| `GET /api/tx/by-height/get` | assist-style block tx query | preserve or wrap | evolve only if perf/cost justifies | yes | seeded |
| `GET /api/address/{address}/state` | additive vnext route | projection-backed | additive-first | no | done in S26 |
| `GET /api/address/{address}/balances` | additive vnext route | projection-backed | additive-first | no | done in S26 |
| `GET /api/address/{address}/utxos` | additive vnext route | projection-backed | additive-first | no | done in S26 |
| `GET /api/address/{address}/history` | additive vnext route | compatibility wrapper over current history service | additive-first | yes, if wrapper is retired | done in S26 |
| `GET /api/token/{tokenId}/state` | additive vnext route | projection-backed | additive-first | no | done in S26 |
| `GET /api/token/{tokenId}/balances` | additive vnext route | projection-backed | additive-first | no | done in S26 |
| `GET /api/token/{tokenId}/utxos` | additive vnext route | projection-backed | additive-first | no | done in S26 |
| `GET /api/token/{tokenId}/history` | additive vnext route | projection-backed | additive-first | no | done in S26 |
| `POST /api/tx/broadcast/{raw}` | broadcast route exists | preserve route, evolve semantics | preserve route, improve lifecycle semantics | yes, if route or body changes | seeded |
| `GET /api/tx/stas/validate/{id}` | STAS validation route exists | preserve route | preserve and strengthen semantics | yes | seeded |
| SignalR tx/balance events | existing realtime callbacks | additive-first evolution plus `OnRealtimeEvent` envelope and token subscriptions | preserve where cheap | yes, if event shape breaks consumers | done in S27 |

## Performance Notes

- Current baseline commits:
  - `58008fe`
  - `6dba6c2`
- Current benchmark paths: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/benchmarks/`
- Current known bottlenecks:
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
  - `S09`
  - `S10`
  - `S11`
  - `S12`
  - `S13`
  - `S14`
  - `S15`
  - `S16`
  - `S17`
  - `S18`
  - `S19`
  - `S20`
  - `S21`
  - `S22`
  - `S23`
  - `S24`
  - `S25`
  - `S26`
  - `S27`
  - `S28`
  - `S29`
  - `S30`
- Current risks:
  - journal benchmark workflow depends on `/Users/imighty/.dotnet-vnext`
  - address projection currently blocks checkpoint advance when source `MetaTransaction`/`MetaOutput` docs are not yet available; this preserves correctness but should be revisited before broader cutover waves
  - token state currently recomputes from `MetaTransaction` plus address projection state on each touched token; this is correct and bounded for current scope, but `S29/A4` should verify replay cost before full cutover
  - additive `GET /api/address/{address}/history` still delegates to the existing history service; full projection-backed address history remains a follow-up concern for later cutover waves
  - scope lifecycle events are emitted from tracked-status snapshot diffs during block-processed notifications; this keeps S27 additive, but means non-block lifecycle changes may not surface until the next block-driven pass
  - local macOS apphost signing drift requires `UseAppHost=false` for packaging-zone validation commands; repository packaging semantics remain unchanged
  - same-pass `block_disconnected` handling does not currently observe freshly stored applied-transaction rows inside the same rebuild session; reorg validation therefore uses a two-phase pass and this behavior should be revisited in later state/runtime work
- Next slice to open: `S31`
