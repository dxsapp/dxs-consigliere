# Bitails Websocket Default Wave

## Goal

Make `Bitails websocket` the honest default-on realtime onboarding path for new `Consigliere` operators.

The user-facing interpretation after this wave:
- Bitails websocket is enabled as the default realtime onboarding path
- a Bitails API key is not presented as mandatory for first-run websocket use
- a key remains the optional upgrade path for higher limits and paid provider features
- operators can still switch away from Bitails through the admin shell

## Product Rule

This wave must distinguish clearly between:
- default-on onboarding behavior
- paid/provider-specific upgrade behavior
- formal provider guarantees

The product may state that Bitails websocket is the default first-run path and that docs show websocket examples without a key.
The product must not overclaim a formally documented unlimited websocket SLA.

## Scope

In scope:
- product and admin docs
- example config cleanup
- provider-card and missing-requirements semantics for Bitails websocket
- Providers page copy showing optional API key for first-run websocket onboarding

Out of scope:
- broad routing rewrites
- generic provider enable/disable matrix editor
- billing or plan management flows
- claims that Bitails websocket is formally unlimited

## Intended Posture After Closeout

- realtime onboarding default = `Bitails websocket`
- raw tx recommendation = `JungleBus / GorillaPool`
- REST fallback/onboarding = `WhatsOnChain`
- Bitails API key = optional for first-run websocket onboarding, recommended for paid/higher-limit usage

## Status Ledger

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `BW1` | `repo-governance` | `operator/docs` | `done` | - | docs review | docs describe Bitails websocket as default-on without overstating guarantees |
| `BW2` | `platform-config-surface` | `operator/config` | `done` | `BW1` | example review | config examples no longer imply Bitails API key is mandatory for websocket startup |
| `BW3` | `public-api-and-realtime` | `operator/api` | `done` | `BW1` | focused tests | provider catalog and missing-requirements semantics reflect optional-key websocket onboarding |
| `BW4` | `frontend-admin-shell` | `operator/ui` | `done` | `BW3` | frontend build | Providers page explains default-on Bitails websocket and optional key clearly |
| `BW5` | `verification-and-conformance` | `operator/verification` | `done` | `BW2`,`BW3`,`BW4` | focused proof | backend/frontend proof confirms the new onboarding posture |
| `BW6` | `repo-governance` | `operator/governance` | `done` | `BW5` | closeout docs | wave ledger and closeout state exactly what changed and what did not |

## Definition of Done

- new operators see Bitails websocket as the default realtime path
- Bitails API key no longer looks mandatory for first-run websocket onboarding
- admin UI explains the optional-key posture clearly
- config examples do not contradict the new onboarding story
- no false guarantee about unlimited websocket usage is introduced

## Validation Summary

- `pnpm typecheck`
- `pnpm build`
- `dotnet test /Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release -p:UseAppHost=false --filter "FullyQualifiedName~AdminProvidersControllerTests|FullyQualifiedName~AdminRuntimeSourcePolicyServiceTests"`
- `dotnet build /Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dxs.Consigliere.csproj -c Release -p:UseAppHost=false`

## Residuals

- This wave changes onboarding posture, examples, and provider-card semantics only.
- Runtime routing defaults were not rewritten here.
- No formal unlimited websocket SLA claim is made for Bitails.
