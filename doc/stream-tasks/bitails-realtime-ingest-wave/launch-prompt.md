Use [$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md).

Parent task:
- execute `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bitails-realtime-ingest-wave/master.md`

Rules:
- treat this as a multi-zone wave led by `indexer-ingest-orchestration`
- keep the runtime path bounded to live ingest only
- do not spread into historical backfill, public API, or admin work
- reuse the existing Bitails realtime transport seam; do not rebuild topic planning in runtime code
- keep JungleBus available unless the wave explicitly proves it can be demoted

Execution order:
1. `BR1` indexer-ingest-orchestration
2. `BR2` external-chain-adapters
3. `BR3` service-bootstrap-and-ops
4. `BR4` verification-and-conformance
5. `A1` repo-governance closeout

Validation:
- focused runtime and adapter tests
- startup/build verification if DI or hosted-task wiring changes
- explicit proof that Bitails realtime events reach the existing tx/filter pipeline
