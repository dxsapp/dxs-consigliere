# Slices

## `BW1` Docs and Policy

Update product/admin docs so Bitails websocket is presented as the default realtime onboarding path.

Primary paths:
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-provider-policy.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`

Rules:
- say API key is optional for first-run websocket onboarding
- avoid claiming formal unlimited websocket guarantees

## `BW2` Example Config Cleanup

Update example config files so they do not imply that Bitails API key is mandatory for websocket startup.

Primary paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/appsettings.vnext.hybrid.example.json`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/appsettings.vnext.provider-only.example.json`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-config-examples.md`

## `BW3` Provider Catalog Semantics

Adjust bounded provider metadata and missing-requirements logic.

Primary path:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/AdminProviderConfigService.cs`

Rules:
- Bitails websocket should not be treated as broken merely because API key is empty
- provider description should state key is optional for start and useful for higher-limit/provider-paid usage

## `BW4` Providers Page Copy

Update `/providers` page copy so the onboarding story is explicit.

Primary path:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/ProvidersPage.tsx`

Rules:
- call out Bitails websocket as the default-on realtime path
- make API key optionality visible
- keep JungleBus raw-tx and WhatsOnChain fallback messaging coherent

## `BW5` Focused Proof

Minimum proof:
- frontend build passes
- focused backend tests pass
- config examples and provider status semantics are coherent

## `BW6` Closeout

Add audit and closeout notes that state:
- what changed in onboarding posture
- what still needs a key or paid plan
- what was intentionally not claimed about websocket guarantees
