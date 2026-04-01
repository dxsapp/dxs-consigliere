# Slices

## `JA1` Assurance Contract Freeze
- freeze the assurance state enum and operator meanings
- define which timestamps and heights drive confidence vs lag
- keep the output honest about single-source assurance

## `JA2` Assurance Read Model
- persist/read current assurance snapshot for JungleBus-first block sync
- include observed tip movement, local tip movement, lag, and stale markers
- distinguish unavailable from degraded

## `JA3` Runtime Integration
- update JungleBus runtime tasks to refresh assurance fields on control and processing events
- compute stalled-control-flow vs stalled-local-progress without requiring node RPC
- keep provider/runtime wiring bounded to diagnostics

## `JA4` API Contract
- add or extend ops endpoint for assurance payload
- document nullability and degraded-state semantics clearly

## `JA5` Admin UI Surface
- add runtime assurance card/panel distinct from raw lag card
- show confidence state, single-source warning, and last movement timestamps
- make unavailable and stale states visually explicit

## `JA6` Focused Proof
- runtime tests for stale/control/local-progress scenarios
- controller tests for assurance payload
- frontend build and runtime page proof

## `A1` Closeout Audit
- verify operators can tell healthy vs catching-up vs stale vs single-source from UI/API
- record residual gaps honestly
