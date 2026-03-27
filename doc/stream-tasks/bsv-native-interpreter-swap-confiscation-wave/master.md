# BSV Native Interpreter Swap + Confiscation Wave

## Scope

Bounded verification wave to close the remaining native-proof gaps called out by the prior lifecycle audit:
- positive native proof for swap initiation
- positive native proof for confiscation under multisig authority policy
- negative native proof for insufficient multisig authority signatures on confiscation
- rerun oracle demotion audit with updated evidence

## Zones

- `verification-and-conformance`
- `repo-governance`

## Status Ledger

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `SC1` | `verification-and-conformance` | `operator/verification` | `done` | - | fixture diff + oracle hash update | vendored conformance vectors include native `swap` and multisig confiscation proofs |
| `SC2` | `verification-and-conformance` | `operator/verification` | `done` | `SC1` | focused .NET test packs | broader DSTAS lifecycle suites call the new native proofs explicitly |
| `A1` | `repo-governance` | `operator/governance` | `done` | `SC2` | audit note + closeout evidence | oracle role is reassessed against the updated proof surface |

## Validation

- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dxs.Bsv.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~NativeInterpreterParityTests|FullyQualifiedName~DstasConformanceVectorsTests|FullyQualifiedName~DstasProtocolTruthOracleTests|FullyQualifiedName~BsvSignatureHashVariantsTests"`
- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~VNextDstasFullSystemValidationTests|FullyQualifiedName~VNextDstasMultisigAuthorityValidationTests"`

## Outcome

The wave stayed inside `tests/**` and `doc/**`. No protocol-core changes were required.
