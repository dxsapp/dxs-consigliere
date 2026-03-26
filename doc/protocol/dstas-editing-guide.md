# DSTAS Editing Guide

## Entry points

Start here when editing DSTAS:
- protocol source-of-truth: `/Users/imighty/Code/dxs-consigliere/doc/protocol/dstas-source-of-truth.md`
- target architecture: `/Users/imighty/Code/dxs-consigliere/doc/protocol/dstas-refactor-target.md`
- active execution package: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dstas-ai-first-wave/master.md`

## Protocol core

Canonical DSTAS protocol seams live in:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Dstas/Models/`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Dstas/Parsing/`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Dstas/Validation/`
- façade compatibility layer: `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Validation/StasProtocolLineageEvaluator.cs` and `/Users/imighty/Code/dxs-consigliere/src/Dxs.Bsv/Tokens/Validation/StasLineageEvaluator.cs`

## Consigliere consumers

Runtime and persistence should consume protocol seams through adapters, not re-derive DSTAS ad hoc:
- transaction mapping: `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Transactions/Dstas/`
- token runtime adapters: `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tokens/Dstas/`
- remaining generic consumers:
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Models/Transactions/MetaOutput.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tokens/TokenProjectionRebuilder.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tokens/TokenProjectionReader.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Addresses/AddressProjectionRebuilder.cs`
  - `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Transactions/TransactionStorePatchScripts.cs`

## Mandatory validation

At minimum run the DSTAS-focused packs touched by the change:
- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dxs.Bsv.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~Dstas|FullyQualifiedName~StasLineageEvaluatorTests|FullyQualifiedName~LockingScriptReaderOnePassTests|FullyQualifiedName~UnlockingScriptReaderTests"`
- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~Dstas|FullyQualifiedName~TransactionStoreDstas|FullyQualifiedName~VNextDstas|FullyQualifiedName~StasProtocolProjectionSemanticsTests"`

## Edit rules

- change protocol semantics in `Dxs.Bsv` first
- then adapt `Consigliere` consumers
- keep Raven patch parity explicit
- do not add a second hidden semantic engine in `Consigliere`
- if a change crosses protocol, state, and tests, sequence it as `bsv-protocol-core -> indexer-state-and-storage -> verification-and-conformance`
