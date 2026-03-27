# Slices

## `SC1` Fixture Expansion
- add `swap_valid`
- add `confiscate_multisig_valid`
- add `confiscate_multisig_insufficient_signatures_rejected`
- update conformance expectation map
- refresh oracle hash manifest

## `SC2` Lifecycle Integration
- add explicit native proof call for `swap_valid` in the swap lifecycle suite
- replace generic confiscation proof in multisig authority suite with multisig-specific positive/negative proofs
- refresh representative vector assertions/counts where needed

## `A1` Audit
- summarize new proof coverage
- decide whether oracle can be demoted from primary truth anchor to regression/backstop anchor
