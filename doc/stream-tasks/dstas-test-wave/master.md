# DSTAS Parity Test Wave Master Ledger

## Header

- Parent task: DSTAS parity test wave against dxs-bsv-token-sdk
- Branch: `codex/consigliere-vnext`
- Current status: `wave-1`
- Slice plan: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dstas-test-wave/dstas-test-wave-slices.md`

## Active Zones

| zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|
| repo-governance | operator/governance | done | - | docs review | durable test-wave ledger, slice plan, and closeout evidence exist |
| verification-and-conformance | operator/verification | done | gap map | focused DSTAS suites | SDK-adjacent DSTAS coverage exists for parser, state, swap, authority, and lifecycle |
| indexer-state-and-storage | operator/state | done | verification findings | state/query tests | any parity gaps exposed by tests are fixed or recorded with explicit evidence |
| public-api-and-realtime | operator/api | not_opened | state findings | controller tests | only opened if test wave proves outward DTO/API gaps |

## Execution Wave

- Active wave: `Closed`
- Critical-path slice: `C01`
- Parallel sidecar slices: `none`

## Evidence Log

| date | zone | type | path_or_commit | note |
|---|---|---|---|---|
| 2026-03-26 | repo-governance | plan | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dstas-test-wave/dstas-test-wave-slices.md` | DSTAS parity test-wave slices locked before code changes |
| 2026-03-26 | verification-and-conformance | validation | `dotnet test tests/Dxs.Bsv.Tests/Dxs.Bsv.Tests.csproj --filter "FullyQualifiedName~UnlockingScriptReaderTests\|FullyQualifiedName~StasLineageEvaluatorTests\|FullyQualifiedName~DstasConformanceVectorsTests"` | parser, lineage, and conformance vector wave passed |
| 2026-03-26 | indexer-state-and-storage | validation | `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj --filter "FullyQualifiedName~TransactionStoreIntegrationTests\|FullyQualifiedName~TokenProjectionRebuilderIntegrationTests\|FullyQualifiedName~MetaOutputDstasMappingTests\|FullyQualifiedName~VNextDstasFullSystemValidationTests"` | DSTAS store, projection, mapping, and full-system wave passed |
| 2026-03-26 | indexer-state-and-storage | regression | `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj --filter "FullyQualifiedName~AddressProjectionRebuilderIntegrationTests\|FullyQualifiedName~TransactionStoreQueryContractTests\|FullyQualifiedName~TransactionControllerValidateStasTests"` | address projection fix regression pack passed |
| 2026-03-26 | repo-governance | closeout | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dstas-test-wave/evidence/dstas-test-wave-closeout.md` | closeout notes, validations, and residuals captured |
