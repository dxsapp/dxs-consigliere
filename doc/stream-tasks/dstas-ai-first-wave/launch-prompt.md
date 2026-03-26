[$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md)

Work in `/Users/imighty/Code/dxs-consigliere` on branch `codex/consigliere-vnext`.

Goal:
refactor DSTAS for AI-first development so the protocol has one obvious semantic core, `Consigliere` becomes a thin consumer of that contract, and verification is discoverable by feature.

Primary specs:
- `/Users/imighty/Code/dxs-consigliere/doc/protocol/dstas-source-of-truth.md`
- `/Users/imighty/Code/dxs-consigliere/doc/protocol/dstas-refactor-target.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dstas-ai-first-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dstas-ai-first-wave/slices.md`

Hard rules:
- keep DSTAS product semantics unchanged unless a slice explicitly proves and documents a contract correction
- maintain one direction of truth: `Dxs.Bsv` owns canonical DSTAS parsing and derivation; `Dxs.Consigliere` consumes it
- do not mix unrelated zones in one work item without explicit dependency need
- do not stop because helper files, migration seams, or adjacent tests appear; integrate them as expected execution churn
- preserve rooted token semantics and current history/readiness contract unless a slice explicitly owns that surface

Execution order:
1. D0-D4 on the critical path to establish canonical protocol shape
2. D5-D7 after the canonical export exists
3. D8 after protocol and runtime seams are stable enough to repackage tests safely
4. D9-D10 after feature seams are real, not hypothetical
5. A1 audit and closeout

Required first output:
- one-table plan for D0-D10 with statuses, dependencies, and validation
- explicit zone routing for protocol, state, verification, and governance work
- critical-path local execution plus bounded sidecar delegation where useful
- evidence paths
- then immediate execution
