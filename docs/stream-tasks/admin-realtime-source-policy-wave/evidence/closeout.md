# Admin Realtime Source Policy Wave Closeout

## Completed

- persisted realtime policy override document and Raven storage seam
- effective realtime policy service over static config plus operator override
- runtime consumption of effective policy in realtime ingest/bootstrap paths
- admin API endpoints to read, apply, and reset realtime policy override
- admin Runtime page controls for narrow realtime policy mutation
- focused controller, service, store, and routing verification
- admin handoff docs updated for Runtime Sources panel and restart semantics

## Validation

- `cd /Users/imighty/Code/dxs-consigliere/src/admin-ui && pnpm typecheck`
- `cd /Users/imighty/Code/dxs-consigliere/src/admin-ui && pnpm build`
- `dotnet build /Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj -c Release -p:UseAppHost=false`
- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~AdminRuntimeControllerTests|FullyQualifiedName~AdminRuntimeSourcePolicyServiceTests|FullyQualifiedName~RealtimeSourcePolicyOverrideStoreIntegrationTests|FullyQualifiedName~BitailsRealtimeSubscriptionScopeProviderTests|FullyQualifiedName~SourceCapabilityRoutingTests|FullyQualifiedName~OpsControllerTests|FullyQualifiedName~AdminInsightsControllerTests|FullyQualifiedName~ConsigliereConfigBindingTests"`

## Residuals

- override scope is intentionally narrow: `primaryRealtimeSource` and `bitailsTransport` only
- runtime source-selection changes still require service restart to be fully effective
- no provider credentials, URLs, or non-realtime capability routing are editable from the shell
