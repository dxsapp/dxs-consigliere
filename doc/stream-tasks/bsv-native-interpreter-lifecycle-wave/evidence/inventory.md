# Lifecycle Native Proof Inventory

## Suite Status

### `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/FullSystem/VNextDstasFullSystemValidationTests.cs`
- status: `mixed`
- native-covered proof now attached for:
  - `transfer_regular_valid`
  - `freeze_valid`
  - `unfreeze_valid`
  - `confiscate_valid`
  - `confiscate_without_authority_rejected`
  - `confiscate_without_bit2_rejected`
  - `swap_cancel_valid`
- remaining synthetic-only blind spot:
  - no positive native proof yet for the repo's swap-initiation leg; the suite still proves swap semantics through state/query behavior plus native `swap_cancel` execution truth

### `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/FullSystem/VNextDstasMultisigAuthorityValidationTests.cs`
- status: `mixed`
- native-covered proof now attached for:
  - protocol-owner chain `authority_multisig_freeze_unfreeze_cycle`
  - protocol-owner chain `owner_multisig_positive_spend`
  - generic `confiscate_valid` execution truth
- remaining blind spots:
  - no explicit vendored native proof yet for confiscation under multisig authority policy
  - no vendored native negative proof yet for insufficient multisig authority signatures on the confiscation/unfreeze path

### `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/RootedHistory/RootedDstasFullSystemValidationTests.cs`
- status: `mixed`
- native-covered proof now attached for the trusted branch:
  - `transfer_regular_valid`
  - `freeze_valid`
  - `unfreeze_valid`
- remaining blind spot:
  - unknown-root exclusion remains a rooted state/history proof, not a separate native execution fixture universe

### `/Users/imighty/Code/dxs-consigliere/tests/Dxs.Consigliere.Tests/Dstas/RootedHistory/RootedDstasTokenReadSurfaceTests.cs`
- status: `mixed`
- native-covered proof now attached for the trusted readable branch:
  - `transfer_regular_valid`
  - `freeze_valid`
  - `unfreeze_valid`
- remaining blind spot:
  - rooted read filtering over unknown-root branches remains proven at the read-model layer, not as a new native tx fixture set

## Outcome

The lifecycle-native proof gap is materially narrower:
- broader DSTAS lifecycle suites now carry explicit native replay evidence instead of relying only on synthetic projection state
- protocol-owner and conformance fixture universes are reused as the deterministic execution-truth anchor for those suites
- no protocol-core change was required for this wave

But the proof is not yet complete enough to demote or reopen oracle role blindly. The remaining blind spots are now small and explicit.
