Use [$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md).

Parent task: `junglebus-block-sync-wave`

Read first:
- `/Users/imighty/Code/dxs-consigliere/doc/AGENTS.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/zone-catalog.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/ownership-matrix.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/junglebus-block-sync-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/junglebus-block-sync-wave/slices.md`

Mission:
- make block synchronization align with product posture: JungleBus-first for block sync, Bitails websocket for realtime, node optional only
- remove unconditional node RPC dependency from block-processing startup path
- add explicit JungleBus block-sync setup to first-run wizard
- keep scope bounded; do not turn wizard into a generic provider editor

Execution rules:
- split by zone if implementation naturally crosses runtime/api/ui/docs seams
- preserve node support as advanced optional infrastructure
- do not leave hidden drift between setup defaults and runtime behavior
- add focused tests before closeout

Required closeout:
- update wave ledger statuses in `master.md`
- add `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/junglebus-block-sync-wave/audits/A1.md`
- add `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/junglebus-block-sync-wave/evidence/closeout.md`
- summarize honest residuals
