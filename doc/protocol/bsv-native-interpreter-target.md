# BSV Native Interpreter Target

## Purpose

Add a native BSV-profiled transaction and script evaluation subsystem to `Dxs.Bsv` so the repository can reproduce current STAS/DSTAS script-valid truth locally.

This target is about execution truth, not token business semantics.

## Fixed Decisions

- profile: `BSV` only
- v1 scope: all script paths required by current repo STAS/DSTAS truth and lifecycle packs
- non-goal: universal support for arbitrary out-of-scope BSV contracts in v1
- boundary: interpreter returns execution truth only; protocol classification remains outside the interpreter
- oracle transition: current deterministic oracle remains during parity phase as a regression anchor

## V1 Definition Of Done

V1 is complete when all of the following are true:
- the repository exposes a stable native evaluation API from `Dxs.Bsv`
- vendored STAS/DSTAS truth fixtures can be replayed locally through the native interpreter
- native interpreter outcomes match the current deterministic oracle across conformance, owner, multisig, and lifecycle packs
- negative cases fail deterministically with stable error codes or reasons
- the interpreter does not depend on Raven, provider APIs, runtime services, or ad-hoc fixture loaders

## Scope Boundary

### Interpreter Owns

- script VM execution
- stack, alt-stack, and conditional execution state
- opcode dispatch for repo-needed BSV paths
- sighash/preimage generation
- `CHECKSIG` and `CHECKMULTISIG` verification
- input-pair evaluation using resolved prevouts
- transaction-level pass/fail with deterministic reasons
- optional debug trace metadata

### Interpreter Does Not Own

- STAS lineage semantics
- DSTAS business event classification
- rooted-history or trusted-root policy
- projection shaping
- storage/runtime orchestration

## Required Contracts

Core contracts should live under a dedicated evaluation seam in `Dxs.Bsv`.

Minimum contracts:
- `IPrevoutResolver`
- `BsvScriptExecutionPolicy`
- `DictionaryPrevoutResolver`
- `ScriptEvaluationResult`
- `BsvScriptTraceStep`
- `TransactionEvaluationResult`
- `TransactionEvaluationService`

Current entrypoints:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/IPrevoutResolver.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/DictionaryPrevoutResolver.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/BsvScriptExecutionPolicy.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/ScriptEvaluationResult.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/BsvScriptTraceStep.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/TransactionEvaluationResult.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/TransactionEvaluationService.cs`

## Policy Model

The interpreter is BSV-profiled from the start.

Policy model must explicitly define:
- mandatory validation rules
- opcode enablement/disablement for the supported BSV profile
- standard execution flags used by the repository truth model
- any repo-specific execution defaults needed for deterministic parity

Current proven policy surface:
- `ScriptVerify = Mandatory | ForkId`
- `AllowOpReturn = true` in repo-default parity mode
- supported forkid sighash variants:
  - `SIGHASH_ALL | FORKID`
  - `SIGHASH_NONE | FORKID`
  - `SIGHASH_SINGLE | FORKID`
  - each of the above with `ANYONECANPAY`
- non-forkid signatures are expected to fail script evaluation under repo-default policy

The interpreter should evaluate through an explicit policy input rather than hidden global defaults.

## Prevout Resolution

The interpreter must not fetch prevouts itself.

Required model:
- caller provides transaction plus resolver
- resolver returns previous output script and value context needed for evaluation
- storage/network/runtime concerns stay outside the interpreter boundary

## Trace Model

Trace is optional but recommended.

Minimum debug data:
- success/failure
- deterministic error code
- failing opcode index, if applicable
- failing opcode, if applicable

Verbose step-by-step trace can remain a later extension.

Current trace contract:
- trace is opt-in through `TransactionEvaluationService.EvaluateTransaction(..., enableTrace: true, traceLimit: ...)`
- per-input result may include `Trace`
- each step contains:
  - phase
  - program counter
  - opcode
  - stack depth
  - top-of-stack hex
  - alt-stack depth

## Oracle Transition

During parity adoption:
- current deterministic oracle remains authoritative as a regression anchor
- native interpreter becomes the new local execution engine under test
- parity suites compare native interpreter truth against oracle truth

After parity stabilizes, the oracle can be demoted from primary truth anchor to regression/backstop role.
Current repository decision is tracked in `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bsv-native-interpreter-followup-wave/audits/A1.md`.

## Validation Packs

Current required packs for native interpreter work:
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dstas/Conformance/DstasProtocolTruthOracleTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dstas/Conformance/DstasConformanceVectorsTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dstas/Conformance/DstasProtocolOwnerFixturesTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dstas/Conformance/NativeInterpreterParityTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/ScriptEvaluation/BsvSignatureHashVariantsTests.cs`

## Proven Scope Today

Today the repository proves native interpreter parity for:
- vendored DSTAS conformance vectors
- vendored protocol-owner chains, including deterministic completion of chain-local missing prevouts
- repo-needed forkid sighash variants for native signature verification

Not yet proven repository-wide today:
- full DSTAS master lifecycle native replay
- a broader audit-backed claim that native interpreter coverage fully replaces oracle primacy across all lifecycle and multisig packs

Still out of scope unless opened by a later wave:
- generic arbitrary BSV contract coverage
- runtime hot-path adoption inside `Dxs.Consigliere`
- any claim that business semantics moved into the interpreter

## Risks

Main risks:
- incorrect sighash behavior
- incorrect `CHECKMULTISIG` handling
- silent divergence from current oracle truth
- mixing execution truth with STAS/DSTAS business semantics
- over-claiming support beyond repo-needed BSV paths in v1

## Success Criteria

Success is not “VM exists”.

Success is:
- repository-local script-valid truth exists for current STAS/DSTAS scope
- parity with current oracle is demonstrated
- the subsystem is discoverable and reusable as a first-class `Dxs.Bsv` capability
