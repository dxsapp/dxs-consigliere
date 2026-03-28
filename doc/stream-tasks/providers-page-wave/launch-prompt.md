Use [$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md).

Execute `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/providers-page-wave/` end-to-end as a bounded product-delivery wave.

Read first:
- `/Users/imighty/Code/dxs-consigliere/doc/AGENTS.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/zone-catalog.md`
- `/Users/imighty/Code/dxs-consigliere/doc/repository-zones/ownership-matrix.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-provider-policy.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/providers-page-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/providers-page-wave/slices.md`

Objective:
- deliver a dedicated Providers page that makes provider onboarding/configuration obvious for new `Consigliere` operators
- keep the wave bounded to provider onboarding + minimal configuration
- do not turn the admin shell into a generic config editor

Hard product rules:
- Realtime default is `Bitails`
- REST default is `WhatsOnChain`
- `JungleBus` is advanced realtime, not the default onboarding path
- `ZMQ` is advanced infrastructure and should explain both self-hosted node ZMQ and Bitails ZMQ-as-a-service posture
- page must clearly advertise what each provider can give the operator and how to connect it
- Runtime page should stay diagnostics-oriented; Providers page becomes the onboarding/configuration surface

Execution order:
1. `PP1` persisted provider override layer
2. `PP2` effective provider-config consumption
3. `PP3` admin provider API contract
4. `PP4` frontend Providers page
5. `PP5` focused proof
6. `PP6` docs, audit, and closeout

Validation minimum:
- backend build/tests for provider config and controller behavior
- frontend `pnpm typecheck` and `pnpm build`
- explicit proof of static vs override vs reset behavior
- explicit proof of invalid/missing-required-fields rejection

Closeout requirements:
- update wave ledger statuses to `done`
- add `audits/A1.md`
- add `evidence/closeout.md`
- commit with a bounded product/runtime message
