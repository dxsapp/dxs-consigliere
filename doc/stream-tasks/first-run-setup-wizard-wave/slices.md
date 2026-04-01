# Slices

## `FW1` Capability Matrix And Onboarding Contract

Freeze the minimal operator-visible setup capability matrix.

Must define explicitly:
- `admin_access`
- `raw_tx_fetch`
- `rest_fallback`
- `realtime_ingest`

Must also define:
- the defaults for each capability
- which provider choices are available for each capability
- which fields are required for each choice
- which choices are advanced and should remain out of the default onboarding path

Likely docs to update during implementation:
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-api-map.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-provider-policy.md`

## `FW2` Persisted Setup And Bootstrap State

Add DB-backed persisted setup/bootstrap state.

Expected persisted concepts:
- setup/bootstrap status document
- admin bootstrap state or credential document
- provider setup choices persisted by capability rather than raw provider matrix semantics

Rules:
- password must be stored only as hash
- setup completion must be explicit
- first-run detection must come from persisted state, not from a frontend guess

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Runtime/**`

## `FW3` Setup API Contract

Expose a narrow setup API for first-run flow.

Expected endpoints:
- `GET /api/setup/status`
- `GET /api/setup/options`
- `POST /api/setup/complete`

Optional split endpoints are allowed only if they reduce risk materially, but v1 should prefer one bounded final submit.

Rules:
- setup API is unauthenticated only while setup is incomplete
- after setup is complete, bootstrap route must be closed or explicitly maintenance-gated
- payloads should be capability-first, not provider-jargon-first

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Controllers/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Dto/**`

## `FW4` Bootstrap Gating And Runtime Integration

Wire setup/bootstrap state into runtime behavior.

Expected behavior:
- first-run setup route is available only while setup is incomplete
- admin auth behavior respects the new persisted bootstrap state
- runtime/provider config can consume the capability-first setup outputs without inventing a second config model

Rules:
- avoid double sources of truth between setup state and provider config
- keep restart/apply semantics explicit
- do not weaken admin policy accidentally when setup is complete

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Setup/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Program.cs`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Startup.cs`

## `FW5` Capability-First Setup Wizard UI

Build a new first-run wizard at `/setup`.

Wizard steps:
1. Admin access
2. Raw transaction source
3. REST fallback
4. Realtime source
5. Review

Rules:
- capability-first wording only
- show only fields relevant to the currently selected choice
- make defaults obvious
- keep mutation feedback consistent with admin shell patterns
- preserve mobile and desktop readability

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/**`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/**`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/routes/**`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/types/**`

## `FW6` Demote `/providers` To Advanced Settings

After setup wizard exists, simplify the role of `/providers`.

Expected behavior:
- `/providers` becomes advanced settings + provider docs + diagnostics-oriented control surface
- it should no longer act like the required first-run mental model
- outdated concepts like static/override/effective should be reduced or renamed if they remain visible

Rules:
- keep advanced provider help links
- keep current-state visibility
- remove first-run pressure and jargon-heavy presentation from the page

Likely paths:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/ProvidersPage.tsx`
- related admin docs/handoff docs

## `FW7` Focused Verification

Minimum proof set:
- empty DB -> app redirects to setup
- setup incomplete -> setup endpoints accessible without admin auth
- setup complete -> bootstrap route is no longer open in normal flow
- admin credentials are persisted hashed
- provider choices persist and map cleanly to runtime config
- raw tx source recommendation is `JungleBus`
- REST fallback recommendation is `WhatsOnChain`
- realtime recommendation is `Bitails websocket`
- `/providers` no longer acts like the main onboarding path

Likely test paths:
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Controllers/**`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Data/**`
- `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Setup/**`
- frontend `pnpm typecheck` and `pnpm build`

## `A1` Closeout Audit

Close out the wave with an honest audit.

Must answer:
- is provider-first first-run onboarding retired?
- is the wizard capability-first and understandable for a new operator?
- is admin bootstrap safely gated?
- what provider complexity still remains in advanced settings and why?

Closeout artifacts:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/first-run-setup-wizard-wave/audits/A1.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/first-run-setup-wizard-wave/evidence/closeout.md`
