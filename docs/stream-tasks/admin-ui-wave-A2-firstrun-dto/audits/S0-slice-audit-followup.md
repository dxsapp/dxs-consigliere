---
created: 2026-05-19
type: slice-audit-followup
parent: docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/audits/S0-slice-audit-prompt.md
status: applied
---

# wave-A2 S0 slice-audit followup (APPROVE WITH CHANGES)

Codex slice-audit on `b09e8af`: APPROVE WITH CHANGES
(0C / 0H / 1M / 1L). Both findings closed in this commit.

## M1 — post-submit auth re-hydrate can leave stale `setupRequired`

**Verified:** `SetupWizardPage.tsx:59` awaited
`auth.hydrate()` and navigated to `/login` unconditionally.
`AuthStore.applySessionError` (root.ts:215) handles non-401
hydrate failures via `status = "error"` + `lastError = ...`
but does NOT clear `setupRequired`. A transient
`/api/admin/auth/me` failure after a successful
`POST /api/setup/complete` would land the operator on
`/login` with the stale "Setup required" banner + the
Go-to-setup CTA still visible — and AuthGuard would bounce
them back to `/setup` on the next click.

**Revision applied:**

- New `AuthStore.applySetupStatus(res)` (root.ts:189):
  applies the `setupRequired` + `adminEnabled` flags from a
  `SetupStatusResponse`-shaped payload synchronously, no
  network call.
- `SetupWizardPage.tsx`: the post-submit effect now calls
  `auth.applySetupStatus(store.submittedStatus)` BEFORE
  awaiting `auth.hydrate()`. The hydrate call is now
  best-effort + wrapped in `try { } catch {}` — failure no
  longer blocks navigation, and the local `setupRequired`
  state is already correct.
- New regression test: `clears auth.setupRequired
  post-submit even if hydrate fails (S0-audit M1)` —
  spies on `MockAuthClient.me` to reject once, walks the
  full wizard, asserts the redirect lands on `/login` AND
  `auth.setupRequired === false`.

## L1 — completed installs briefly see wizard chrome

**Verified:** `SetupWizardPage.tsx:82` returned the full
`<Card>` chrome (header, Stepper rail, "Consigliere —
first-run setup" subheader) before `getSetupOptions()`
resolved. For an already-completed install the redirect at
`:74` only fired AFTER the options call returned, so the
operator saw a brief flash of the Stepper before landing on
`/login`.

**Revision applied:**

- New early-return at `SetupWizardPage.tsx:104`: while
  `status === "loading" && !options`, render a neutral
  loading shell (centred `LinearProgress`, no chrome) with
  `data-testid="setup-wizard-loading"`.
- The original in-content `LinearProgress` switches to fire
  only for `status === "submitting"` (after the operator
  has already seen the form).
- New regression test: `renders a neutral loader before
  options resolve (S0-audit L1)` — mocks
  `getSetupOptions` to never resolve, asserts the loader
  test-id is present AND that none of the wizard chrome
  strings ("first-run setup", "Admin account") are in the
  DOM.

---

## Summary

| Layer | Change |
|---|---|
| `stores/root.ts` | NEW `AuthStore.applySetupStatus(res)` (M1) |
| `screens/setup-wizard/SetupWizardPage.tsx` | Apply setup status BEFORE hydrate; hydrate is best-effort try/catch; neutral loader before options resolve (M1, L1) |
| `screens/setup-wizard/SetupWizardPage.test.tsx` | +2 regression tests (M1 + L1) |

Verify: 33/33 vitest files · **178/178 cases** (+2 from
previous 176) · shell 194.60 KB gzip · 10/10 e2e green.

S0 slice-gate cleared. S1 (Swagger codegen) opens.
