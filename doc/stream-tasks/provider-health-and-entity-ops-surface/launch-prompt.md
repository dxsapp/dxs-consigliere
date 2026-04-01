Use [$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md).

Parent task: `provider-health-and-entity-ops-surface`

Read first:
- `/Users/imighty/Code/dxs-consigliere/doc/AGENTS.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/zone-catalog.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/ownership-matrix.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/provider-health-and-entity-ops-surface/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/provider-health-and-entity-ops-surface/slices.md`

Mission:
- add an operator-grade JungleBus block-sync health surface
- enrich address and token detail pages with meaningful operational summaries
- keep scope read-only and capability-focused
- do not expand provider config editing

Execution rules:
- split by zone if the work crosses storage/runtime/api/ui seams
- prefer explicit unavailable states over fake data
- keep Raven reads bounded and operator-safe
- add focused tests before closeout

Required closeout:
- update wave ledger statuses in `master.md`
- add `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/provider-health-and-entity-ops-surface/audits/A1.md`
- add `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/provider-health-and-entity-ops-surface/evidence/closeout.md`
- summarize honest residuals
