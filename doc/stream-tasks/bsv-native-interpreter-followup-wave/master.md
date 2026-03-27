# BSV Native Interpreter Follow-Up Master Ledger

## Header

- Parent task: close the next bounded native-interpreter steps after the parity blocker was cleared
- Branch: `codex/consigliere-vnext`
- Current status: `todo`
- Slice plan: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-followup-wave/slices.md`
- Launch prompt: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-followup-wave/launch-prompt.md`
- Depends on:
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-wave/master.md`
  - commit `1c4b827`

## Goal

Finish the next two bounded steps:

1. make native protocol-owner parity complete by introducing an explicit prevout completion strategy for incomplete fixtures
2. stabilize the public interpreter surface and run the audit that decides whether the deterministic oracle can be demoted from primary truth anchor to regression anchor

## Fixed Decisions

- do not mutate runtime semantics, storage semantics, or DSTAS business meaning in this wave
- do not invent missing prevouts heuristically; prevout completion must be deterministic and explainable
- protocol-owner completion may use chain-local tx graph reconstruction, fixture augmentation, or a dedicated resolver composition, but the strategy must be explicit in code and docs
- `I11` and `A1` open only after protocol-owner parity is no longer partial
- oracle demotion is an audit decision, not an implementation slogan

## Non-Goals

- no new BSV opcode surface
- no new non-forkid behavior changes
- no broad runtime adoption of the interpreter in `Dxs.Consigliere`
- no fixture churn without justification and reproducible provenance

## Active Slices

| slice | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|
| `P1-protocol-owner-prevout-completion` | `operator/verification` | `todo` | - | protocol-owner native parity tests | protocol-owner fixtures can be replayed natively without skipping incomplete prevout cases |
| `P2-public-api-and-docs` | `operator/protocol` | `todo` | `P1-protocol-owner-prevout-completion` | API/docs review + focused build/tests | native interpreter public surface and trace/policy contract are documented and stable |
| `P3-oracle-demotion-audit` | `operator/governance` | `todo` | `P2-public-api-and-docs` | audit note | audit states whether oracle can be demoted, and under what residual constraints |

## Evidence Paths

- audits: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-followup-wave/audits/`
- evidence: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-followup-wave/evidence/`

## Hard Stops

Stop and open remediation if:
- prevout completion starts depending on Raven/runtime/network services
- protocol-owner parity becomes “green” only by skipping or weakening script verification
- public interpreter docs describe support that the tests do not prove
- audit claims oracle demotion while known incomplete parity slices remain open

