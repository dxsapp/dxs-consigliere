# simplified-wizard S2 — slice-audit prompt

Target: S2 (collapse wizard to 1 step). Diff: `1843bf4..6d03bf0`. Read §S2.

## What landed
- `setup-wizard.store`: step machine reduced to one step; providers/blockSync
  form state + per-step gating removed; `canSubmit` = admin valid;
  `buildRequest` sends admin + EMPTY providers/blockSync (backend reads empty
  as absent → seeded defaults).
- `SetupWizardPage`: single Step1AdminAccess, Stepper removed, copy reframed.
- Removed `steps/Step2Providers|Step3BlockSync|Step4Review` (no external importer).
- Store + page tests rewritten.

## Focus
1. **One required step.** A fresh wizard shows only the admin step and
   completing it reaches the app (no provider/block-sync gating).
2. **buildRequest empty-objects contract.** Confirm empty providers/blockSync
   serialize such that the S1 backend treats them as absent (no accidental
   provider apply). Cross-check with the S1 `hasProviderSelection` logic.
3. **No orphaned imports / dead refs** after removing the 3 step components;
   Settings provider/config screens still build + work.
4. **Types** stayed generated re-exports; bundle budget green.

## Validation
`pnpm verify` green (199 unit, budget under cap, contracts:check OK);
test:contract 24/24; secrets-lint 0.

Verdict + `C*|H*|M*|L*` findings.
