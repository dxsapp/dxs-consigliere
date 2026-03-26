[$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md)

Work in `/Users/imighty/Code/dxs-consigliere` on branch `codex/consigliere-vnext`.

Goal:
raise both DSTAS AI-first quality and overall `Consigliere` platform maturity above `9.5/10`.

Primary specs:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-95-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-95-wave/slices.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dstas-ai-first-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/history-sync-model.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/token-rooted-history-model.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/consigliere-vnext/master.md`

Hard rules:
- do not claim `>9.5` by docs polish alone; each wave must land structural changes and validation evidence
- remove semantic duplication before repackaging consumers that depend on it
- preserve rooted-token safety and unknown-root strictness
- preserve selective managed-scope history semantics
- do not stop because helper files, migrations seams, or adjacent tests appear; integrate them as expected execution churn
- use audit gates honestly: if a dimension is still `<9.5`, record it explicitly

Execution order:
1. `W1` first, because Raven semantic duplication is the biggest remaining DSTAS blocker
2. `W2-W4` next to finish DSTAS AI-first quality above `9.5`
3. `W5-W7` only after DSTAS core/runtime/verification seams are structurally stable
4. `A1` only after all opened waves have validation evidence

Required first output:
- one-table plan for `W1-W7` plus `A1`
- explicit zone routing and dependencies
- expected score-lift tracking by wave
- evidence paths and audit gates
- then immediate execution
