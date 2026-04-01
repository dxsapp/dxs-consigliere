Use [$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md).

Execute `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/first-run-setup-wizard-wave/` end-to-end as a bounded product-delivery wave.

Read first:
- `/Users/imighty/Code/dxs-consigliere/doc/AGENTS.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/zone-catalog.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/ownership-matrix.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/first-run-setup-wizard-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/first-run-setup-wizard-wave/slices.md`

Objective:
- replace the current provider-first first-run UX with a capability-first setup wizard
- keep provider complexity out of the first-run path
- move admin bootstrap into safely gated persisted setup state
- demote `/providers` to advanced settings + docs rather than first-run onboarding

Hard product rules:
- first-run questions must be about operator capabilities, not provider jargon
- wizard steps must be:
  1. admin access
  2. raw transaction source
  3. REST fallback
  4. realtime source
  5. review
- defaults must be:
  - raw tx = `JungleBus / GorillaPool`
  - rest fallback = `WhatsOnChain`
  - realtime = `Bitails websocket`
- setup must be unauthenticated only while setup is incomplete
- once setup is complete, bootstrap route must be closed or explicitly maintenance-gated
- `/providers` must remain in the product, but as advanced settings and provider docs

Execution order:
1. `FW1` capability matrix and onboarding contract
2. `FW2` persisted setup/bootstrap state
3. `FW3` setup API contract
4. `FW4` bootstrap gating and runtime integration
5. `FW5` capability-first setup wizard UI
6. `FW6` demote `/providers` to advanced settings
7. `FW7` focused verification
8. `A1` audit and closeout

Validation minimum:
- backend tests for setup gating and persisted setup state
- frontend `pnpm typecheck` and `pnpm build`
- explicit proof that empty DB goes to setup
- explicit proof that setup completion closes normal bootstrap access
- explicit proof that capability defaults and recommendations match product policy

Closeout requirements:
- update wave ledger statuses to `done`
- add `audits/A1.md`
- add `evidence/closeout.md`
- commit with a bounded product/setup message
