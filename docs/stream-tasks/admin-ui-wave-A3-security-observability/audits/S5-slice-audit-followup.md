---
created: 2026-05-20
type: slice-audit-followup
parent: docs/stream-tasks/admin-ui-wave-A3-security-observability/audits/S5-slice-audit-prompt.md
status: applied
---

# wave-A3 S5 slice-audit followup (MAJOR REVISION REQUIRED)

Codex slice-audit on `a087036`: MAJOR REVISION REQUIRED
(0C / 0H / 1M / 1L). Both findings folded.

## M1 — `File.Exists` short-circuit left plaintext secrets in Raven on partial failure

**Verified:** the previous `MigrateFromRavenAsync` checked
`File.Exists(_filePath)` first and returned early when the
file was there. That meant: a startup that successfully
wrote `providers.json` and then failed the
`session.SaveChangesAsync` (Raven hiccup, network blip,
permissions issue on Raven side) would land in a state
where the file is authoritative AND the Raven document with
plaintext API keys still exists — and the next startup
would NEVER retry the delete because the file-exists guard
fires first.

The whole point of S5 is "no plaintext provider secrets in
RavenDB." A path that silently keeps them there indefinitely
defeats the slice.

**Revision applied:**

- Migration now **keys on Raven, not on the file**. The
  Raven document is loaded first; if none exists, the call
  is a no-op. If the document exists:
  - When the file does NOT exist yet, write it from the
    Raven payload (no window where neither location holds
    the secrets).
  - When the file DOES exist, treat its content as
    authoritative (operator may have edited it via the
    admin UI after a prior partial migration).
- Either way, the Raven document is deleted via
  `session.Delete` + `SaveChangesAsync`. Fail-stop remains
  in force — any error propagates out of
  `Startup.InitializeDatabase`.
- Unit test
  `MigrateFromRavenAsync_is_idempotent_when_file_already_exists`
  retired (it pinned the old, unsafe contract). Replaced
  with two new cases:
  - `MigrateFromRavenAsync_keeps_file_authoritative_AND_deletes_stale_Raven`
    — the file payload survives; the Raven copy is gone.
  - `MigrateFromRavenAsync_with_no_Raven_doc_and_existing_file_is_a_clean_noop`
    — when nothing is left in Raven the file mtime must
    not move (proves we don't rewrite on every startup).

## L1 — CI's backend job set only .NET 9 SDK; RavenTestDriver tests silently skipped

**Verified:** `actions/setup-dotnet` was pinned to
`9.0.x` only. `RavenDB.TestDriver`'s embedded server runs
against the .NET 8 runtime regardless of which SDK
compiled the test assemblies. Without 8.x on `PATH`, every
`SkippableFact` gated on `DotNetRuntimeFacts.HasRuntimeMajor(8)`
silently no-ops in CI — which means the new
`SecretsFileStoreMigrationTests` were not actually
executing remotely.

**Revision applied:**

- The backend job's setup-dotnet step now installs both
  `8.0.x` and `9.0.x`:

  ```yaml
  - uses: actions/setup-dotnet@v4
    with:
      dotnet-version: |
        8.0.x
        9.0.x
  ```

- Comment in the workflow explicitly calls out why both
  runtimes are needed (RavenTestDriver embedded server
  requires .NET 8) so a future workflow refactor doesn't
  drop one of them.

## Validation

- `dotnet build -c Release` clean (warnings unchanged).
- `dotnet test --filter Secrets`: 6 passed locally; 4
  skipped (no .NET 8 runtime on this dev machine — the
  same SkippableFact gate, exercised in CI now that both
  runtimes are present).
- `pnpm test:contract`: 23/23 still green.
- `bash scripts/secrets-lint.sh`: exits 0.

## Residual risk

The manual Docker smoke from the prompt
(`docker compose --profile dev up -d --build` →
`ls -la /var/lib/consigliere/secrets/`) is documented as
the operator-side verification step in the runbook but is
NOT exercised by the local audit run. The chmod 600
behaviour is pinned by `SecretsFileStore_writes_file_with_owner_read_write_only_on_posix`
in the unit suite, which is the same code path the
container hits — so the smoke is a sanity check on the
mount, not on the chmod.

## Result

2 of 2 findings folded. The migration now safely retries
the Raven cleanup on every startup until the document is
actually gone, and CI ensures the migration tests run
instead of silently skipping.
