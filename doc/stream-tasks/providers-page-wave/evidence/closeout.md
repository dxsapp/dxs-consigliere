# Closeout

## Closed Slices

- `PP1` persisted provider override layer
- `PP2` effective provider config consumption
- `PP3` admin provider API contract
- `PP4` frontend Providers page
- `PP5` focused proof
- `PP6` docs and handoff

## Delivered Surface

Backend:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Controllers/AdminProvidersController.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/AdminProviderConfigService.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/ExternalChainProviderSettingsAccessor.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Models/Runtime/RealtimeSourcePolicyOverrideDocument.cs`

Frontend:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/ProvidersPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/providers.store.ts`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/RuntimePage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/routes/index.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/routes/AppShell.tsx`

Docs:
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-stack-and-rules.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-provider-policy.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-config-examples.md`

## Validation

- `dotnet build /Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj -c Release -p:UseAppHost=false`
- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~AdminProvidersControllerTests|FullyQualifiedName~AdminRuntimeControllerTests|FullyQualifiedName~AdminRuntimeSourcePolicyServiceTests|FullyQualifiedName~RealtimeSourcePolicyOverrideStoreIntegrationTests|FullyQualifiedName~BitailsRealtimeIngestRunnerTests"`
- `cd /Users/imighty/Code/dxs-consigliere/src/admin-ui && pnpm typecheck`
- `cd /Users/imighty/Code/dxs-consigliere/src/admin-ui && pnpm build`

## Honest Notes

- this wave broadened the previous realtime-only override into a bounded provider configuration overlay, but not a generic config system
- Bitails and WhatsOnChain credentials/base URLs are now surfaced through the provider contract because the page would be misleading otherwise
- JungleBus remains positioned as an advanced source; its broader subscription model is not simplified beyond the bounded fields exposed here
