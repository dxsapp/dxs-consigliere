# simplified-first-run-wizard — closeout

## Result
The first-run wizard is now a single required step — create the admin account.
Providers + block sync are optional (Settings-managed); the backend no longer
forces a JungleBus subscription. A fresh operator reaches a working product
(P2P thin node enabled, add addresses/tokens, API + admin UI) with NO
third-party subscription — the "just turn it on" story for the demo video.

## Key files
- `src/Dxs.Consigliere/Data/Runtime/SetupWizardService.cs` — CompleteAsync
  optional providers/block-sync.
- `src/admin-ui/src/screens/setup-wizard/` — 1-step UI (store + page);
  Step2/3/4 removed.

## Behavioral summary
- install → create admin → done. No subscription/provider input required.
- Providers + block-subscription still configurable later under Settings.
- Legacy full-config completion still works (back-compat for any caller that
  sends the full payload).

## Residuals
- Live end-to-end (fresh stack → one-step wizard → working) is operator-run,
  evidence-pending.

## Delivery
| slice | commit | status |
|---|---|---|
| S1 backend (optional steps) | `1843bf4` | done |
| S2 frontend (1-step) | `6d03bf0` | done |
