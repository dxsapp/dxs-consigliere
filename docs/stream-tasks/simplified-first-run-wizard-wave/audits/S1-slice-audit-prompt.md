# simplified-wizard S1 — slice-audit prompt

Target: S1 (CompleteAsync optional providers/block-sync). Diff: `49e885a..1843bf4`.
Read `master.md` + `slices.md` §S1.

## What landed
`SetupWizardService.CompleteAsync`: Providers + BlockSync now OPTIONAL.
- block sync only validated if started (both BaseUrl + SubId required together);
- `ApplyProviderConfigAsync` called ONLY when provider selection or block sync
  is supplied; admin-only request skips it → seeded p2p-primary defaults remain;
- admin validation + bootstrap + `SetP2pEnabledAsync(true)` unchanged.
No DTO/contract change (regen produced no diff).

## Focus
1. **No mandatory third-party.** Admin-only request completes, no JungleBus
   required, P2P enabled. (Test: `CompleteAsync_AdminOnly_Succeeds_WithoutProvidersOrBlockSync`.)
2. **Legacy path intact.** A full request (providers + block sync) still
   validates + applies; partial block sync (one field) still rejected.
3. **No silent misconfig.** Admin-only path leaves the seeded default override
   doc (p2p primary) — confirm it does NOT wipe/blank provider config.
4. **`hasProviderSelection` heuristic** — does an empty-but-present Providers
   object (the frontend sends empty strings) correctly read as "absent"?
   Verify empty-string selection doesn't trip ApplyProviderConfigAsync with
   invalid values.

## Validation
`dotnet build -c Release` clean; SetupWizardServiceTests 5/5 + SetupControllerTests.

Verdict + `C*|H*|M*|L*` findings.
