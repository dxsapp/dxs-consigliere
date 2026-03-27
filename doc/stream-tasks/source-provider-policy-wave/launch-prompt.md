Use [$execution-operator](/Users/imighty/.codex/skills/execution-operator/SKILL.md).

Parent task:
- execute `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/source-provider-policy-wave/master.md`

Rules:
- treat this as a multi-zone task with docs first, then config contract, then adapters
- do not collapse provider policy, config binding, and adapter implementation into one uncontrolled patch
- keep `bitails` as one provider with multiple transport modes, not multiple fake providers
- keep `junglebus` available but non-default
- keep `whatsonchain` REST-only in the product model

Execution order:
1. `SP4` service-bootstrap-and-ops
2. `SP5` external-chain-adapters

Validation:
- config binding and validation checks
- startup diagnostics review
- adapter proof for whichever Bitails realtime transport is implemented first
