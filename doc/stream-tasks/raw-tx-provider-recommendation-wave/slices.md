# Slices

## `RT1` Docs Inventory

Find every place that currently implies or states that `WhatsOnChain` is the preferred raw-tx path.

Expected paths:
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-provider-policy.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-capability-matrix.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-config-examples.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`

## `RT2` Product Messaging Update

Update the product wording so that:
- `Bitails` remains recommended for realtime
- `JungleBus / GorillaPool` becomes the recommended raw transaction fetch path
- `WhatsOnChain` becomes the easy fallback / onboarding REST choice

Rules:
- do not overclaim formal guarantees that are not documented clearly
- distinguish recommendation from SLA

## `RT3` Providers Page Copy

Update Providers page text and provider-card positioning.

Expected changes:
- `Bitails` card emphasizes managed realtime ingest
- `JungleBus` card explicitly calls out raw transaction fetch recommendation
- `WhatsOnChain` card explicitly calls out fallback / easy starter role
- recommended setup copy stays coherent and non-contradictory

## `RT4` Optional Provider Catalog Metadata Update

If the frontend needs structured backend metadata to render the recommendation cleanly, update the bounded provider-catalog payload.

Allowed examples:
- add or change `recommendedFor[]` values
- refine provider descriptions

Not allowed:
- broad API redesign
- generic provider capability editor

## `RT5` Closeout

Update:
- wave ledger
- audit note
- closeout note

Document explicitly:
- what changed in provider messaging
- what did not change in runtime routing
- that `WhatsOnChain` still exists as fallback/onboarding REST
