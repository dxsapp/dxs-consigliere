# Slices

## `SP1` Product Policy
- add a compact product-facing provider policy doc
- state baseline, advanced, and fallback source roles clearly
- explain why selective managed realtime ingest drives the decision

## `SP2` Capability Matrix Alignment
- update the canonical source-capability matrix
- mark `bitails` as the baseline provider-first realtime source
- mark `junglebus` as advanced/manual
- mark `whatsonchain` as REST-only in product policy

## `SP3` Config Example Alignment
- update config examples so they no longer imply `junglebus`-first by default
- show `bitails` as the primary provider-first realtime source
- add a future-looking Bitails transport modeling note for `websocket | zmq`

## `SP4` Runtime Config Contract
- extend `ConsigliereSourcesConfig` for an explicit Bitails transport selector
- update config validation and startup diagnostics
- keep the provider identity stable while adding transport modes

## `SP5` Adapter Follow-Up
- implement Bitails realtime transport selection behind one provider contract
- keep `junglebus` as an optional advanced adapter path
- do not let `whatsonchain` drift into fake realtime support
