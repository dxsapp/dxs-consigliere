# DSTAS Parity Closeout

## Implemented Changes

- `UnlockingScriptReader` now extracts `DstasSpendingType` from both simple single-sig tails and authority/owner multisig tails that end with MPKH preimages.
- `StasLineageEvaluator` now classifies:
  - `swap` for regular spends of swap-marked DSTAS inputs
  - `swap_cancel`, `freeze`, `unfreeze`, `confiscation` as before
  - redeem only when the current DSTAS owner already matches the issuer redeem address
- Raven patch derivation in `TransactionStorePatchScripts` matches the updated evaluator semantics.
- `TokenProjectionRebuilder` now preserves `ProtocolType=dstas` for DSTAS redeem history via `DstasSpendingType`.

## Validation

### Protocol-focused tests

```bash
dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Bsv.Tests/Dxs.Bsv.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~LockingScriptReaderOnePassTests|FullyQualifiedName~DstasConformanceVectorsTests|FullyQualifiedName~UnlockingScriptReaderTests|FullyQualifiedName~StasLineageEvaluatorTests"
```

Result: `Passed: 15, Failed: 0`

### State/API-focused tests

```bash
PATH=/Users/imighty/.dotnet-vnext:$PATH DOTNET_HOST_PATH=/Users/imighty/.dotnet-vnext/dotnet /Users/imighty/.dotnet-vnext/dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~TokenProjectionRebuilderIntegrationTests|FullyQualifiedName~MetaOutputDstasMappingTests|FullyQualifiedName~TransactionControllerValidateStasTests|FullyQualifiedName~TransactionStoreQueryContractTests|FullyQualifiedName~UpdateStasAttributes_MapsSwapEventFromSwapMarkedInput|FullyQualifiedName~UpdateStasAttributes_SetsFreezeEventAndContinuity|FullyQualifiedName~UpdateStasAttributes_BlocksRedeemWhenInputIsFrozen|FullyQualifiedName~UpdateStasAttributes_RequiresCurrentOwnerToMatchIssuerForRedeem|FullyQualifiedName~UpdateStasAttributes_RecognizesRedeemWhenIssuerIsCurrentOwner"
```

Result: `Passed: 19, Failed: 0`

## Not Opened

- `A01 public-api-and-realtime`: existing `ValidateStasResponse` surface remained sufficient; no SDK-truth requirement forced new outward fields.
- `I01 service-bootstrap-and-ops`: no DI, config, startup, or packaging changes were required.
