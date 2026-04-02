# Admin UI Operator Utility Wave Slices

## Overview

This wave exists to make the admin shell useful on first read.

The main anti-patterns to remove are:
- primary rendering through raw JSON/object dumps
- pages that all tell the same story with different words
- actions hidden below passive status walls
- wording that still sounds like internal backend implementation instead of operator meaning

## Slice Breakdown

### `AU1` Page Contract And Information Hierarchy

Intent:
- freeze the purpose of each top-level screen before polishing details

Owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/DashboardPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/RuntimePage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/StoragePage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/ProvidersPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md` only if copy/contract needs sync

Exact task:
- define what each page is for and remove overlap
- freeze which questions each page must answer:
  - `Dashboard`: what matters right now in tracked scope
  - `Runtime`: what is degraded, stalled, blocked, or unavailable
  - `Storage`: what persistence/storage posture is active
  - `Providers`: what is configured/recommended/active and what can be changed

Do not do:
- full visual redesign
- broad backend work
- history-heavy UX expansion

Validation:
- frontend build passes
- page purpose can be summarized in one sentence each without overlap

Completion signal:
- page roles are visible in headers and content structure, not only in docs

### `AU2` Infrastructure Surfaces Cleanup

Intent:
- make `Dashboard`, `Runtime`, `Storage`, and `Providers` immediately readable

Owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/DashboardPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/RuntimePage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/StoragePage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/ProvidersPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/components/**`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/dashboard.store.ts`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/ops.store.ts`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/types/api.ts` if small typing cleanup is required

Exact task:
- replace any remaining low-signal dumps with structured summary blocks
- emphasize useful metrics, state badges, and action guidance
- ensure `Storage` is not just a dump of admin + ops payloads
- ensure `Dashboard` is summary-first and not a second runtime page
- ensure `Providers` keeps advanced configuration but remains readable

Do not do:
- create new top-level routes
- add speculative provider features
- turn Runtime into a settings form

Validation:
- frontend build passes
- manual smoke of dashboard/runtime/storage/providers shows no primary JSON dump

Completion signal:
- an operator can understand the infrastructure story without expanding debug detail first

### `AU3` Entity Pages Usefulness Pass

Intent:
- make address/token screens useful for daily operator work

Owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/AddressesPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/TokensPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/AddressDetailPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/TokenDetailPage.tsx`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/address-*.ts`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/stores/token-*.ts`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/types/api.ts`
- small supporting DTO/API additions only if the current detail/list surfaces still cannot show useful state

Exact task:
- improve list-table columns so high-signal fields are visible without drilling in blindly
- improve detail-page hierarchy so operator sees:
  - readiness
  - current state
  - useful counts/timestamps
  - actionable next step
- keep scoped-history honesty visible but not dominating the whole page
- subordinate extra/debug fields behind structured inspectors

Do not do:
- implement full history browser
- build the future `v2` History section
- add bulk workflow UI

Validation:
- frontend build passes
- manual smoke on address/token list and detail routes

Completion signal:
- tracked entity pages feel like operator pages, not API object viewers

### `AU4` Copy, Badges, And State Clarity

Intent:
- remove ambiguous or backend-shaped wording that still confuses operators

Owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/pages/**`
- `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/components/**`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md` if needed

Exact task:
- normalize wording around:
  - scoped history
  - rooted backfill
  - readiness vs degraded vs authoritative
  - single-source assurance
  - provider health vs provider capability
- remove legacy labels that overpromise or expose backend implementation details too directly

Do not do:
- invent new backend states
- rename backend contract values in a way that hides truth

Validation:
- frontend build passes
- quick copy review against current product model

Completion signal:
- the shell uses clear, consistent operator language across screens

### `AU5` Verification And Closeout

Intent:
- prove the wave changed usability, not just code shape

Owned paths:
- package docs for closeout
- no production-path expansion beyond what previous slices already needed

Exact task:
- run `pnpm typecheck`
- run `pnpm build`
- do manual smoke across:
  - `/dashboard`
  - `/runtime`
  - `/storage`
  - `/providers`
  - `/addresses`
  - `/tokens`
  - one address detail page if data exists
  - one token detail page if data exists
- capture honest residuals in closeout docs

Do not do:
- claim usefulness without touching actual pages

Validation:
- commands above pass
- manual notes are recorded

Completion signal:
- package can be closed with honest proof and residuals

## Dependency Order

1. `AU1`
2. `AU2` and `AU3`
3. `AU4`
4. `AU5`

## Validation Matrix

- `AU1`: page-purpose review + `pnpm build`
- `AU2`: `pnpm typecheck` + `pnpm build` + runtime/manual page smoke
- `AU3`: `pnpm typecheck` + `pnpm build` + entity-page manual smoke
- `AU4`: `pnpm build` + copy review against current product docs
- `AU5`: final `pnpm typecheck`, `pnpm build`, closeout notes

## Closeout Requirements

At closeout add:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/admin-ui-operator-utility-wave/audits/A1.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/admin-ui-operator-utility-wave/evidence/closeout.md`

Closeout must include:
- what pages materially improved
- what remained noisy or backend-shaped
- whether any small API/DTO additions were required
- whether a second UI polish wave is still needed
