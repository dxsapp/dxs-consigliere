# thin-node S5 — slice-audit prompt

Audit target: S5 (wizard Providers remodel → p2p primary default).
Diff: `e29f572..783726a`.

Read `master.md` Product Decisions 3-4, `slices.md` §S5.

## What landed
- `Step2Providers.tsx`: `PROVIDER_LABELS` (p2p → "Thin node (P2P)"); intro
  reframed (P2P-first, no URL/key); primary dropdowns marked
  "(recommended: Thin node)"; external sub-form headers relabeled
  Bitails/WhatsOnChain (fallback), JungleBus (fallback + historical block
  sync).
- `admin-systems-seed.ts` (mock): seedSetupOptions mirrors S1 backend —
  p2p default + first allowed for realtime + rawTx.
- No backend/DTO change: SetupWizardService's realtime filter only strips
  "node" (p2p passes through); S1 recommendations already return p2p;
  provider names are strings (P2P needs no URL/key).

## Focus
1. **No hand-mirrored DTO regression (wave-A4 S3 invariant).** Confirm
   `src/admin-ui/src/types/{admin,auth}.ts` stay generated re-exports;
   `contracts:check` passed (committed contracts still match backend → no
   DTO drift).
2. **Defaults persist p2p.** A defaults-only wizard completion must POST
   realtimePrimaryProvider="p2p" + rawTxPrimaryProvider="p2p", and
   `ApplyProviderConfigAsync` must ACCEPT them (S1 allowed lists include
   p2p). S5 confirmed acceptance — re-verify there's no
   `invalid_realtime_primary_provider`/`invalid_raw_tx_primary_provider`
   path for p2p.
3. **P2P needs no config field.** Confirm no spurious URL/key field was
   added for p2p; the JungleBus block-sync step (Step3) contract is
   untouched.
4. **Mock seed parity.** `admin-systems-seed.ts` change keeps the wizard's
   mock-driven tests aligned with the live contract — confirm it doesn't
   diverge from `GET /api/setup/options`.
5. **Review step.** `Step4Review.tsx` shows the raw value `p2p` (left
   untouched) — acceptable or should it humanize? (Low at most.)

## Validation evidence
- `dotnet build … -c Release` clean.
- `pnpm verify` green (ends `contracts:check OK`).
- `RAVEN_URL=…:18080 pnpm --dir src/admin-ui test:contract` 24/24.
- `secrets-lint` 0.

## Verdict + finding format
Verdict first; findings `C*|H*|M*|L*` with file:line, why, fix.
