# Slices — simplified first-run wizard

Source of truth: `master.md`. Co-author trailer on commits:
`Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
On ambiguity: smallest safe call, document in A1, continue.

## Anchors
- `src/Dxs.Consigliere/Data/Runtime/SetupWizardService.cs` — `CompleteAsync`
  currently: validates admin → validates `blockSync.BaseUrl` +
  `BlockSubscriptionId` (THROWS if missing) → merges blockSync into the
  junglebus payload → `providerConfigService.ApplyProviderConfigAsync(...)` →
  writes `SetupBootstrapDocument` → `runtimeSettings.SetP2pEnabledAsync(true)`.
- `src/Dxs.Consigliere/Dto/Requests/SetupCompleteRequest.cs` — `Admin`,
  `Providers`, `BlockSync` sub-objects.
- `src/Dxs.Consigliere/Data/Runtime/AdminProviderConfigService.cs` —
  `BuildDefaultProviderConfigDocument` already seeds p2p-primary defaults
  (so skipping ApplyProviderConfigAsync leaves sane config).
- Frontend: `src/admin-ui/src/screens/setup-wizard/` —
  `setup-wizard.store.ts` (steps, `buildRequest`), `steps/Step1Admin.tsx`,
  `Step2Providers.tsx`, `Step3BlockSync.tsx`, `Step4Review.tsx`, the stepper
  host component.

## S1 — backend: optional providers + block sync

**Owned:** `SetupWizardService.cs`, `SetupCompleteRequest.cs` (+ regen contract).
**Task:**
- In `CompleteAsync`, treat `Providers` and `BlockSync` as OPTIONAL:
  - If `BlockSync` is null/empty (no BaseUrl or no BlockSubscriptionId): do
    NOT throw and do NOT require it. Skip the block-sync merge.
  - If `Providers` is null/empty (no realtime/rawtx/rest selection): skip
    `ApplyProviderConfigAsync` entirely — the seeded default doc
    (p2p-primary) remains in force. If `Providers` IS supplied, keep current
    behaviour (apply + validate).
  - Keep admin validation (username/password required when `admin.Enabled`),
    the `SetupBootstrapDocument` write, and `SetP2pEnabledAsync(true)`.
- Keep DTO shape (don't delete fields); the wizard just stops sending them.
  If Swashbuckle's `required` set for the sub-objects changes, regenerate
  `swagger.json` + `api.generated.ts` (`pnpm --dir src/admin-ui contracts:generate`).
**Not:** don't change provider routing defaults; don't remove Settings-side
provider/block-sync config; don't weaken admin validation.
**Validation:** `dotnet build -c Release`; extend `SetupWizardServiceTests`:
admin-only request (no Providers, no BlockSync) → `SetupCompleted` true, P2P
enabled (`SetP2pEnabledAsync(true)` called), NO exception; a request WITH
providers still applies them; missing-admin-creds still rejected when enabled.

## S2 — frontend: collapse to one step

**Owned:** `src/admin-ui/src/screens/setup-wizard/` (stepper + steps + store).
Does NOT touch providers/configuration Settings screens.
**Task:**
- Reduce the first-run stepper to a single step: **Admin account** (Step1),
  then Complete. Remove Providers + Block-sync from the first-run stepper
  (delete their steps from the stepper sequence; the components may remain in
  the tree if Settings imports them, but they're out of the wizard flow).
- `setup-wizard.store.ts` `buildRequest`: send `admin` only (omit
  `providers`/`blockSync`, or send empty objects the backend treats as
  absent). Drop the now-unused validation for providers/block-sync from the
  wizard's step gating.
- Simplify copy: the wizard explains "create your admin account — that's it;
  the node runs on the built-in P2P thin node, no subscriptions needed.
  Providers + history sync are optional under Settings."
**Not:** reintroduce hand-mirrored DTOs (types stay generated re-exports);
don't delete the Settings provider/block-sync screens.
**Validation:** `dotnet build -c Release`; `pnpm verify`;
`RAVEN_URL=http://127.0.0.1:18080 pnpm --dir src/admin-ui test:contract`;
`secrets-lint`. Update any wizard store/contract tests that assumed the
multi-step flow.

## Dependency order
S1 → S2 (S2 relies on the backend accepting an admin-only complete).

## Closeout
Ledger done; hashes in master Delivery Notes (separate backfill commit);
`audits/A1.md` + `evidence/closeout.md`; per-slice audit prompts.
