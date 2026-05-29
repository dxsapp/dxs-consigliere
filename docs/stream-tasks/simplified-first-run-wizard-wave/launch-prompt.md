# Launch — simplified first-run wizard

## Mission
Collapse the first-run wizard to a single required step (create admin
account); make providers + block sync optional + Settings-managed, so a fresh
operator reaches a working product with NO third-party subscription.

## Package path
`docs/stream-tasks/simplified-first-run-wizard-wave/` — `master.md` (ledger +
decisions), `slices.md` (anchors + tasks). Update master as slices close.

## Constraints (frozen)
- Admin account is the only required step; absent providers/block-sync must
  NOT throw (seeded p2p defaults remain). Keep P2P-enable-on-complete.
- Don't remove the Settings provider/block-sync config screens.
- Types stay generated re-exports; regen swagger + api.generated if the
  request `required` set changes; contracts:check green.

## Execution order
S1 `setup-backend` (local) → regen contract → commit. Then S2 `setup-frontend`.

## Validation
`dotnet build Dxs.Consigliere.sln -c Release`; `pnpm --dir src/admin-ui verify`;
`RAVEN_URL=http://127.0.0.1:18080 pnpm --dir src/admin-ui test:contract`;
`bash scripts/secrets-lint.sh` → 0. E2E (operator-run): fresh stack →
one-step wizard → app reachable, no subscription.

## Closeout
Ledger done; hashes in master Delivery Notes (separate backfill commit);
`audits/A1.md` + `evidence/closeout.md`; per-slice `audits/S<n>-slice-audit-prompt.md`.
One commit per slice, co-author trailer.
