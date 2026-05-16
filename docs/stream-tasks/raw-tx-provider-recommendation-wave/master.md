# Raw Tx Provider Recommendation Wave

## Goal

Reposition `raw_tx_fetch` in the product surface without rewriting the broader provider model.

The intended product posture after this wave:
- `Bitails` remains the recommended realtime provider
- `JungleBus / GorillaPool transaction get` becomes the recommended raw transaction source
- `WhatsOnChain` remains the easy REST fallback and onboarding provider, not the preferred raw-tx path

## Scope

In scope:
- provider messaging
- capability recommendation for `raw_tx_fetch`
- Providers page catalog/help copy
- platform docs that currently imply `WhatsOnChain` is the best raw-tx path
- optional bounded provider-catalog metadata change if the UI needs explicit `recommendedFor = raw_tx_fetch`

Out of scope:
- rewriting the full routing model
- changing realtime defaults
- pretending JungleBus is the default managed realtime source
- claiming formal unlimited SLA where the docs do not explicitly provide one
- broad historical-scan routing changes

## Product Rule

This wave should distinguish between:
- product recommendation based on actual operating behavior
- formally documented provider guarantees

That means the docs and UI may recommend `JungleBus / GorillaPool` as the best practical `raw_tx_fetch` path, but must avoid overstating official guarantees that are not clearly documented.

## Intended Product Position After Closeout

- `Bitails` = recommended realtime
- `JungleBus / GorillaPool` = recommended raw transaction fetch
- `WhatsOnChain` = simple fallback / general REST onboarding provider
- `JungleBus` still remains an advanced realtime option rather than the default realtime onboarding path

## Status Ledger

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `RT1` | `repo-governance` | `operator/docs` | `done` | - | docs inventory | all misleading raw-tx recommendations are located |
| `RT2` | `platform-product-surface` | `operator/product` | `done` | `RT1` | docs review | product wording is updated to position JungleBus raw-tx correctly |
| `RT3` | `frontend-admin-shell` | `operator/ui` | `done` | `RT2` | frontend build | Providers page copy and catalog wording reflect new positioning |
| `RT4` | `public-api-and-realtime` | `operator/api` | `done` | `RT2` | focused tests | optional bounded provider-catalog metadata update lands if needed |
| `RT5` | `repo-governance` | `operator/governance` | `done` | `RT3`,`RT4` | closeout docs | wave ledger and closeout reflect honest residuals |

## Recommended Scope

Default execution should stop at messaging and provider-catalog positioning.

Only broaden into runtime default changes if there is an explicit product decision to make `raw_tx_fetch` routing prefer JungleBus in live behavior, not just in onboarding copy.

## Definition of Done

- docs no longer imply `WhatsOnChain` is the preferred raw-tx provider
- Providers page clearly recommends `JungleBus / GorillaPool` for raw transaction fetch
- `Bitails` remains the clear realtime recommendation
- `WhatsOnChain` remains positioned as simple REST fallback/onboarding
- no unnecessary routing churn was introduced

## Validation Summary

Validated during closeout:
- `cd /Users/imighty/Code/dxs-consigliere/src/admin-ui && pnpm typecheck && pnpm build`
- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~AdminProvidersControllerTests|FullyQualifiedName~AdminRuntimeSourcePolicyServiceTests"`
- `dotnet build /Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj -c Release -p:UseAppHost=false`

## Honest Residuals

- this wave updated product messaging and provider-catalog metadata only
- `WhatsOnChain` remains the recommended REST default even though it is no longer presented as the preferred raw transaction source
- runtime `raw_tx_fetch` routing defaults were not changed in this wave
- no claim is made here about formal unlimited JungleBus SLA; the recommendation is based on practical product posture and operator experience
