# DSTAS Source Of Truth

## Goal

Identify the current DSTAS semantic seams that must stay aligned during refactor.

## Current canonical layers

### `bsv-protocol-core`

Protocol parsing and semantic classification currently live in:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/ScriptType.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Read/LockingScriptReader.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Script/Read/UnlockingScriptReader.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Validation/StasLineageEvaluator.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Validation/StasLineageEvaluation.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Validation/StasLineageInput.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Validation/StasLineageOutput.cs`

### `indexer-state-and-storage`

Persisted DSTAS mapping and derived state currently live in:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Models/Transactions/MetaOutput.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Models/Transactions/MetaTransaction.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Transactions/TransactionStorePatchScripts.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tokens/TokenProjectionRebuilder.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tokens/TokenProjectionReader.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Addresses/AddressProjectionRebuilder.cs`

### `verification-and-conformance`

DSTAS verification currently spans:
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Conformance/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Script/Read/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Tokens/Validation/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/FullSystem/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Data/Tokens/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Services/Impl/`

## Current AI-first weaknesses

1. DSTAS has no obvious bounded feature entrypoint.
2. `StasLineageEvaluator` name hides real DSTAS scope.
3. Business semantics are duplicated across protocol evaluator, Raven patch scripts, and runtime readers.
4. Tests are strong but not packaged as an obvious DSTAS map.
5. A safe DSTAS edit still requires loading too many cross-layer files.

## Refactor rule

The refactor must preserve one clear direction of truth:
- `Dxs.Bsv` owns canonical DSTAS parsing and semantic derivation.
- `Dxs.Consigliere` consumes that canonical contract for persistence and queries.
- tests prove parity and block semantic drift.
