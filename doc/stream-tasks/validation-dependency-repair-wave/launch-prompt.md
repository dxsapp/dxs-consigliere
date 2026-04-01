Implement `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/validation-dependency-repair-wave/` end-to-end.

Read first:
- `/Users/imighty/Code/dxs-consigliere/doc/AGENTS.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/zone-catalog.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/ownership-matrix.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/legacy-convergence-program/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/root-semantics-glossary.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/token-rooted-history-model.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/validation-dependency-repair-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/validation-dependency-repair-wave/slices.md`

Objective:
- turn `validation_fetch` into a real validation dependency repair subsystem
- keep `Consigliere` as the only authoritative local `(D)STAS` validation engine
- keep providers as dependency/data sources only

Required outcomes:
- durable validation repair work items
- a bounded repair worker/scheduler
- targeted revalidation after dependency acquisition
- API/runtime integration that keeps public validation local and truthful
- operator-visible unresolved/running/failed repair state

Hard boundaries:
- do not turn providers into token validation authorities
- do not collapse `raw_tx_fetch` and `validation_fetch`
- do not replace rooted-history semantics with generic explorer history
- do not build a generic workflow engine
- do not hide residual scenarios; document them honestly

Execution rules:
- split work by repository zone when the write scope crosses zone boundaries
- keep write sets explicit and narrow
- add focused tests for each new seam
- update wave ledger status as slices close
- write `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/validation-dependency-repair-wave/audits/A1.md`
- write `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/validation-dependency-repair-wave/evidence/closeout.md`

Validation minimum:
- focused backend tests for durable repair state, worker behavior, and targeted revalidation
- API proof for validate-flow integration
- any admin/ops UI proof required by the chosen surface
- final `dotnet build` for `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj`

Closeout requirements:
- commit bounded changes intentionally
- leave the worktree clean
- report residuals explicitly
