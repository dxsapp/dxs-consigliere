Use [$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md).

Parent task: close `junglebus-chain-tip-assurance-wave` end to end.

Before implementation:
- read `/Users/imighty/Code/dxs-consigliere/docs/AGENTS.md`
- read `/Users/imighty/Code/dxs-consigliere/docs/repository-zones/zone-catalog.md`
- read `/Users/imighty/Code/dxs-consigliere/docs/repository-zones/ownership-matrix.md`
- read `/Users/imighty/Code/dxs-consigliere/docs/stream-tasks/junglebus-chain-tip-assurance-wave/master.md`
- read `/Users/imighty/Code/dxs-consigliere/docs/stream-tasks/junglebus-chain-tip-assurance-wave/slices.md`

Execution rules:
- route work by zone before editing
- do not reintroduce node-required block verification
- keep the wave read-only and diagnostics-first
- if a secondary cross-check does not exist, label the result as single-source assurance rather than faking stronger confidence
- keep `/runtime` operator-facing and explicit about unavailable/degraded states

Required closeout:
- update wave ledger statuses in `master.md`
- add `/Users/imighty/Code/dxs-consigliere/docs/stream-tasks/junglebus-chain-tip-assurance-wave/audits/A1.md`
- add `/Users/imighty/Code/dxs-consigliere/docs/stream-tasks/junglebus-chain-tip-assurance-wave/evidence/closeout.md`
- run focused validation for touched zones
- commit with a message matching the landed behavior
