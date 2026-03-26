# History Sync Wave Master Ledger

- Parent task: history sync and readiness implementation
- Branch: `codex/consigliere-vnext`
- Main spec: `/Users/imighty/Code/dxs-consigliere/doc/platform-api/history-sync-model.md`
- Main slices: `/Users/imighty/Code/dxs-consigliere/doc/platform-api/history-sync-implementation-slices.md`
- Current status: closed

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| H01 | repo-governance | operator/governance | done | - | docs review | durable task package and evidence paths exist |
| H02 | public-api-and-realtime | operator/api | done | H01 | controller tests | control-plane DTOs include `historyPolicy` and public history status shapes |
| H03 | indexer-state-and-storage | operator/state | done | H01 | focused readiness/history tests | tracked state/readiness docs store `historyReadiness`, `historyCoverage`, and history-policy fields |
| H04 | indexer-state-and-storage | operator/state | done | H03 | build + focused readiness/history tests | internal history backfill job/status documents exist with shared base and typed payloads |
| H05 | service-bootstrap-and-ops | operator/platform | done | H02,H03,H04 | app build + config binding tests | config, DI, and runtime registration support historical capability routing and backfill workers |
| H06 | indexer-ingest-orchestration | operator/runtime | done | H03,H05 | focused readiness/history tests | `forward_only` boundary initialization records anchor, attaches realtime, closes gaps, and promotes `historyReadiness` to `forward_live` |
| H07 | indexer-ingest-orchestration | operator/runtime | done | H04,H05 | focused history-sync tests | explicit full-history backfill jobs queue, run, retry, complete, and fail with checkpointed progress |
| H08 | external-chain-adapters | operator/integration | done | H05 | app build + config binding tests | explicit provider capability model supports `historical_address_scan` and `historical_token_scan` |
| H09 | indexer-ingest-orchestration | operator/runtime | done | H07,H08 | focused history-sync tests | historical address scans emit normal observation facts into the canonical journal via `TxMessage.FoundInBlock` |
| H10 | public-api-and-realtime | operator/api | done | H02,H06,H07 | controller tests | single-item and bulk full-history upgrade endpoints are idempotent and return public history status |
| H11 | indexer-state-and-storage | operator/state | done | H03,H06,H07,H09 | controller/readiness tests | history query path enforces `acceptPartialHistory` and returns honest coverage metadata |
| H12 | verification-and-conformance | operator/verification | done | H06,H07,H09,H10,H11 | focused tests + config binding tests + benchmark build | forward-only, full-history, upgrade, retry, degraded, and partial-history semantics are covered on the implemented paths |
| A1 | repo-governance | operator/governance | done | H12 | audit note | quality, reuse, AI-first seams, and semantic honesty are audited before further history expansions |

## Evidence Log

| date | slice | type | path_or_commit | note |
|---|---|---|---|---|
| 2026-03-26 | H01 | task-package | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/history-sync-wave/master.md` | durable history-sync ledger opened |
| 2026-03-26 | H02-H11 | build | `dotnet build /Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj -c Release -p:UseAppHost=false` | app build passed after history-sync implementation |
| 2026-03-26 | H12 | test | `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~ReadinessControllerTests|FullyQualifiedName~AddressControllerReadinessTests|FullyQualifiedName~TokenControllerTests|FullyQualifiedName~AddressHistoryServiceProjectionTests|FullyQualifiedName~TrackedEntityReadinessCacheTests|FullyQualifiedName~TrackedHistorySyncTests"` | focused readiness/history/controller wave passed |
| 2026-03-26 | H12 | test | `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~ConsigliereConfigBindingTests"` | source/config binding passed with historical capabilities added |
| 2026-03-26 | H12 | build | `dotnet build /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Benchmarks/Dxs.Consigliere.Benchmarks.csproj -c Release -p:UseAppHost=false` | benchmark assembly still compiles after API/model expansion |
