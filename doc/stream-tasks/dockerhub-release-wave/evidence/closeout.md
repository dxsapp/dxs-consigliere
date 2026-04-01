# Closeout

## Closed Slices

- `DR1` release policy
- `DR2` DockerHub workflow
- `DR3` README update
- `DR4` verification and checklist

## Delivered Surface

- `/Users/imighty/Code/dxs-consigliere/.github/workflows/docker-release.yml`
- `/Users/imighty/Code/dxs-consigliere/README.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dockerhub-release-wave/master.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/dockerhub-release-wave/audits/A1.md`

## Validation

- `ruby -e 'require "yaml"; YAML.load_file("/Users/imighty/Code/dxs-consigliere/.github/workflows/docker-release.yml"); puts "workflow yaml ok"'`
- `git diff --check`

## Manual Release Checklist

1. Ensure `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` are set in GitHub repository secrets.
2. Ensure `main` is in the desired release state.
3. Create and push a stable tag: `vX.Y.Z`.
4. Confirm GitHub Actions `docker-release` workflow runs on that tag.
5. Confirm DockerHub receives:
   - `dxs/consigliere:X.Y.Z`
   - `dxs/consigliere:X.Y`
   - `dxs/consigliere:X`
   - `dxs/consigliere:latest`
6. Pull the published tag locally and smoke-check container startup.

## Rollback Note

If a bad image is published, cut a new stable `vX.Y.Z+1` replacement release from corrected code. Do not mutate or silently repoint an existing immutable version tag.
