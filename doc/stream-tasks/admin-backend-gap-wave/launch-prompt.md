Use [$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md).

Parent task: close the missing backend contracts for the Consigliere operator admin shell.

Read first:
- `/Users/imighty/Code/dxs-consigliere/doc/AGENTS.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/zone-catalog.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/ownership-matrix.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/admin-backend-gap-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/admin-backend-gap-wave/slices.md`

Execution rules:
- split by zone, do not mix state/store and controller transport blindly
- implement `AB1 -> AB2 -> AB3 -> AB4`
- keep untrack semantics tombstone-based, not destructive purge
- keep rooted token findings explicit in outward DTOs
- no frontend work in this wave
- validate with focused build/tests before closeout

Closeout requirements:
- update wave ledger statuses
- add audit/closeout note if any backend gap remains intentionally deferred
