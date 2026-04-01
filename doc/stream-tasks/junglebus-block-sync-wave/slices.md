# Slices

## `JB1` Block-Sync Contract Freeze
- define operator-visible `block_sync` capability
- document default = `JungleBus / GorillaPool`
- document node as advanced optional path

## `JB2` Runtime Block-Processing Fix
- remove unconditional node RPC dependency from block sync first step
- make block-processing path work when node RPC is absent or placeholder-only
- keep orphan/reorg handling honest if node-only behavior remains required

## `JB3` JungleBus Block-Sync Integration
- make JungleBus-backed block path consume the config it actually needs
- validate required JungleBus block-sync fields explicitly
- avoid hidden fallback to broken legacy placeholders

## `JB4` Setup/API Contract Update
- add block-sync setup inputs to setup API contract
- validate incomplete JungleBus block-sync config with explicit codes
- keep provider/setup contract bounded

## `JB5` Wizard UI Update
- add explicit JungleBus block-sync step or substep
- explain why block sync matters for drift prevention
- keep wizard capability-first and narrow

## `JB6` Runtime Diagnostics And Messaging
- stop implying node is required for default startup posture
- update diagnostics/help text to match JungleBus-first block sync

## `JB7` Focused Verification
- block process path with invalid node RPC no longer fails by URI parse
- setup rejects missing JungleBus block-sync requirements
- frontend build passes
- focused runtime and controller tests pass

## `A1` Closeout Audit
- verify block-sync posture matches product guidance
- record residual node-only advanced behavior honestly
