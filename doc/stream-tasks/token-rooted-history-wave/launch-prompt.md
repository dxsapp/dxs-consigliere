[$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md)

Work in `/Users/imighty/Code/dxs-consigliere` on branch `codex/consigliere-vnext`.

Goal:
implement rooted token history so token `full_history` is canonical only inside an explicit trusted-root universe.

Primary specs:
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/history-sync-model.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/token-rooted-history-model.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/token-rooted-history-implementation-slices.md`

Durable ledger:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/token-rooted-history-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/token-rooted-history-wave/slices.md`

Hard rules:
- token `full_history` without `trustedRoots[]` is invalid
- unknown-root branches must not be auto-expanded into canonical token history
- rooted token historical work must still emit normal observation facts into the canonical journal
- canonical token state/history must ignore unknown-root branches
- do not mix unrelated zones without explicit dependency need
- do not stop because adjacent files or helper artifacts appear; integrate them as expected execution churn

Execution order:
1. TT02 and TT03 on the critical path
2. TT04 after contract/state persistence are ready
3. TT05-TT08 for rooted planner and worker execution path
4. TT09-TT10 for canonical state and public status semantics
5. TT11 verification
6. A1 audit and closeout

Required first output:
- one-table plan for TT01-TT11 with statuses
- critical-path local work
- any bounded sidecar delegation by zone
- evidence paths
- then immediate execution
