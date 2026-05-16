# Operator Task Intake

Use this template when a new task enters the repository.

## Intake Header

- Parent task:
- Requested outcome:
- Definition of done:
- Constraints:
- Validation required:
- Delivery artifact expected:

## Zone Split

| Zone | Scope in this task | Status | Depends on | Execute locally or delegate | Preferred lane |
|---|---|---|---|---|---|
| `example-zone` | | `pending` | | `local` or `delegate` | `operator/example` |

## Clarifying Questions

Ask only if the answer changes execution:

- Question:
- Why it matters:
- Default assumption if unanswered:

## Child Tasks

For each active zone:

- Child task id:
- Zone:
- Owned paths:
- Required inputs:
- Acceptance criteria:
- Validation:
- Handoff needed:

## Execution Notes

- Critical path:
- Parallelizable side work:
- Risk hotspots:
- Rollback or recovery note:

## Closeout

- Completed zones:
- Validation actually performed:
- Remaining risks:
- Follow-up tasks:

## Rules

- Determine ownership by changed paths, not by feature label.
- Create one parent task and child tasks per zone when the task crosses boundaries.
- Keep the critical path local unless a delegated result is clearly sidecar work.
- If a zone blocks another, attach `handoff-contract.md`.
