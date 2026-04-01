# Validation Dependency Repair Wave Closeout

## Result
The wave is complete.

`validation_fetch` is now implemented as a concrete subsystem with:
- durable repair work items
- asynchronous dependency repair execution
- targeted revalidation of dependent STAS state
- ops visibility for pending/running/failed/blocked work

## Key Files
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Models/Transactions/ValidationRepairWorkItemDocument.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Transactions/IValidationRepairWorkItemStore.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Transactions/ValidationRepairWorkItemStore.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/IValidationDependencyRepairScheduler.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/ValidationDependencyRepairScheduler.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/IValidationDependencyRepairProcessor.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/ValidationDependencyRepairProcessor.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/ValidationDependencyRepairBackgroundTask.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/IValidationDependencyService.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/ValidationDependencyService.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/UpstreamTransactionAcquisitionService.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/IValidationRepairStatusReader.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/ValidationRepairStatusReader.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Controllers/OpsController.cs`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/RuntimePage.tsx`

## Behavioral Summary
- unresolved local validation can schedule durable repair work
- dependency acquisition is routed through provider capability resolution, not hardcoded provider-specific validation authority
- after fetched dependencies land, dependent STAS state is re-evaluated
- runtime UI shows queue pressure and recent failures instead of requiring Raven inspection

## Honest Residual
- auto-healing still focuses on transaction-level missing lineage dependencies
- more advanced rooted-history or token-wide repair scenarios can be layered later on top of this subsystem
- repo-wide warning cleanup remains separate work
