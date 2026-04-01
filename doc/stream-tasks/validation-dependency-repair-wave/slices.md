# Validation Dependency Repair Wave Slices

## VR1 Contract Freeze

### Goal
Freeze the canonical contract for validation dependency acquisition and repair.

### Must define
- `ValidationDependencyRequest`
- `ValidationDependencyPlan`
- `ValidationDependencyResult`
- `ValidationDependencyReason`
- repair work item lifecycle
- scenario coverage matrix

### Required terminology
- `raw_tx_fetch`
- `validation_fetch`
- `validation_repair`
- `unresolved lineage`
- `targeted revalidation`

### Done when
- docs clearly distinguish dependency acquisition from authoritative validation
- scenario matrix is frozen for implementation

## VR2 Durable Repair Work

### Goal
Persist validation-repair work items so unresolved lineage state survives restarts and can be retried sanely.

### Suggested shape
- `ValidationRepairWorkItemDocument`

### Suggested fields
- `EntityType`
- `EntityId`
- `Reason`
- `MissingDependencies[]`
- `State`
- `AttemptCount`
- `LastAttemptAt`
- `LastError`
- `NextAttemptAt`
- `CreatedAt`
- `UpdatedAt`

### Rules
- dedupe repeated unresolved requests for the same target
- do not create infinite duplicate work
- preserve enough state for operator visibility

### Done when
- repair work is durable and deduplicated

## VR3 Repair Worker

### Goal
Run durable validation repair work through a bounded acquisition worker.

### Required behavior
- picks pending work items
- fetches dependency data through validation dependency service
- retries with bounded backoff
- stops infinite churn on permanent failures
- updates work state honestly

### Possible runtime pieces
- `IValidationRepairScheduler`
- `IValidationRepairWorker`
- `IValidationDependencyService`

### Done when
- repair execution is deterministic and observable

## VR4 Targeted Revalidation

### Goal
Re-run only the affected lineage/token projections after new dependency data lands.

### Required behavior
- acquired dependency causes affected tx to be re-evaluated
- token/rooted-history state reflects the new lineage facts
- status can move from `unknown` to `valid` or `invalid`

### Done when
- dependency acquisition is connected to real validation state changes

## VR5 API Integration

### Goal
Integrate the repair subsystem into public validation flow without changing validation authority.

### Required behavior
- validate endpoint still returns local Consigliere verdict
- unresolved lineage can schedule or reference repair work
- no API wording implies provider-side validation authority

### Done when
- public validation and internal repair model are consistent

## VR6 Ops Surface

### Goal
Give operators clear visibility into validation repair health.

### Suggested visibility
- queue depth
- unresolved count
- running count
- failed count
- oldest unresolved age
- recent repair failures
- stuck items by reason

### Possible endpoints
- `GET /api/ops/validation/repairs`
- `GET /api/ops/validation/unresolved`

### Done when
- operators can inspect unresolved/stuck/failed validation repair state without DB spelunking

## A1 Closeout Audit

### Goal
Close the wave only when `validation_fetch` is materially real rather than merely described.

### Must verify
- contract exists
- repair work is durable
- targeted revalidation works
- operator visibility exists
- residuals are honest

### Done when
- the residual from legacy convergence about `validation_fetch` is substantially retired
