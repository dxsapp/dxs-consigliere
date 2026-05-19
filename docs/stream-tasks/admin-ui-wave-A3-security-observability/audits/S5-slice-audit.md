MAJOR REVISION REQUIRED

# wave-A3 S5 slice audit

Audit range checked: `761d7b0..a087036` on `codex/consigliere-vnext`.
HEAD during audit: `9fb1de6` (Delivery Notes hash backfill after the S5 commit).

## Findings

### M1 — Failed Raven delete is not recoverable on the next startup

file: `src/Dxs.Consigliere/Data/Runtime/SecretsFileStore.cs:147`

`MigrateFromRavenAsync` short-circuits immediately when
`providers.json` already exists. In the normal migration path, the
method writes the file first (`SecretsFileStore.cs:164`) and then
deletes the Raven document (`SecretsFileStore.cs:166-167`). If
`SaveChangesAsync` throws after the file write succeeds, startup
correctly fails once, but the next startup sees the file and returns
before looking at Raven. The host can then run with provider secrets
still present in RavenDB indefinitely, which violates the S5 cutover
contract: Raven plaintext provider config is supposed to be migrated
once and deleted.

Recommended fix: add a recovery path for the "file exists + Raven doc
still exists" state. Load both records; if the Raven document matches
the already-migrated file content (or a stored migration hash/marker),
delete the Raven document and save changes. Preserve a non-matching
"fresh" Raven doc to keep the current idempotency guarantee. Add a
test that seeds both file + matching Raven doc and asserts the Raven
doc is deleted without overwriting the file.

### L1 — CI does not deterministically run the S5 Raven migration tests

file: `.github/workflows/ci-tests.yml:37`

The backend job installs only `9.0.x`, while
`SecretsFileStoreMigrationTests` skip unless a .NET 8 runtime is
available (`tests/Dxs.Consigliere.Tests/Secrets/SecretsFileStoreMigrationTests.cs:51`).
That means the three tests that cover file-write + Raven-delete
migration can silently skip depending on the runner image. In this
audit environment they skipped 3/3.

Recommended fix: install both `8.0.x` and `9.0.x` in the backend CI job
or update the embedded Raven test dependency so these tests run on the
net9 runtime. For this slice, make the migration-test command fail CI
when the migration tests are skipped unexpectedly.

## Positive Checks

- `RealtimeSourcePolicyOverrideStore.cs` and its old Raven integration
  test are deleted from tracked files.
- DI registers `SecretsFileStore` as the singleton implementation behind
  `IRealtimeSourcePolicyOverrideStore`.
- `Startup.InitializeDatabase` runs Raven migrations before invoking
  `SecretsFileStore.MigrateFromRavenAsync`, and exceptions propagate out
  of startup.
- `WriteFileAsync` writes via temp file + `File.Move(..., overwrite:
  true)` and calls `File.SetUnixFileMode(UserRead | UserWrite)` on the
  final `_filePath` after the move.
- `scripts/secrets-lint.sh` is present and wired as a top-level CI job.
- `appsettings.Production.json`, DockerComposeE2E/Test settings, compose
  volume binding, and `.gitignore` entries for local secrets paths are in
  place.

## Validation Evidence

- `bash scripts/secrets-lint.sh`: passed.
- `dotnet build Dxs.Consigliere.sln -c Release`: passed, 0 errors
  (existing warnings remain).
- `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj
  -c Release --no-build --filter "FullyQualifiedName~Secrets.SecretsFileStoreTests"`:
  passed 6/6.
- `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj
  -c Release --no-build --filter "FullyQualifiedName~Secrets.SecretsFileStoreMigrationTests"`:
  0 failed, 3 skipped because .NET 8 runtime is not installed locally.
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract`: passed 23/23.
- `pnpm verify`: passed (typecheck, lint, 182 unit tests, build, budget,
  inventory, contracts check).

## Residual Risk

The full manual Docker smoke from the prompt (`docker compose --profile
dev up -d --build` followed by setup wizard completion and in-container
`ls -la /var/lib/consigliere/secrets/`) was not run during this audit.
The blocking finding above is in the migration recovery path and should
be folded before treating S5 as production-ready.
