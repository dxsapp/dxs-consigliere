# Closeout

## Closed Slices

- `FW1` capability matrix and onboarding contract
- `FW2` persisted setup/bootstrap state
- `FW3` setup API contract
- `FW4` bootstrap gating and runtime integration
- `FW5` capability-first setup wizard UI
- `FW6` `/providers` demotion to advanced settings
- `FW7` focused verification
- `A1` audit

## Delivered Surface

Backend:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Controllers/SetupController.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Models/Runtime/SetupBootstrapDocument.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/ISetupBootstrapStore.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/SetupBootstrapStore.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/SetupWizardService.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Setup/ConsigliereAdminAuthService.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Setup/AdminAuthSetup.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/AdminProviderConfigService.cs`

Frontend:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/SetupPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/setup.store.ts`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/auth.store.ts`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/routes/index.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/routes/AuthGuard.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/ProvidersPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/providers.store.ts`

Docs:
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-stack-and-rules.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/first-run-setup-wizard-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/first-run-setup-wizard-wave/audits/A1.md`

## Validation

- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~AdminAuthControllerTests|FullyQualifiedName~AdminProvidersControllerTests|FullyQualifiedName~AdminRuntimeControllerTests|FullyQualifiedName~SetupControllerTests|FullyQualifiedName~SetupBootstrapStoreIntegrationTests|FullyQualifiedName~AdminRuntimeSourcePolicyServiceTests|FullyQualifiedName~ConsigliereConfigBindingTests"`
- `cd /Users/imighty/Code/dxs-consigliere/src/admin-ui && pnpm build`
- `dotnet build /Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj -c Release -p:UseAppHost=false`

## Honest Notes

- provider config still uses a bounded advanced overlay layer; this wave did not reintroduce generic appsettings editing
- setup wizard narrows first-run realtime options to the recommended/advanced provider set and deliberately does not expose every infrastructure permutation
- `Node ZMQ` stays available through advanced settings/runtime surfaces and was intentionally kept out of wizard v1
- runtime source/client wiring changes still need restart even though setup and provider selections are persisted immediately
