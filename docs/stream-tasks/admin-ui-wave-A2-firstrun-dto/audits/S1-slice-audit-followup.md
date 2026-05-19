---
created: 2026-05-19
type: slice-audit-followup
parent: docs/stream-tasks/admin-ui-wave-A2-firstrun-dto/audits/S1-slice-audit-prompt.md
status: applied
---

# wave-A2 S1 slice-audit followup (APPROVE WITH CHANGES)

Codex slice-audit on `c431761`: APPROVE WITH CHANGES
(0C / 0H / 1M / 3L). All 4 findings closed in this commit.

## M1 — `contracts-check.mjs` leaked temp dirs on every CI run

**Verified:** `process.exit(...)` inside `try { ... }` tears
the Node process down before the `finally` block fires
(`scripts/contracts-check.mjs:77` in the prior version). The
auditor confirmed leftover `/private/var/.../T/consigliere-
contracts-*` directories after CI runs.

**Revision applied:**

- Replace every `process.exit(N)` inside the try with
  `process.exitCode = N; return;` from a new `runCheck()`
  async helper. The outer `try { await runCheck(); }
  finally { rmSync(tmp, ...); }` now always runs the
  cleanup, regardless of which exit branch fired.
- Manually re-verified post-fix on macOS:
  - clean tree: 0 leaked dirs after success
  - simulated drift: 0 leaked dirs after exit-1 path

## L1 — master.md S1 ledger row still marked todo

**Verified:** the wave ledger at `master.md:161` still read
`todo`, while Delivery Notes at `:193` already recorded
`56b55bb`. Both rows must agree.

**Revision applied:**

- S0 row flipped `todo → done` with the S0-audit fold
  reference (was missed during the S0 commit).
- S1 row flipped `todo → done` with this followup
  reference. The "done-when" column now records the
  deferred refactor explicitly as a wave-A3 residual so
  the slice contract is honest about its scope.

## L2 — naive line-by-line diff cascaded on inserts

**Verified:** The previous `diff(a, b)` walked both strings
index-by-index. A single-line insert at row N shifted every
subsequent line and printed up to 60 mismatched entries —
loud but unhelpful.

**Revision applied:**

- Replace the homegrown diff with a `git diff --no-index
  --unified=3 --no-color --no-pager <committed> <fresh>`
  child-process call. Git's LCS algorithm produces a tight
  unified-diff hunk:
  ```
  @@ -3656,4 +3656,3 @@ export interface components {
   }
   export type $defs = Record<string, never>;
   export type operations = Record<string, never>;
  -// DRIFT FOLD TEST
  ```
- Output capped at 120 diff lines (was 60 line-pairs); the
  truncation tail prints `… (N more diff lines)` so the
  CI log stays readable on large drifts.
- Graceful fallback: if `git` is absent (minimal container
  images), the script logs the structural size summary
  rather than crashing. Exit code still flips to 1.

## L3 — `types/admin.ts` + `types/auth.ts` doc comments said
"S3/S1 followup replaces with codegen"

**Verified:** Headers in both files claimed the codegen
followup would auto-generate them. wave-A2 master.md now
documents that the generated file is the GATE source, and
that screen migration is a wave-A3 residual.

**Revision applied:**

- `types/admin.ts` header rewritten to record: hand-mirror
  remains the screen-side source; `api.generated.ts` is
  the drift-gate source; screen migration deferred to
  wave-A3 once Swashbuckle NRT-aware required-field
  inference is wired.
- `types/auth.ts` header points at the admin.ts header
  for rationale (same residual).

---

## Summary

| Layer | Change |
|---|---|
| `scripts/contracts-check.mjs` | `try/finally` actually cleans tmp dirs (M1); switched to `git diff --no-index --unified=3` (L2) |
| `src/types/admin.ts` + `src/types/auth.ts` | header comments updated to match wave-A2 reality (L3) |
| `docs/.../master.md` | S0 + S1 ledger rows flipped to done; reference audit fold paths (L1) |

Verify: 33/33 vitest files · **178/178 cases** · shell
194.60 KB gzip · 10/10 Playwright · `pnpm contracts:check`
green clean tree, fails red on injected drift with a tight
hunk, leaves zero leaked temp directories on either path.

S1 slice-gate cleared. S2 (ASP.NET-host parity test) opens.
