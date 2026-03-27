# BSV Native Interpreter Lifecycle Wave Slices

## Slice Order

1. `L1-lifecycle-surface-inventory`
2. `L2-native-fullsystem-parity`
3. `L3-rooted-and-multisig-native-proof`
4. `L4-minimal-protocol-support`
5. `A1-closeout-audit`

## `L1-lifecycle-surface-inventory`

- zone: `verification-and-conformance`
- goal: map which broader DSTAS lifecycle suites currently prove semantics and which of them still lack native replay proof
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/**`
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-lifecycle-wave/master.md`
- completion:
  - inventory identifies existing lifecycle suites
  - each suite is tagged as native-covered, oracle-only, mixed, or not applicable
  - missing proof is narrowed to a concrete bounded set

## `L2-native-fullsystem-parity`

- zone: `verification-and-conformance`
- goal: replay broader DSTAS full-system lifecycle suites through the native interpreter
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/FullSystem/**`
  - `/Users/imighty/Code/dxs-consigliere/tests/Shared/**` only where native replay helpers are shared across lifecycle suites
- completion:
  - broader DSTAS lifecycle suites have explicit native replay paths
  - positive and negative lifecycle branches no longer depend on oracle-only escapes for execution truth
  - validation remains faithful to existing product semantics

## `L3-rooted-and-multisig-native-proof`

- zone: `verification-and-conformance`
- goal: extend native replay evidence to rooted-history and multisig-heavy DSTAS suites
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/RootedHistory/**`
  - `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/FullSystem/VNextDstasMultisigAuthorityValidationTests.cs`
  - narrow shared helpers under `/Users/imighty/Code/dxs-consigliere/tests/Shared/**` if needed
- completion:
  - rooted trusted/unknown-root flows have explicit native replay evidence
  - multisig owner and authority flows have explicit native replay evidence
  - any residual gaps are named precisely, not hidden in broad test green-ness

## `L4-minimal-protocol-support`

- zone: `bsv-protocol-core`
- goal: land only the smallest protocol-core changes required to let current lifecycle suites replay natively
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/**`
  - only additional repo-needed protocol files if a lifecycle-native blocker proves they are required
- completion:
  - protocol-core changes are strictly bounded to current lifecycle replay needs
  - no business-semantics leakage into the interpreter
  - targeted protocol tests prove the added support
- disallowed forms:
  - speculative generic opcode expansion
  - hidden policy broadening without matching proof

## `A1-closeout-audit`

- zone: `repo-governance`
- goal: record whether broader native lifecycle proof is now enough to reopen oracle demotion
- owned paths:
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-lifecycle-wave/audits/**`
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-lifecycle-wave/evidence/**`
  - `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-lifecycle-wave/master.md`
- completion:
  - audit references concrete lifecycle/native validation output
  - audit states one of:
    - evidence is sufficient to reopen oracle demotion
    - exact remaining blind spots still block it

## Validation

- `L1`:
  - inventory note with concrete suite list and current proof state
- `L2`:
  - focused `Dxs.Consigliere.Tests` DSTAS full-system pack
  - native interpreter proof paths demonstrated inside those suites
- `L3`:
  - focused rooted-history and multisig packs
  - native replay evidence recorded explicitly
- `L4`:
  - `Dxs.Bsv` build
  - targeted `Dxs.Bsv.Tests` packs for any protocol changes
- `A1`:
  - audit references concrete test output, residuals, and commit hashes
