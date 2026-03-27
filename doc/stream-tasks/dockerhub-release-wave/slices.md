# Slices

## `DR1` Release Policy
- define release trigger
- define Docker tag mapping
- define prerelease ignore policy for v1
- define required secrets

## `DR2` DockerHub Workflow
- add `.github/workflows/docker-release.yml`
- trigger on `v*` tags
- login to DockerHub
- build image from repo `Dockerfile`
- push version tags and `latest`
- add OCI metadata labels if cheap

## `DR3` README Update
- document DockerHub release flow
- document tag semantics
- document maintainer steps for a release

## `DR4` Verification
- dry-run checklist
- manual tagged release checklist
- rollback note if a bad image is published
