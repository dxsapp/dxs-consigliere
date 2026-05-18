---
created: 2026-05-18
type: audit-followup
parent: admin-ui-vnext-S0-slice-audit
status: applied
---

# Admin UI vNext — S0 slice-audit followup (APPROVE WITH CHANGES)

Codex S0 slice-audit verdict on commit `379b7c4`: APPROVE WITH
CHANGES (0 C / 1 H / 0 M / 2 L). All three findings closed in
this commit.

## H1 — Root `Dockerfile` not tracked

**Verified:** the workspace had `DockerFile` (capital `F`)
tracked in git, while the release workflow at
`.github/workflows/docker-release.yml:76` references
`file: ./Dockerfile` (lowercase). On macOS' case-insensitive
filesystem both resolve to the same blob, but Linux CI (and a
clean GitHub checkout) would fail to find `./Dockerfile`.

**Revision applied:** Renamed the tracked file
`DockerFile → Dockerfile` via a two-step `git mv` to overcome
the case-insensitive filesystem (`git mv DockerFile dockerfile-temp
&& git mv dockerfile-temp Dockerfile`). The rename is now a
proper git rename in the commit log. Docker-release workflow
path is unchanged and will resolve correctly on Linux.

## L1 — README stale metadata

**Verified:** `src/admin-ui/README.md` had three stale items:
- "pnpm 9" while `package.json` pins `pnpm@10.15.0`.
- CI branch list mentioned only `main` / `codex-*`; the actual
  workflow also runs on `codex/*` (the current dev branch) +
  pull requests.
- Stack-profile link label used the absolute
  `/Users/imighty/Code/...` path which isn't valid for anyone
  but the local dev.

**Revision applied:**
- "Package manager" row now reads `pnpm 10.15.0 (pinned via
  packageManager)`.
- CI section now lists `main`, `codex-*`, `codex/*`, and "every
  pull request".
- Stack-profile reference rewritten to name the workspace
  baseline as plain text + a working relative link to the
  in-repo mirror under `docs/admin-ui/design-bundle/project/uploads/`.

## L2 — `master.md` integration-test path drift

**Verified:** master.md Core Rule §15 + the
`admin-ui-tests` zone row described integration tests at
`src/admin-ui/src/integration/`, while S0 actually created
the directory at `src/admin-ui/integration/` (top-level, not
under `src/`). Internally consistent (the slice ledger + S0
prompt both used the top-level path), but the plan was stale.

**Revision applied:** master.md Core Rule §15 and the
`admin-ui-tests` Ownership Zone row both changed to
`src/admin-ui/integration/`.

---

## Summary

| Layer | Change |
|---|---|
| Repo root | `DockerFile` renamed to `Dockerfile` (proper git rename) |
| `src/admin-ui/README.md` | pnpm version, CI branch list, stack-profile link |
| `master.md` Core Rule §15 | integration path `src/admin-ui/src/integration/` → `src/admin-ui/integration/` |
| `master.md` Ownership Zones | same path correction in `admin-ui-tests` row |

S0 slice-gate cleared; S1 (theme + tokens from the design
bundle + persistence migration) opens.
