# wizard-p2p S1 — slice-audit prompt

Audit target: S1 (runtime-settings doc + seed + wizard write).
Diff: `3e78b2c..34ecce0` on `codex/consigliere-vnext`.

Read `master.md` (Product Decisions 2 + 4, Core Rules 1, 4, 5), `slices.md` §S1.

## What landed
- `OperatorRuntimeSettingsDocument` (RavenDB `operator/runtime/operator-settings`,
  `AuditableEntity`): nullable `P2pEnabled` (null = not overridden) + `UpdatedBy`.
- `IOperatorRuntimeSettingsStore` (thin Raven Get/Save) +
  `OperatorRuntimeSettingsService`:
  `GetP2pEnabledAsync = doc?.P2pEnabled ?? BsvP2pConfig.Enabled`;
  `SetP2pEnabledAsync(bool, updatedBy)`.
- `SetupWizardService.CompleteAsync` calls `SetP2pEnabledAsync(true, …)`
  after the bootstrap save.
- DI in `IndexerStateSetup`.

## Focus
1. **Seed-then-DB semantics (Core Rule 1).** Confirm a missing doc falls back
   to the config seed (false by default) and a present doc wins both
   directions (true-over-false AND false-over-true). A null field falls back
   to the seed. (Covered by 7 unit tests — verify they assert all four.)
2. **CI/E2E safe (Core Rule 4).** No wizard → no doc → seed false → P2P off.
   Confirm nothing seeds the doc eagerly at startup (the service computes the
   fallback; it does not persist a seeded doc on read).
3. **No secret in the doc (Core Rule 5).** `P2pEnabled` + `UpdatedBy` only;
   `secrets-lint` green.
4. **Wizard write is unconditional-on-complete.** Completing the wizard sets
   `P2pEnabled=true` regardless of which realtime/rawtx primary was chosen —
   intended (the thin node is the product default). Confirm it fires after a
   successful provider-config + bootstrap save, and the updatedBy is the admin
   username (or "setup").
5. **AuditableEntity overrides** (AllKeys/UpdateableKeys/ToEntries) include the
   new fields (audit-trail completeness).

## Validation evidence
- `dotnet build … -c Release` clean.
- `OperatorRuntimeSettingsServiceTests` 7/7; `SetupWizardServiceTests` 5/5
  (incl. `CompleteAsync_EnablesP2pThinNode` verifying `SetP2pEnabledAsync(true)`).

## Verdict + finding format
Verdict first (`APPROVE`/`APPROVE WITH CHANGES`/`MAJOR REVISION`); findings
`C*|H*|M*|L*` with file:line, why, fix.
