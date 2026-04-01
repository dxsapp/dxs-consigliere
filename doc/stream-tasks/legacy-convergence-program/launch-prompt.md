Use [$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md).

Execute `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/legacy-convergence-program/` as a bounded convergence program.

Read first:
- `/Users/imighty/Code/dxs-consigliere/doc/AGENTS.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/zone-catalog.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/ownership-matrix.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/root-semantics-glossary.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/legacy-convergence-program/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/legacy-convergence-program/slices.md`

Objective:
- converge old and new `Consigliere` models into one capability-first product and runtime contract
- keep `validation_fetch` as a critical first-class capability
- keep `Consigliere` as the authoritative local `(D)STAS` validation engine
- remove semantic drift between docs, config, admin UI, and runtime behavior

Canonical rules:
- `Bitails` = default `realtime_ingest`
- `JungleBus / GorillaPool` = default `block_sync` and preferred `raw_tx_fetch`
- `WhatsOnChain` = `REST fallback`
- providers supply data; `Consigliere` supplies `(D)STAS` truth
- `validation_fetch` is dependency acquisition for local validation, not external validation authority
- rooted token history is bounded by `trustedRoots[]`
- do not collapse `raw_tx_fetch`, `validation_fetch`, and `historical_token_scan`

Execution order:
1. `LC1` capability contract cleanup
2. `LC2` raw tx convergence
3. `LC3` validation capability convergence
4. `LC4` historical scan truthfulness
5. `LC5` broadcast multi-target
6. `LC6` dead legacy removal
7. `A1` closeout audit

Validation minimum:
- explicit docs proof that the capability model is coherent
- focused runtime/service tests for raw-tx routing, validation semantics, and broadcast policy
- frontend `pnpm typecheck` and `pnpm build` if operator-facing wording changes
- final build/test proof that no dead-legacy cleanup broke the runtime

Closeout requirements:
- update program ledger statuses
- add `audits/A1.md`
- add `evidence/closeout.md`
- commit per bounded wave, not as one giant convergence commit
