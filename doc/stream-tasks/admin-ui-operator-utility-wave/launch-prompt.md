# Mission

Execute the `admin-ui-operator-utility-wave` package so the current admin shell becomes materially useful for operators.

This is a bounded admin-shell usefulness pass, not a redesign program.

# Package Path

- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/admin-ui-operator-utility-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/admin-ui-operator-utility-wave/slices.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/admin-ui-operator-utility-wave/launch-prompt.md`

# Constraints

- Keep the work centered in `/Users/imighty/Code/dxs-consigliere/src/admin-ui/src/**` unless a small supporting DTO/API addition is truly required.
- Do not build the future `v2 History` section.
- Do not add generic config-editor behavior.
- Do not let raw JSON/object dumps remain the primary content of operator pages.
- Preserve current product truth:
  - scoped history
  - local `(D)STAS` validation authority
  - JungleBus single-source assurance unless backend says otherwise

# Required Execution Order

1. Execute `AU1` first and freeze page purpose.
2. Execute `AU2` to clean infrastructure surfaces.
3. Execute `AU3` to clean entity list/detail usefulness.
4. Execute `AU4` to normalize wording and badges.
5. Execute `AU5` to validate and close out.

If a page cannot become useful without small API support, add the smallest backend contract necessary and keep it bounded.

# Validation

Required commands:

```bash
cd /Users/imighty/Code/dxs-consigliere/src/admin-ui && pnpm typecheck
cd /Users/imighty/Code/dxs-consigliere/src/admin-ui && pnpm build
```

Required manual smoke:
- `/dashboard`
- `/runtime`
- `/storage`
- `/providers`
- `/addresses`
- `/tokens`
- one address detail page if data exists
- one token detail page if data exists

# Closeout

When implementation is complete:
- update the package ledger in `master.md`
- add `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/admin-ui-operator-utility-wave/audits/A1.md`
- add `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/admin-ui-operator-utility-wave/evidence/closeout.md`

Closeout must explicitly state:
- what became more useful for operators
- what still feels noisy or incomplete
- whether another admin UI wave should follow immediately

# Commit / Report Expectations

- prefer one docs-only package commit before execution if the user wants commits
- prefer one or more implementation commits grouped by meaningful UI milestone
- report changed pages and the operator-facing effect, not just component names
- record implementation commit hashes back into `master.md`
