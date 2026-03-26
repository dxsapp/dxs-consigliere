# Rooted Follow-up Wave Closeout

- Date: 2026-03-26
- Status: closed

## Delivered

### F1 Address-history regression

- Kept the production envelope contract intact in `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Addresses/AddressHistoryEnvelopeHelper.cs`.
- Corrected the focused test fixture in `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Services/Impl/AddressHistoryServiceProjectionTests.cs` so the optimized path is exercised with a valid envelope.

### F2 Mixed rooted DSTAS verification

- Added rooted DSTAS full-system coverage in `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/FullSystem/VNextDstasFullSystemValidationTests.cs` for:
  - trusted rooted lifecycle remaining canonical across state, history, balances, and UTXOs
  - unknown-root DSTAS lifecycle being excluded from outward reads while downgrading validation to `Unknown`
- Hardened rooted protocol tagging in `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Data/Tokens/TokenProjectionReader.cs` so DSTAS issue roots continue to surface as `dstas`.

## Validation

- Focused history regression pack: green
- Focused rooted DSTAS follow-up pack: green
- Broader rooted/history/readiness/regression pack: green
- Benchmarks assembly build: green

## Outcome

- The address-history follow-up is closed without relaxing production invariants.
- Rooted token verification now covers adversarial mixed trusted/unknown-root DSTAS flows at the full-system level.
