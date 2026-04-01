# First-Run Setup Wizard Wave

## Goal

Replace the current provider-first first-run experience with a capability-first setup wizard that makes `Consigliere` understandable to a new self-hosted operator.

After this wave:
- first-run setup is guided by operator tasks, not provider jargon
- admin bootstrap can live in persisted setup state instead of static config only
- provider choices are made by capability:
  - admin access
  - raw transaction fetch
  - REST fallback
  - realtime ingest
- `/providers` remains in the product, but as advanced settings + provider docs, not the primary onboarding surface

## Product Position

`Consigliere` is an open-source self-hosted scoped BSV indexer.

A new operator should not need to understand:
- `static` vs `override` vs `effective`
- provider-specific subscription terminology
- every possible endpoint a provider offers

A new operator should only need to answer:
- do I want admin protection?
- where should raw transactions come from?
- what is my REST fallback?
- what is my realtime source?

## Canonical Defaults

Setup wizard defaults:
- `admin_access`: optional
- `raw_tx_fetch`: `JungleBus / GorillaPool`
- `rest_fallback`: `WhatsOnChain`
- `realtime_ingest`: `Bitails websocket`

Advanced alternatives:
- `raw_tx_fetch`
  - `JungleBus / GorillaPool`
  - `WhatsOnChain`
  - `Bitails`
- `rest_fallback`
  - `WhatsOnChain`
  - `Bitails`
- `realtime_ingest`
  - `Bitails websocket`
  - `Bitails ZMQ`
  - `JungleBus`
  - `Node ZMQ`

## Setup Flow

The wizard should be capability-first and ordered like this:

1. `Admin access`
- ask whether the operator wants admin login protection
- if yes, collect username and password

2. `Raw transaction source`
- explicitly explain this is the source used for fetching full tx hex by txid
- recommend `JungleBus / GorillaPool`

3. `REST fallback`
- recommend `WhatsOnChain`
- only show fields needed for the chosen provider

4. `Realtime source`
- recommend `Bitails websocket`
- only show fields needed for the chosen transport/provider

5. `Review`
- show a clean summary of selected capability choices
- explain restart/apply semantics honestly if needed

## Scope

In scope:
- persisted setup/bootstrap state in DB
- first-run setup route and gating
- DB-backed admin bootstrap state
- capability-first provider choices for first run
- minimal provider capability matrix of only what `Consigliere` really uses
- demotion of `/providers` from first-run onboarding to advanced settings + docs

Out of scope:
- generic config editor
- full provider routing matrix editor
- provider billing/purchase automation
- secret vault platform work
- exposing every possible provider feature in setup
- advanced block-backfill policy editing unless it is required by a chosen first-run capability

## Capability Matrix Requirement

Before implementation, freeze the minimal operator-visible capability set for setup v1:
- `admin_access`
- `raw_tx_fetch`
- `rest_fallback`
- `realtime_ingest`

Do not expose provider features outside that matrix in the wizard.

## Routing And Ownership

Primary zones:
- `repo-governance`
- `indexer-state-and-storage`
- `public-api-and-realtime`
- `service-bootstrap-and-ops`
- `verification-and-conformance`

Supporting zone:
- `frontend-admin-shell`
  - implemented under `src/admin-ui/**`
  - repository governance still owns the wave package and handoff docs

## Status Ledger

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `FW1` | `repo-governance` | `operator/governance` | `todo` | - | docs review | capability matrix and onboarding contract are frozen |
| `FW2` | `indexer-state-and-storage` | `operator/state` | `todo` | `FW1` | store tests | DB-backed setup/bootstrap documents exist |
| `FW3` | `public-api-and-realtime` | `operator/api` | `todo` | `FW2` | controller tests | setup read/write API exists and is correctly gated |
| `FW4` | `service-bootstrap-and-ops` | `operator/platform` | `todo` | `FW2`,`FW3` | runtime tests | bootstrap gating and admin-first-run behavior work |
| `FW5` | `frontend-admin-shell` | `operator/ui` | `todo` | `FW3`,`FW4` | frontend build + smoke | capability-first setup wizard exists |
| `FW6` | `frontend-admin-shell` | `operator/ui` | `todo` | `FW5` | page QA | `/providers` is demoted to advanced settings and no longer acts like first-run onboarding |
| `FW7` | `verification-and-conformance` | `operator/verification` | `todo` | `FW2`,`FW3`,`FW4`,`FW5`,`FW6` | focused proof | first-run, save, restart-required, and post-setup flows are covered |
| `A1` | `repo-governance` | `operator/governance` | `todo` | `FW7` | audit | old provider-first onboarding is retired cleanly |

## Hard Boundaries

- do not keep pushing the current `/providers` page as the first-run UX
- do not expose provider subscription jargon before the wizard has narrowed the capability choice
- do not build a generic config editor as part of setup
- do not introduce an admin bootstrap hole: unauthenticated setup access must exist only while setup is incomplete
- do not present more provider fields than the chosen capability/provider actually needs

## Definition of Done

- first-run setup is capability-first
- admin can be configured from DB-backed setup state
- raw transaction source is explicitly configurable and clearly recommends `JungleBus`
- REST fallback clearly recommends `WhatsOnChain`
- realtime clearly recommends `Bitails websocket`
- current `/providers` page is no longer the primary first-run control surface
- a new operator can configure `Consigliere` without understanding internal provider jargon
