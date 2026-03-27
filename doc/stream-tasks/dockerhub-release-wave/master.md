# DockerHub Release Wave

## Goal

Publish versioned Docker images to DockerHub automatically when a release tag is pushed.

## Release Model

Use a tag-driven release flow.

Canonical trigger:
- push git tag `vX.Y.Z`

Docker tags to publish for `vX.Y.Z`:
- `X.Y.Z`
- `X.Y`
- `X`
- `latest`

DockerHub repository:
- `dxs/consigliere`

## Current State

- Docker build source exists:
  - `/Users/imighty/Code/dxs-consigliere/Dockerfile`
- CI workflow exists:
  - `/Users/imighty/Code/dxs-consigliere/.github/workflows/ci-tests.yml`
- There is no dedicated DockerHub release workflow yet.

## Required Secrets

- `DOCKERHUB_USERNAME`
- `DOCKERHUB_TOKEN`

## Delivery Decisions

- canonical image repository is `dxs/consigliere`
- `latest` is updated on every stable `vX.Y.Z` tag
- prerelease tags are out of scope for v1 and should be ignored by the release workflow

## Status Ledger

| slice | zone | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `DR1` | `repo-governance` | `operator/governance` | `todo` | - | docs review | release policy is documented clearly |
| `DR2` | `service-bootstrap-and-ops` | `operator/platform` | `todo` | `DR1` | workflow lint + dry review | GitHub Actions workflow publishes versioned DockerHub tags from `vX.Y.Z` |
| `DR3` | `repo-governance` | `operator/governance` | `todo` | `DR2` | README review | README documents the release flow and image tag semantics |
| `DR4` | `verification-and-conformance` | `operator/verification` | `todo` | `DR2`,`DR3` | dry run / release checklist | release process is operationally verified |

## Definition of Done

- pushing `vX.Y.Z` publishes versioned DockerHub image tags
- `latest` behavior is documented and intentional
- README explains how maintainers cut releases
- no ambiguity remains around release source of truth
