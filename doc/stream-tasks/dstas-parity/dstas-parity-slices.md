# DSTAS Parity Implementation Slices

## Objective

Bring `/Users/imighty/Code/dxs-consigliere` to protocol, validation, persisted-state, and conformance parity for DSTAS flows consumed by `/Users/imighty/Code/dxs-bsv-token-sdk`, without broadening public API unless SDK-truth makes additional fields necessary.

## Source Of Truth

- `/Users/imighty/Code/dxs-bsv-token-sdk/docs/DSTAS_SDK_SPEC.md`
- `/Users/imighty/Code/dxs-bsv-token-sdk/docs/DSTAS_SCRIPT_INVARIANTS.md`
- `/Users/imighty/Code/dxs-bsv-token-sdk/docs/DSTAS_CONFORMANCE_MATRIX.md`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-flow.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-state-flows.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-swap-flows.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-swap-mode.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-master-lifecycle.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-multisig-authority-flow.test.ts`

## Zone Table

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| G01 | repo-governance | operator/governance | done | - | ledger + slice doc review | durable plan and evidence structure exist |
| P01 | bsv-protocol-core | operator/protocol | done | G01 | focused parser tests | unlocking reader handles DSTAS spend-type tails beyond simple p2pkh-like suffixes |
| P02 | bsv-protocol-core | operator/protocol | done | P01 | lineage evaluator tests | DSTAS event classification includes swap-state semantics required by SDK truth |
| P03 | bsv-protocol-core | operator/protocol | done | P01 | focused parser tests | owner/service/action-data parsing does not drift on MPKH and authority-heavy vectors |
| P04 | bsv-protocol-core | operator/protocol | done | P02,P03 | protocol regression tests | redeem/freeze/confiscation state-machine rules match SDK truth for regular and authority spends |
| S01 | indexer-state-and-storage | operator/state | done | P02 | state/query tests | Raven patch logic matches protocol evaluator for new DSTAS semantics |
| S02 | indexer-state-and-storage | operator/state | done | P03 | persistence mapping tests | persisted MetaOutput/MetaTransaction carry all SDK-parity DSTAS fields needed for downstream queries |
| S03 | indexer-state-and-storage | operator/state | done | S01,S02 | token/address projection tests | projections/revalidation behave correctly for DSTAS swap, freeze, confiscation, and redeem flows |
| A01 | public-api-and-realtime | operator/api | not_opened | S01,S02 | controller/DTO tests | outward DTO changes are made only where new DSTAS truth must be visible |
| V01 | verification-and-conformance | operator/verification | done | P01 | unlocking reader tests | positive and negative DSTAS unlocking tail cases cover simple, MPKH-owner, and authority-multisig shapes |
| V02 | verification-and-conformance | operator/verification | done | P02,P04 | evaluator + query-contract tests | protocol and Raven derivation agree on DSTAS event classification and redeem gating |
| V03 | verification-and-conformance | operator/verification | done | S01,S02 | store integration tests | stored DSTAS fields and derived tx state match evaluator truth |
| V04 | verification-and-conformance | operator/verification | done | P03,S03 | DSTAS vector/lifecycle suites | existing conformance vectors and lifecycle fixtures prove no semantic drift |
| V05 | verification-and-conformance | operator/verification | done | V04 | full focused test wave | DSTAS regression pack passes end-to-end |
| I01 | service-bootstrap-and-ops | operator/platform | not_opened | A01 | build/startup check | only needed if new DTO/config/DI wiring is introduced |
| C01 | repo-governance | operator/governance | done | V05 | ledger + evidence review | task package updated with final evidence and closeout summary |

## Execution Order

### Wave 1
- `G01`
- `P01`
- `V01`

### Wave 2
- `P02`
- `P04`
- `S01`
- `V02`
- `V03`

### Wave 3
- `P03`
- `S02`
- `S03`
- `V04`

### Wave 4
- `A01` only if protocol/state truth must become public
- `V05`
- `I01` only if startup/config wiring changes
- `C01`

## Explicit Scope Cuts

- No TypeScript SDK builders are ported into this repository.
- No new broad public API is added unless existing validate/query surfaces need new DSTAS truth fields.
- No unrelated ingest/provider changes.
- No generic STAS refactor unless required to keep DSTAS semantics coherent.

## Acceptance Criteria

1. `UnlockingScriptReader` correctly extracts DSTAS spend type from simple P2PKH-like tails and owner/authority multisig tails reflected by SDK unlock decomposition.
2. `StasLineageEvaluator` and Raven patch logic classify DSTAS events consistently, including swap-marked inputs, swap cancel, freeze, unfreeze, confiscation, and redeem gating.
3. Persisted transaction state remains in lockstep with evaluator truth.
4. Existing and new DSTAS tests prove parity against SDK documents and representative lifecycle/conformance vectors.
5. Any public DTO change is justified by SDK-truth visibility requirements and covered by controller tests.

## Evidence Paths

- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dstas-parity/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dstas-parity/evidence/`
