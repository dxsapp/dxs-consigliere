# Reverse Lineage Validation Fetch Wave Closeout

## Result
The wave is complete.

`validation_fetch` now uses a bounded reverse-lineage strategy for tx-level `(D)STAS` repair:
- `JungleBus transaction/get` is the primary upstream path
- requests are throttled to `10 req/sec`
- traversal is bounded by visited-set, traversal-depth, and fetch-budget rules
- repair state exposes why traversal stopped and how much work was consumed

## Key Files
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Infrastructure/JungleBus/JungleBusRawTransactionClient.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Infrastructure/JungleBus/JungleBusProviderDiagnostics.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/IValidationDependencyService.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/ValidationDependencyService.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Models/Transactions/ValidationRepairWorkItemDocument.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Transactions/IValidationRepairWorkItemStore.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Transactions/ValidationRepairWorkItemStore.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/ValidationDependencyRepairProcessor.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/ValidationDependencyRepairScheduler.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/ValidationRepairStatusReader.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dto/Responses/ValidationRepairStatusResponse.cs`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/RuntimePage.tsx`

## Behavioral Summary
- reverse-lineage repair starts from the unresolved transaction and walks backward only through needed parent dependencies
- each repair uses a per-run visited set so duplicate ancestry fetches are skipped
- traversal stops on explicit boundaries instead of turning provider instability into protocol verdicts
- operator-facing runtime panels now show stop reason and traversal consumption metrics
- service bootstrap defaults and example configs now treat `validation_fetch` as JungleBus-first, with fallback providers still available for generic acquisition paths

## Honest Residual
- transaction-level reverse-lineage repair is now real, but broader token/root-expansion auto-heal still belongs to later work
- the current throttle is global to JungleBus raw-transaction acquisition and not partitioned per repair queue or per tenant/operator
- this wave intentionally does not broaden historical discovery semantics beyond bounded validation repair
