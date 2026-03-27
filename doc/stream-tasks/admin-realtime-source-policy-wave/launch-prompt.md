Use [$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md).

Parent task:
- execute `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/admin-realtime-source-policy-wave/master.md`

Rules:
- keep this wave bounded to minimal realtime source policy controls
- do not broaden into generic config editing
- do not allow secrets or provider URLs to be changed from the admin UI
- persist overrides outside static files
- restrict overrides to:
  - realtime primary source
  - Bitails transport
- make `SourceCapabilityRouting` consume overrides only for `realtime_ingest`

Execution order:
1. `RS1` persisted override layer
2. `RS2` effective routing consumption
3. `RS3` admin read/apply/reset API
4. `RS4` focused verification
5. `RS5` docs and admin UI handoff update

Validation:
- store tests for override persistence
- routing tests for static vs override behavior
- controller tests for read/apply/reset contract
- explicit rejection tests for invalid source/transport values
