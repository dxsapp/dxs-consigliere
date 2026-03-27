# BSV Native Interpreter Usage

## Purpose

Use the native interpreter in `Dxs.Bsv` when execution truth must be reproduced locally from:
- transaction hex
- explicit prevouts
- repository BSV execution policy

Do not use it for DSTAS/STAS business classification. That remains outside the interpreter.

## Entry Point

Primary entrypoint:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/TransactionEvaluationService.cs`

Exact public entrypoint:

```csharp
TransactionEvaluationResult EvaluateTransaction(
    Models.Transaction transaction,
    IPrevoutResolver prevoutResolver,
    BsvScriptExecutionPolicy? policy = null,
    bool enableTrace = false,
    int traceLimit = 2048)
```

Behavioral defaults:
- `policy = null` resolves to `BsvScriptExecutionPolicy.RepoDefault`
- `traceLimit` defaults to `2048`

Core contracts:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/IPrevoutResolver.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/DictionaryPrevoutResolver.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/BsvScriptExecutionPolicy.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/ScriptEvaluationResult.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/BsvScriptTraceStep.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Evaluation/TransactionEvaluationResult.cs`

## Minimal Flow

1. Parse transaction into `Dxs.Bsv.Models.Transaction`
2. Build an `IPrevoutResolver`
3. Call `EvaluateTransaction`
4. Inspect:
   - transaction-level `Success`
   - per-input `Success`
   - per-input `ErrorCode`
   - per-input `Detail`
   - optional `Trace`

## Repo-Default Policy

`BsvScriptExecutionPolicy.RepoDefault` currently means:
- `ScriptVerify = Mandatory | ForkId`
- `AllowOpReturn = true`

This default is chosen to match current repo-owned STAS/DSTAS parity truth.

Custom policy combinations are callable, but only `RepoDefault` plus the proven forkid matrix below are audit-backed today.

## Supported Sighash Matrix

Currently proven by tests:
- `SIGHASH_ALL | FORKID`
- `SIGHASH_NONE | FORKID`
- `SIGHASH_SINGLE | FORKID`
- each of the above with `ANYONECANPAY`

Non-forkid signatures are expected to fail under repo-default policy.

## Trace

Trace is opt-in:
- set `enableTrace: true`
- optionally set `traceLimit`

Trace is debug metadata, not semantic truth.
`traceLimit` is a ring-buffer cap per input; execution continues, but only the last `N` steps are retained.

## Error Contract

`ErrorCode` is currently a string contract, not a stable enum.

Current sources of values:
- synthetic service-level values such as `PrevoutMissing`
- otherwise `BsvScriptError.ToString()` names from the native evaluation core

Use `ErrorCode` for deterministic assertions, but do not assume it is a frozen public enum yet.

## Required Validation Packs

Before changing interpreter behavior, run at minimum:
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dstas/Conformance/DstasProtocolTruthOracleTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dstas/Conformance/NativeInterpreterParityTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/ScriptEvaluation/BsvSignatureHashVariantsTests.cs`

For DSTAS-facing changes, also run the broader DSTAS conformance pack.
