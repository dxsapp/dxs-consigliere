# DSTAS Test Wave Closeout

## Scope

This wave mirrored high-value DSTAS parity coverage from `/Users/imighty/Code/dxs-bsv-token-sdk` into `/Users/imighty/Code/dxs-consigliere` across:
- parser and unlocking tail detection
- lineage classification and redeem/state rules
- persisted DSTAS field mapping
- Raven-backed transaction-state derivation
- token projection validation transitions
- bounded full-system DSTAS replay and reorg correctness

## Completed Slices

- `G01` durable test-wave package created and maintained
- `T01` parser and conformance coverage expanded
- `T02` lineage matrix expanded for unfreeze, confiscation, swap-cancel, optionalData continuity, redeem-after-unfreeze
- `T03` authority-tail coverage strengthened in unlocking-script tests
- `T04` transaction-store DSTAS derivation tests expanded
- `T05` token projection DSTAS rollback and validation-status transitions added
- `T06` bounded full-system DSTAS lifecycle suite added in `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/FullSystem/VNextDstasFullSystemValidationTests.cs`
- `A01` remained `not_opened`; no outward DTO/API gap was proven by this wave
- `C01` closeout evidence recorded

## Runtime Fix Triggered By The Test Wave

The new DSTAS full-system scenarios exposed a real projection bug in `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Addresses/AddressProjectionRebuilder.cs`.

Problem:
- same-address same-token debit+credit transitions such as DSTAS `unfreeze` and `swap_cancel` could try to delete and recreate the same address-balance document in one Raven session
- this caused `NonUniqueObjectException`

Fix:
- coalesce balance deltas by `(address, tokenId)` inside one apply/revert mutation before writing balance documents
- preserve `null` token ids as `null`, not `string.Empty`, so BSV and token balance ids remain correct

Regression pack was run after the fix.

## Files Added Or Expanded

### Verification
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Script/Read/UnlockingScriptReaderTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Tokens/Validation/StasLineageEvaluatorTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Conformance/DstasConformanceVectorsTests.cs`

### State / Mapping / Projection
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Services/Impl/TransactionStoreIntegrationTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Data/Models/Transactions/MetaOutputDstasMappingTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Data/Tokens/TokenProjectionRebuilderIntegrationTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/FullSystem/VNextDstasFullSystemValidationTests.cs`

### Runtime Fix
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Addresses/AddressProjectionRebuilder.cs`

## Validation Commands

### Verification wave
```bash
PATH=/Users/imighty/.dotnet-vnext:$PATH DOTNET_HOST_PATH=/Users/imighty/.dotnet-vnext/dotnet /Users/imighty/.dotnet-vnext/dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dxs.Bsv.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~UnlockingScriptReaderTests|FullyQualifiedName~StasLineageEvaluatorTests|FullyQualifiedName~DstasConformanceVectorsTests"
```

Result:
- `Passed: 20`
- `Failed: 0`

### State / projection / full-system wave
```bash
PATH=/Users/imighty/.dotnet-vnext:$PATH DOTNET_HOST_PATH=/Users/imighty/.dotnet-vnext/dotnet /Users/imighty/.dotnet-vnext/dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~TransactionStoreIntegrationTests|FullyQualifiedName~TokenProjectionRebuilderIntegrationTests|FullyQualifiedName~MetaOutputDstasMappingTests|FullyQualifiedName~VNextDstasFullSystemValidationTests"
```

Result:
- `Passed: 24`
- `Failed: 0`

### Address projection regression wave
```bash
PATH=/Users/imighty/.dotnet-vnext:$PATH DOTNET_HOST_PATH=/Users/imighty/.dotnet-vnext/dotnet /Users/imighty/.dotnet-vnext/dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~AddressProjectionRebuilderIntegrationTests|FullyQualifiedName~TransactionStoreQueryContractTests|FullyQualifiedName~TransactionControllerValidateStasTests"
```

Result:
- `Passed: 12`
- `Failed: 0`

## Residuals

- No public DTO/API expansion was required.
- The suggested `TransactionStoreVectorParityIntegrationTests` harness was not added in this wave because the expanded focused transaction-store integration tests plus conformance/vector and full-system layers already exposed and closed the real parity gaps found here.
- If another DSTAS parity wave is needed later, the next best incremental slice is a dedicated vector-driven `TransactionStore` parity harness that consumes the shared conformance fixture directly.
