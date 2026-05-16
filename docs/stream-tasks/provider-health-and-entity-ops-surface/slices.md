# Slices

## `PH1` Ops Contract Freeze
- freeze the exact JungleBus health fields and entity detail stats
- decide what is explicit `unknown/unavailable` vs zero
- keep the scope operator-facing, not analytics-facing

## `PH2` Entity Summary Read Models
- enrich tracked address detail with balance/UTXO/tx counters and first/last activity
- enrich tracked token detail with balance/UTXO/tx counters and rooted-history summary
- keep queries cheap and bounded

## `PH3` JungleBus Health Read Model
- expose observed JungleBus block tip vs local indexed tip
- compute lag blocks and last healthy timestamps
- capture last error if present without scraping generic logs in the UI

## `PH4` API Contract
- extend/admin endpoints for the new address/token details
- add runtime/ops endpoint for JungleBus block-sync health
- document payload shapes and nullability honestly

## `PH5` Admin UI Surface
- runtime page: JungleBus health card/panel
- address detail page: operational stats card(s)
- token detail page: operational stats card(s)
- show explicit unavailable states and timestamps

## `PH6` Focused Proof
- query/service tests for entity summaries
- runtime tests for JungleBus health/lag
- controller tests for new payloads
- frontend build and page-level verification

## `A1` Closeout Audit
- verify that normal operator questions can be answered from UI/API
- record any residual gaps honestly
