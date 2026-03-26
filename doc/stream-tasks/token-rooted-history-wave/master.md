# Token Rooted History Wave Master Ledger

- Parent task: token rooted history implementation
- Branch: `codex/consigliere-vnext`
- Main spec: `/Users/imighty/Code/dxs-consigliere/doc/platform-api/token-rooted-history-model.md`
- Main slices: `/Users/imighty/Code/dxs-consigliere/doc/platform-api/token-rooted-history-implementation-slices.md`
- Current status: not_opened

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| TT01 | repo-governance | operator/governance | todo | - | docs review | rooted token history task package and evidence paths exist |
| TT02 | public-api-and-realtime | operator/api | todo | TT01 | API contract review | token registration and upgrade contract require trusted roots for `full_history` |
| TT03 | indexer-state-and-storage | operator/state | todo | TT01 | state-model tests | tracked token docs/status docs persist rooted security state |
| TT04 | public-api-and-realtime | operator/api | todo | TT02,TT03 | controller tests | token `full_history` registration/upgrade reject missing trusted roots |
| TT05 | indexer-ingest-orchestration | operator/runtime | todo | TT03 | planner tests | rooted planner creates work only from trusted roots |
| TT06 | indexer-ingest-orchestration | operator/runtime | todo | TT05 | orchestration tests | unknown-root branches become findings, not canonical work |
| TT07 | external-chain-adapters | operator/integration | todo | TT05 | fixture replay or adapter tests | historical token provider path supports rooted hydration |
| TT08 | indexer-ingest-orchestration | operator/runtime | todo | TT06,TT07 | integration tests | rooted token worker emits normal observation facts only for trusted-root branches |
| TT09 | indexer-state-and-storage | operator/state | todo | TT03,TT08 | projection/query tests | unknown-root branches do not pollute canonical token state/history |
| TT10 | public-api-and-realtime | operator/api | todo | TT04,TT09 | controller tests | rooted token status/readiness is visible and honest |
| TT11 | verification-and-conformance | operator/verification | todo | TT04,TT08,TT09,TT10 | rooted token test wave | positive and negative rooted token history semantics are covered |
| A1 | repo-governance | operator/governance | todo | TT11 | audit note | rooted token history is audited before rollout or further expansion |

## Evidence Log

| date | slice | type | path_or_commit | note |
|---|---|---|---|---|
| 2026-03-26 | TT01 | task-package | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/token-rooted-history-wave/master.md` | rooted token history ledger opened |
