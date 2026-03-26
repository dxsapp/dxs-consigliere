# DSTAS AI-First Wave Master Ledger

- Parent task: maximal DSTAS AI-first refactor planning package
- Branch: `codex/consigliere-vnext`
- Current status: planned

| zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|
| `D0-governance-map` | `operator/governance` | `todo` | - | docs review | current DSTAS source-of-truth and target architecture are documented |
| `D1-dstas-models` | `operator/protocol` | `todo` | `D0-governance-map` | build + protocol tests | canonical DSTAS model layer exists in `Dxs.Bsv/Tokens/Dstas/Models` |
| `D2-dstas-parsing` | `operator/protocol` | `todo` | `D1-dstas-models` | parser tests | locking and unlocking DSTAS parsing live behind obvious DSTAS parsers |
| `D3-dstas-validation` | `operator/protocol` | `todo` | `D2-dstas-parsing` | validation tests + conformance | DSTAS rules are separated from generic STAS lineage orchestration |
| `D4-canonical-export` | `operator/protocol` | `todo` | `D3-dstas-validation` | protocol consumer compile + parity tests | protocol core exports one explicit DSTAS semantic contract |
| `D5-consigliere-mapping` | `operator/state` | `todo` | `D4-canonical-export` | state/query tests | persisted transaction mapping consumes the canonical DSTAS contract through dedicated adapters |
| `D6-raven-parity` | `operator/state` | `todo` | `D5-consigliere-mapping` | patch parity tests | Raven patch semantics are explicitly aligned with canonical DSTAS derivation |
| `D7-runtime-adapters` | `operator/state` | `todo` | `D6-raven-parity` | projection/read tests | token and address DSTAS runtime branches are delegated to obvious DSTAS adapters |
| `D8-verification-repack` | `operator/verification` | `todo` | `D4-canonical-export`,`D7-runtime-adapters` | test discovery + green focused packs | DSTAS tests are discoverable by feature and still green |
| `D9-editing-guide` | `operator/governance` | `todo` | `D8-verification-repack` | docs review | one short DSTAS editing guide exists for future agents |
| `D10-naming-cleanup` | `operator/protocol` | `todo` | `D9-editing-guide` | compile + focused regression | names no longer hide DSTAS scope where that causes semantic confusion |
| `A1-closeout` | `operator/governance` | `todo` | `D0-governance-map`,`D10-naming-cleanup` | audit + closeout | closeout captures final architecture, validation, and residual watch items |

## Evidence Log

| date | zone | type | path_or_commit | note |
|---|---|---|---|---|
| 2026-03-26 | kickoff | task-package | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dstas-ai-first-wave/master.md` | maximal DSTAS AI-first refactor package opened |
