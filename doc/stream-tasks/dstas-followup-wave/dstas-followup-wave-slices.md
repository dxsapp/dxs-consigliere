# DSTAS Follow-Up Wave Slices

## Objective

Close the two next DSTAS verification gaps after the parity test wave:
1. add a vector-driven `TransactionStore` parity harness that consumes the shared DSTAS conformance fixture directly
2. add a bounded DSTAS multisig owner/authority world-state suite mirroring the highest-value SDK lifecycle slices

## Source Of Truth

- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-conformance-vectors.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/fixtures/dstas-conformance-vectors.json`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-multisig-authority-flow.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/tests/dstas-master-lifecycle.test.ts`
- `/Users/imighty/Code/dxs-bsv-token-sdk/docs/DSTAS_SDK_SPEC.md`
- `/Users/imighty/Code/dxs-bsv-token-sdk/docs/DSTAS_CONFORMANCE_MATRIX.md`

## Slice Table

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| G01 | repo-governance | operator/governance | in_progress | - | ledger review | durable follow-up package exists |
| F01 | verification-and-conformance | operator/verification | todo | G01 | focused `Dxs.Consigliere.Tests` run | new vector-driven `TransactionStore` parity suite exists and exercises representative conformance vectors directly |
| F02 | verification-and-conformance | operator/verification | todo | F01 | focused `Dxs.Consigliere.Tests` run | bounded multisig owner/authority world-state suite exists and covers positive and negative authority branches |
| F03 | indexer-state-and-storage | operator/state | todo | F01,F02 | focused regressions | any runtime parity bug exposed by F01/F02 is fixed and regression-tested |
| C01 | repo-governance | operator/governance | todo | F03 | evidence review | ledger and closeout evidence updated with exact validations and residuals |

## Scope Cuts

- No public API expansion unless a new suite proves an outward DTO gap.
- No broad SDK builder port.
- No unrelated provider/ingest work.
- Keep the multisig world-state suite bounded; do not recreate the entire SDK mega-lifecycle.

## Acceptance Criteria

1. `TransactionStore` parity is proven directly against shared DSTAS vector fixtures, not only handwritten synthetic transactions.
2. Multisig owner/authority world-state coverage exists for at least one positive authority cycle and one negative authority failure class.
3. Any runtime bug exposed by these suites is fixed or recorded with explicit evidence.
4. Focused validation commands are recorded in closeout evidence.
