# History Sync Wave Closeout

## Shipped

- tracked entity registration now supports nested `historyPolicy.mode`
- tracked docs/status docs persist:
  - `historyMode`
  - `historyReadiness`
  - `historyCoverage`
  - public backfill status fields
- dedicated internal backfill job documents exist for:
  - address full-history
  - token full-history
- `forward_only` boundary initialization now records anchor coverage and promotes history to `forward_live`
- explicit full-history upgrade endpoints landed for single and bulk address/token operations
- history query paths now gate on `GetBlockingHistoryReadinessAsync(...)`
- partial history is only served when `acceptPartialHistory = true`
- historical address scans route through the canonical journal ingress by publishing normal `TxMessage.FoundInBlock` messages
- historical capabilities are first-class in source config/routing:
  - `historical_address_scan`
  - `historical_token_scan`

## Verification

- app build passed
- focused readiness/history/controller test wave passed
- config binding tests passed
- benchmark assembly build passed

## Current Limitation

- address full-history has a real execution path through Bitails-backed paging.
- token full-history is not silently stubbed; it is explicitly unsupported in runtime execution right now and degrades the history dimension honestly.

## Recommended Next Slice

- implement a real `historical_token_scan` source path before treating token `full_history` as production-complete.
