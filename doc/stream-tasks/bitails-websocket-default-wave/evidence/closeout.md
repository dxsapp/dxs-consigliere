# Closeout

## What Changed

- `Bitails websocket` is now described as the default-on realtime onboarding path
- `Bitails` API key is no longer presented as mandatory for first-run websocket onboarding
- provider examples omit `bitails.connection.apiKey` on websocket-first startup paths
- `/providers` now explains that the key can be left blank initially and added later for paid or higher-limit usage

## What Did Not Change

- no broad routing rewrite
- no provider billing or plan-management flow
- no formal claim that Bitails websocket is unlimited

## Validation

- `pnpm typecheck`
- `pnpm build`
- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~AdminProvidersControllerTests|FullyQualifiedName~AdminRuntimeSourcePolicyServiceTests"`
- `dotnet build /Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj -c Release -p:UseAppHost=false`
