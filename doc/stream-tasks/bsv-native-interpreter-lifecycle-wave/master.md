# BSV Native Interpreter Lifecycle Wave Master Ledger

- Parent task: close the remaining native proof gap before re-running oracle demotion
- Branch: `codex/consigliere-vnext`
- Current status: `todo`
- Slice plan: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-lifecycle-wave/slices.md`
- Launch prompt: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-lifecycle-wave/launch-prompt.md`
- Depends on:
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-followup-wave/master.md`
  - commit `8e62ec8`

## Goal

Close the remaining native-interpreter proof gap by replaying broader DSTAS lifecycle suites through the native BSV interpreter, then use that evidence to reopen the oracle-role decision on a later audit pass.

## Fixed Decisions

- do not change DSTAS business semantics, rooted token semantics, or public API semantics in this wave unless a test reveals a real contract mismatch
- treat this as a proof-expansion wave, not a new interpreter feature wave
- prefer reusing existing lifecycle harnesses over inventing parallel fixture universes
- keep zone routing explicit: verification owns test and fixture changes; protocol core only takes minimal helper changes if the native evaluator needs narrow support for replayability
- do not claim oracle demotion in this wave unless the validation surface actually proves it

## Non-Goals

- no new opcode surface unless a current repo-owned lifecycle flow genuinely depends on it
- no runtime hot-path adoption in `Dxs.Consigliere`
- no broad fixture regeneration project
- no storage/history contract changes

## Active Slices

| slice | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|
| `L1-lifecycle-surface-inventory` | `operator/verification` | `todo` | - | inventory note in master evidence log | broader DSTAS lifecycle suites and native-proof gaps are mapped explicitly |
| `L2-native-fullsystem-parity` | `operator/verification` | `todo` | `L1-lifecycle-surface-inventory` | focused `Dxs.Bsv.Tests` and `Dxs.Consigliere.Tests` lifecycle/native packs | broader DSTAS full-system flows replay through native interpreter without oracle-only escapes |
| `L3-rooted-and-multisig-native-proof` | `operator/verification` | `todo` | `L2-native-fullsystem-parity` | focused rooted and multisig lifecycle packs | rooted token and multisig DSTAS flows have explicit native replay evidence |
| `L4-minimal-protocol-support` | `operator/protocol` | `todo` | `L1-lifecycle-surface-inventory` | targeted protocol tests | only minimal protocol-core changes, if truly required, are landed to support existing lifecycle replay |
| `A1-closeout-audit` | `operator/governance` | `todo` | `L2-native-fullsystem-parity`,`L3-rooted-and-multisig-native-proof`,`L4-minimal-protocol-support` | audit note + closeout | wave states whether the evidence is sufficient to reopen oracle demotion or what exact blind spots remain |

## Candidate Validation Surfaces

Primary expected suites:
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/FullSystem/VNextDstasFullSystemValidationTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/FullSystem/VNextDstasMultisigAuthorityValidationTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/RootedHistory/RootedDstasFullSystemValidationTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/RootedHistory/RootedDstasTokenReadSurfaceTests.cs`
- existing native parity packs under `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dstas/Conformance/**`

## Evidence Paths

- audits: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-lifecycle-wave/audits/`
- evidence: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-lifecycle-wave/evidence/`

## Hard Stops

Stop and open remediation if:
- native lifecycle proof is claimed while suites still rely on oracle-only execution branches
- a protocol-core code change broadens interpreter semantics beyond repo-needed lifecycle replay without dedicated proof
- rooted token semantics or DSTAS business meaning drift as an accidental side effect of native replay enablement
- multisig or rooted packs are silently excluded from the reported coverage

## Evidence Log

| date | slice | type | path_or_note | note |
|---|---|---|---|---|
| 2026-03-27 | kickoff | task-package | `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-lifecycle-wave/master.md` | lifecycle proof wave opened to close the remaining oracle-demotion blocker |
