# Slices

## `PP1` Persisted Provider Override Layer

Broaden the current narrow realtime-only override into a still-bounded provider configuration overlay.

Expected document shape:
- `OperatorProviderConfigOverrideDocument` or equivalent
- persisted outside static config files
- stores only fields required for Providers page v1:
  - `RealtimePrimaryProvider`
  - `RealtimeBitailsTransport`
  - `RestPrimaryProvider`
  - provider-specific key/endpoint values needed by UI
  - `UpdatedAt`
  - `UpdatedBy`
  - optional `Version`

Rules:
- no generic config blob
- no full capability-matrix editing
- no arbitrary fallback chains
- no billing or account-management data

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/`

## `PP2` Effective Provider Config Consumption

Make runtime selection consume the effective provider-config overlay for the bounded provider surface.

Expected behavior:
- no override -> defaults remain `Bitails` realtime and `WhatsOnChain` REST when configured that way
- override can change:
  - realtime primary provider
  - Bitails transport
  - REST primary provider
- non-targeted capability routing stays outside the UI surface for this wave

Rules:
- keep runtime behavior bounded
- do not create a second provider-routing system
- keep restart/apply semantics explicit

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/`

## `PP3` Admin Provider API Contract

Expose a dedicated admin API for the Providers page.

Read:
- `GET /api/admin/providers`

Write:
- `PUT /api/admin/providers/config`
- `DELETE /api/admin/providers/config`

Read DTO should include:
- recommended defaults
- static config
- override config
- effective config
- restartRequired
- provider catalog cards
- provider status / missing requirements
- provider help links

Write DTO should include only the bounded provider config fields needed by the page.

Rules:
- reject invalid provider selections with `400`
- reject missing required fields with `400`
- do not expose raw static-file editing through this contract

## `PP4` Frontend Providers Page

Add a dedicated `/providers` page in the admin shell.

The page must show:
- recommended defaults
- current effective setup
- provider catalog cards
- minimal provider configuration form
- apply/reset actions
- clear restart warning when needed

Rules:
- this page is onboarding + configuration, not diagnostics-first
- do not bury provider explanations in raw JSON panels
- keep action feedback toast-only
- mobile and desktop layouts both need to be readable

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/components/`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/types/`

## `PP5` Focused Proof

Minimum proof set:
- no override -> recommended/static/effective provider values are consistent
- override changes realtime provider
- override changes REST provider
- override changes Bitails transport
- reset removes override and restores static setup
- invalid provider values are rejected with `400`
- missing required key/endpoint fields are rejected honestly
- provider status clearly reports missing requirements

Likely test paths:
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Data/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Controllers/`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Services/`
- frontend build/typecheck as part of validation

## `PP6` Docs And Handoff

Update admin docs and wave closeout artifacts.

Must update:
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-stack-and-rules.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-provider-policy.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-config-examples.md`

Document explicitly:
- provider roles and recommendations
- required configuration fields per provider
- apply/reset semantics
- static vs override vs effective setup
- restart messaging
- ecosystem help-link expectations
