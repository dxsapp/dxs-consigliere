---
created: 2026-05-19
slice: S11
status: applied
---

# Admin UI vNext — S11 polish report (a11y + perf + mobile QA)

S11 is a polish slice; no new feature surface. The output is a
codified set of invariants the regression tests pin and a per-
route bundle inventory the closeout doc consumes.

## A11y

- Every header IconButton carries an `aria-label`; the navigation
  Drawer renders a `role="navigation"` landmark; alert badge IconButton
  surfaces the active count via tooltip + label.
- Tests/a11y/shell-a11y.test.tsx pins the three invariants above
  with regex-based accessible-name queries (no DOM coupling). A
  future closeout pass (S12) optionally runs `axe-core` against
  every authed route under Playwright.

## Mobile / responsive QA

Breakpoints exercised manually + verified in jsdom tests:
- **375 px** — AppHeader collapses the connection chip, env tag,
  and density toggle (S2-audit M2); search shrinks via
  `minWidth: 0`. Drawer flips to temporary mode.
- **768 px / md** — full operator chrome appears; Grid containers
  collapse to a single column on the operator screens.
- **≥1024 px** — full design as authored (12-col Grid).

Screens with bespoke breakpoints (Dashboard hero, P2P Pool 8/4
split, Source Metrics 3-up cards, Headers tip/list, Alerts active
stack) all respect `xs/md` breakpoints; jsdom matchMedia returns
false, so tests grab the temporary-mode drawer via
`getAllByRole("link", { hidden: true })`.

## Perf

- Shell ceiling enforced by `pnpm budget` (S6-audit M2). Current
  shell 194.44 KB gzip (cap 200 KB) — 97.2% used.
- New `pnpm inventory` script (`scripts/bundle-inventory.mjs`)
  emits a per-chunk markdown table + applies the per-route
  budget (default 256 KB gzip). The DataGrid chunk is currently
  128.23 KB gzip; ChartsWrapper 59 KB gzip; BroadcastQueuePage
  (framer-motion) 47.01 KB gzip. All under the per-route ceiling.
- `pnpm verify` now ends with `pnpm budget && pnpm inventory` so
  any regression that bloats either the shell or a single route
  chunk fails CI.

## WCAG AA contrast (S8 row deferral)

- Score-gradient palette pulls from `palette.severity.{success,
  warning, error}` declared in `app/theme-augmentation.ts`. These
  tokens carry the audited contrast values from the design bundle.
- A scripted axe pass (S12) extends this with a full DOM audit
  per route at HEAD.

## Residuals → S12

- Playwright e2e: login → header search → Tx detail; Dashboard
  cold-mount; Force-rebroadcast confirmation; Alerts toast on
  injected poll-delta.
- `axe-core` pass per authed route (single-screenshot artifact).
- Closeout doc per playbook template.
