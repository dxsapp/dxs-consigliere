# DSTAS Parity Test Wave Slices

## Objective

Mirror the high-value DSTAS test coverage from `/Users/imighty/Code/dxs-bsv-token-sdk` inside `/Users/imighty/Code/dxs-consigliere`, using this repo's parser, lineage, store, projection, and full-system surfaces. This wave is verification-first: production code changes are allowed only when a new DSTAS test exposes a real parity bug.

## Source Of Truth

- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-flow.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-state-flows.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-swap-flows.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-swap-mode.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-master-lifecycle.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-multisig-authority-flow.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/docs/DSTAS_SDK_SPEC.md`
- `/Users/imighty/Code/dxs-bsv-token-sdk/docs/DSTAS_CONFORMANCE_MATRIX.md`

## Slice Table

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| G01 | repo-governance | operator/governance | done | - | ledger review | durable plan and evidence paths exist |
| T01 | verification-and-conformance | operator/verification | done | G01 | `Dxs.Bsv.Tests` focused run | parser + conformance tests cover regular/freeze/unfreeze/confiscation/swap-cancel + MPKH-owner + authority tails |
| T02 | verification-and-conformance | operator/verification | done | T01 | `Dxs.Bsv.Tests` focused run | lineage tests cover redeem matrix, swap matrix, and negative state-machine branches analogous to SDK state suites |
| T03 | verification-and-conformance | operator/verification | done | T02 | `Dxs.Bsv.Tests` focused run | authority/multisig DSTAS tests exist for owner MPKH and authority multisig classification-sensitive flows |
| T04 | indexer-state-and-storage | operator/state | done | T02 | `Dxs.Consigliere.Tests` focused run | store/query derivation tests mirror the new protocol/state matrix |
| T05 | indexer-state-and-storage | operator/state | done | T04 | `Dxs.Consigliere.Tests` focused run | token/address projection tests cover DSTAS transfer, redeem, rollback, and validation-state transitions |
| T06 | verification-and-conformance | operator/verification | done | T05 | `Dxs.Consigliere.Tests` focused run | full-system DSTAS lifecycle parity tests cover issue->transfer->freeze/unfreeze, confiscation, swap/swap-cancel, and revalidation-sensitive paths |
| A01 | public-api-and-realtime | operator/api | not_opened | T04 | controller tests | open only if test wave proves existing DTO/API surface is insufficient |
| C01 | repo-governance | operator/governance | done | T06 | evidence review | ledger and closeout evidence updated with exact validations and residuals |

## Execution Order

### Wave 1
- `G01`
- `T01`
- `T02`

### Wave 2
- `T03`
- `T04`
- `T05`

### Wave 3
- `T06`
- `A01` only if required
- `C01`

## Scope Cuts

- No TypeScript SDK builder porting.
- No broad public API expansion unless a test proves a missing outward DSTAS truth requirement.
- No unrelated ingest/provider changes.
- No benchmark work in this wave.

## Acceptance Criteria

1. The repo contains DSTAS tests mirroring the SDK's major protocol branches: regular, freeze, unfreeze, confiscation, redeem, swap, swap-cancel, owner MPKH, authority multisig.
2. The same DSTAS semantics are asserted at parser/evaluator, store/query, and projection/full-system layers where those layers expose relevant truth.
3. Any bug found by the new tests is either fixed in the same wave or explicitly recorded as a residual with evidence.
4. The final validation set is reproducible via focused commands recorded in the closeout evidence.
