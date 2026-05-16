# Closeout

## Closed Slices

- `JB1` block-sync contract freeze
- `JB2` runtime block-processing fix
- `JB3` JungleBus block-sync integration
- `JB4` setup/API contract update
- `JB5` wizard UI update
- `JB6` runtime diagnostics and messaging
- `JB7` focused verification
- `A1` audit

## Delivered Surface

Runtime and orchestration:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Blocks/BlockProcessExecutor.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Blocks/ActualChainTipVerifyBackgroundTask.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Blocks/IJungleBusBlockSyncScheduler.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Blocks/JungleBusBlockSyncScheduler.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Blocks/JungleBusBlockSyncMonitorBackgroundTask.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/JungleBusSyncRequestProcessor.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Setup/HostedTasksSetup.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/appsettings.json`

Setup/API/UI:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/SetupWizardService.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dto/Requests/SetupCompleteRequest.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dto/Responses/Setup/SetupStatusResponse.cs`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/SetupPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/setup.store.ts`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/types/api.ts`

Verification:
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/BackgroundTasks/Blocks/JungleBusBlockSyncOrchestrationTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Controllers/SetupControllerTests.cs`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Data/Runtime/SetupWizardServiceTests.cs`

## Validation

- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~JungleBusBlockSyncOrchestrationTests|FullyQualifiedName~SetupControllerTests|FullyQualifiedName~SetupWizardServiceTests|FullyQualifiedName~SourceCapabilityRoutingTests"`
- `cd /Users/imighty/Code/dxs-consigliere/src/admin-ui && pnpm typecheck && pnpm build`
- `dotnet build /Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj -c Release -p:UseAppHost=false`

## Honest Notes

- the default product posture is now coherent: `Bitails websocket` for realtime ingest, `JungleBus / GorillaPool` for raw tx fetch and block sync, `WhatsOnChain` for simple REST fallback
- node RPC/ZMQ remains an advanced optional path; it is no longer a hidden prerequisite for default block synchronization
- runtime wiring changes still require restart before all hosted tasks fully adopt the new setup/provider state
- node-based reorg/chain-tip verification is still only active when block-backfill primary resolves to `node`
