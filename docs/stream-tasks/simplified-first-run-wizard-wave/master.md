---
created: 2026-05-30
type: wave
parent: docs/stream-tasks/consigliere-thin-node-observer-program/
related: docs/stream-tasks/thin-node-primary-source-wave/ (p2p is the routed default);
         docs/stream-tasks/wizard-enabled-p2p-runtime-toggle-wave/ (wizard enables P2P live);
         docs/stream-tasks/first-run-setup-wizard-wave/ (the original multi-step wizard)
status: done (S1 backend + S2 frontend shipped)
---

# Simplified first-run wizard — 1 step (admin only)

The first-run wizard had four steps (Admin → Providers → Block sync → Review)
because configuring external providers + a JungleBus block subscription used
to be necessary. After the thin-node-primary waves that is no longer true:
the thin node is the default realtime + rawTx source, external providers are
auto-fallback, and the wizard already enables P2P on completion. Yet the
wizard still **forces a JungleBus block subscription** —
`SetupWizardService.CompleteAsync` throws `junglebus_block_sync_base_url_required`
/ `junglebus_block_subscription_id_required` — so today you literally cannot
finish setup without a third-party subscription. That directly contradicts the
product story.

This wave collapses the wizard to a single required step — **create the admin
account** — and makes providers + block sync optional, configurable later via
the existing Settings screens. Goal: a clean "install → create admin → add
addresses/tokens → working product (API + admin UI)" path with **no
subscriptions and no third-party providers required**, suitable to demo on
video.

## Product Decision

1. **Admin account is the only required setup step.** Completing it marks
   setup done and (per the prior wave) enables the thin node. No provider or
   block-subscription input is required to reach a working product.
2. **Providers + block sync become optional, Settings-managed.** When the
   wizard sends no provider/block-sync config, the backend keeps the seeded
   defaults (p2p primary realtime+rawTx, external fallback) and does NOT
   require a JungleBus subscription. Operators who want historical backfill or
   to tune fallbacks use the existing providers/configuration Settings screens
   afterward.
3. **No regression of the existing config surface.** The Settings/providers
   pages (already shipped) remain the place to set provider URLs/keys + the
   JungleBus block subscription. This wave only removes them from the
   *mandatory first-run path*.

## Scope

In scope:
- Backend: `SetupWizardService.CompleteAsync` — make `Providers` and
  `BlockSync` optional; skip provider-config apply + block-sub validation when
  absent (seeded defaults remain); keep admin validation + bootstrap +
  P2P-enable. DTO stays shape-compatible (fields become optional, not removed).
- Frontend: collapse the wizard UI to a single Admin-account step + Complete;
  remove the Providers + Block-sync steps from the first-run stepper (the
  store sends admin only). Keep the components/screens that Settings reuses.

Out of scope:
- Removing the providers/block-sync Settings screens (they stay).
- Changing provider routing defaults (already p2p-primary).
- Any change to what "Complete" enables (P2P-enable already shipped).

## Core Rules

1. **No mandatory third-party dependency in first-run.** After this wave a
   fresh operator can complete setup with zero external accounts.
2. **Backend tolerates absent provider/block-sync input** — applies seeded
   defaults, never throws for missing optional config.
3. **Contract discipline.** If the request DTO's `required` set changes,
   regenerate `swagger.json` + `api.generated.ts`; `contracts:check` green.
   Types stay generated re-exports (wave-A4 S3 invariant).
4. **Gates.** `dotnet build -c Release`; `pnpm verify`; `pnpm test:contract`;
   `secrets-lint`.
5. Hash-backfill discipline (separate commit, never `--amend`).

## Ownership Zones

- `setup-backend` — `SetupWizardService.cs` (CompleteAsync), `SetupCompleteRequest.cs`
  (optional semantics), regenerated contract. (S1)
- `setup-frontend` — `screens/setup-wizard/` (stepper + steps + store +
  buildRequest). Does NOT touch the providers/configuration Settings screens. (S2)

## Wave Ledger

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S1 | `setup-backend` | **done** | — | `dotnet build -c Release`; unit: CompleteAsync succeeds with admin-only request (no Providers, no BlockSync) + leaves seeded p2p defaults + still enables P2P; still rejects missing admin creds when admin enabled | Providers + BlockSync optional in CompleteAsync; no JungleBus requirement on the first-run path; contract regenerated if `required` changed | `audits/S1-slice-audit-prompt.md` |
| S2 | `setup-frontend` | **done** | S1 | `pnpm verify` + `pnpm test:contract` + `dotnet build -c Release`; a fresh wizard shows ONE step (admin) and completing it reaches the app | Wizard collapsed to admin-only step; store sends admin only; providers/block-sync no longer in the first-run stepper; no hand-mirrored DTO | `audits/S2-slice-audit-prompt.md` |

## Definition of Done

- A fresh install: install → create admin → done; no provider/subscription
  input required; P2P enabled; operator can add addresses/tokens + use API.
- Providers + block subscription still settable via Settings.
- `dotnet build -c Release` + `pnpm verify` + `pnpm test:contract` +
  `secrets-lint` green.
- `audits/A1.md` + `evidence/closeout.md`.

## Delivery Notes

| slice | commit | summary |
|---|---|---|
| S1 | `1843bf4` | CompleteAsync: optional providers/block-sync (no JungleBus requirement) |
| S2 | `6d03bf0` | wizard collapsed to 1 admin step; steps 2-4 removed |
| Audit folds | none | per-slice audit prompts written; no findings folded |
