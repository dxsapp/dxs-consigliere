# Consigliere v1 Release Readiness

## Intent

This note tracks whether `Consigliere v1` is ready to ship as a scoped operator product.

`v1` means:
- self-hosted scoped BSV indexer
- current managed state
- realtime ingest
- block sync
- local authoritative `(D)STAS` validation
- rooted token truth inside trusted roots

`v1` does **not** mean:
- explorer-grade full chain history
- unlimited historical archaeology
- automatic deep-history completeness for every token/address

Related source-of-truth docs:
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/managed-scope-model.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-provider-policy.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/source-capability-matrix.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/root-semantics-glossary.md`
- `/Users/imighty/Code/dxs-consigliere/doc/admin-ui/admin-ui-product-spec.md`
- `/Users/imighty/Code/dxs-consigliere/doc/platform-api/vnext-rollout-notes.md`

## Status Summary

Current working assessment:
- `v1 release candidate` for scoped operator usage
- not a full-history product
- main open items are now mostly release-hardening and backlog follow-ups, not product-definition gaps

## A. Product Scope Frozen

- [x] `v1` explicitly means scoped current-state indexer + realtime + block sync + local `(D)STAS` validation
- [x] full-history archaeology is explicitly out of `v1`
- [x] `v2` History section is backlog, not hidden scope

Notes:
- history-heavy UX is intentionally deferred to a dedicated `v2` section
- current detail pages expose scoped history posture instead of promising unlimited reconstruction

## B. Source / Provider Posture Frozen

- [x] realtime default = `Bitails websocket`
- [x] block sync default = `JungleBus`
- [x] raw tx default = `JungleBus / GorillaPool`
- [x] REST fallback = `WhatsOnChain`
- [x] provider copy in setup/admin/docs matches runtime reality

Notes:
- validation dependency repair is JungleBus-first with bounded reverse-lineage fetch
- provider setup is DB-backed and reflected through setup/admin docs

## C. Validation Posture Frozen

- [x] `Consigliere` is the validation authority
- [x] providers are data sources only
- [x] `validation_fetch` is described as dependency acquisition, not external validation
- [x] reverse-lineage repair is bounded and observable
- [x] rooted-history terms are stable:
  - `valid root`
  - `trusted root`
  - `illegal root`
  - `unknown root`
  - `B2G resolved`

Notes:
- `validation_fetch` is now a real dependency-repair subsystem
- transaction-level reverse-lineage repair is complete enough for `v1`

## D. Setup / Control Plane Ready

- [x] first-run wizard works from empty DB
- [x] admin bootstrap works
- [x] provider config is DB-backed
- [x] common provider changes do not require restart
- [x] `/providers` is advanced settings, not onboarding

Notes:
- restart is still required only for some advanced runtime wiring paths
- normal provider posture is now setup-first and DB-backed

## E. Runtime / Ops Ready

- [x] `/runtime` shows provider health
- [x] `/runtime` shows JungleBus block sync lag
- [x] `/runtime` shows chain-tip assurance
- [x] `/runtime` shows validation repair state
- [x] operator can distinguish healthy / catching up / degraded / unavailable states

Notes:
- current assurance mode is still mostly `single_source`
- this is acceptable for `v1`, but cross-check and remediation remain backlog items

## F. History Messaging Honest

- [x] UI does not promise unlimited full history
- [x] address/token pages say history is scoped
- [x] historical backfill warns about provider capacity, disk usage, and long-running sync
- [x] docs say deep history is not default `v1` posture

Notes:
- the current product posture is intentionally “managed scope first”
- operators are explicitly guided toward fresh-address consolidation when that is operationally cheaper

## G. API / Runtime Consistency

- [x] setup docs match actual endpoints
- [x] admin DTOs match UI assumptions
- [x] provider/runtime capability names are consistent enough for `v1`
- [x] no major legacy direct-provider bypasses remain in critical flows

Notes:
- legacy convergence program is materially complete for the `v1` product shape
- deeper future cleanup can still happen, but current inconsistency is no longer a blocker

## H. Validation / Build Proof

- [x] backend build passes
- [x] frontend typecheck/build passes
- [x] focused suites exist for provider routing, validation repair, setup/bootstrap, runtime ops, and tracked entity flows
- [x] `git diff --check` is used as a hygiene gate during bounded waves

Notes:
- this category should continue to be refreshed per release cut, not assumed forever

## I. Docker / Release Proof

- [ ] empty-DB `docker compose` first-run smoke has been re-run after the latest simplification and scoped-history copy changes
- [ ] setup wizard has been re-verified end-to-end in containerized run after the latest release-hardening changes
- [ ] runtime panels have been re-smoked in the latest compose image after the latest delivery waves
- [x] Docker/release workflow exists for tagged DockerHub publishing

Notes:
- this is the main remaining release-hardening bucket before calling `v1.0`
- these are execution checks, not architecture gaps

## J. Explicit Backlog, Not Hidden Release Blockers

- [x] secondary chain-tip cross-check is backlog
- [x] assurance-driven remediation is backlog
- [x] dedicated admin `History` section is backlog for `v2`
- [x] deeper token/root expansion auto-heal is backlog
- [x] frontend bundle size cleanup is backlog

Notes:
- all items in this section are useful but non-blocking for `v1 release candidate`

## Current Release Call

Current recommendation:
- `Consigliere` is at `v1 release candidate` status for scoped operator deployments

What is still needed before calling it a cleaner `v1.0`:
1. rerun empty-DB compose first-run smoke
2. rerun setup/admin/runtime smoke in the containerized image
3. confirm no regressions in current docs/UI after the scoped-history simplification

If those pass, the remaining gaps are backlog or polish, not core product uncertainty.
