[$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md)

Работаем в `/Users/imighty/Code/dxs-consigliere` на ветке `codex/consigliere-vnext`.

Цель:
реализовать cache wave для `Consigliere` как event-invalidated read cache over projection-backed reads, не ломая correctness, replay/reorg semantics и AI-first boundaries.

Главные документы:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/cache-wave/cache-wave-slices.md`

Архитектурный контекст:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/vnext-rollout-notes.md`

Repo routing:
- `/Users/imighty/Code/dxs-consigliere/doc/AGENTS.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/zone-catalog.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/ownership-matrix.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/handoff-contract.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/operator-task-intake.md`

Hard requirements:
- cache is invalidation-first, not TTL-first
- invalidation happens from projection apply/revert events, not raw tx ingress
- no cache in write path
- no cache as source of truth
- controllers do not own cache logic
- first backend is simple in-process memory backend
- `Azos` is a separate backend spike behind the same abstraction
- `Azos` adoption is evidence-driven, not assumed

Execution rules:
- execute slices from `C01` through `C14`
- obey audit gates `A1` and `A2`
- if a hard stop is fixable without user input, open a remediation slice, fix it, validate it, record evidence, and continue
- do not stop because adjacent files or support files appear; treat them as expected execution churn unless they violate ownership boundaries
- keep the critical path moving locally
- delegate only bounded sidecar work by zone ownership
- close completed subagents promptly

Expected first output:
1. short intake
2. active slices for first wave
3. plan table in this format:

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|

4. what is on the local critical path
5. what, if anything, is delegated as bounded sidecar work
6. where evidence will be written
7. then immediate execution

Start with:
- `C01`
- `C02`
- `C03`
- `C04`

Do not open `C12` or any `Azos` integration work before `A1` passes.
