# wave-A3 S5 — slice-audit prompt

Audit target: wave-A3 S5 (Secrets at rest) on
`codex/consigliere-vnext`. Diff range: `761d7b0..<S5 commit>`.

---

You are auditing the **fifth slice in wave-A3**. S5 moves
the provider-config secrets (API keys, websocket / ZMQ URLs,
JungleBus subscription IDs) out of RavenDB and onto a single
chmod-600 JSON file under `Consigliere:Secrets:Dir`. The
wave-A2 Raven document is migrated once on first wave-A3
startup and then deleted — per the launch-prompt's no-back-
compat-shim constraint, there is no parallel path.

Read first:

- `docs/stream-tasks/admin-ui-wave-A3-security-observability/master.md`
  (S5 row marked done; Delivery Notes updated)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/slices.md`
  § S5 (intent · owned paths · exact task · migration ·
  what-not-to-do · validation · completion signal)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/launch-prompt.md`
  (wave constraint: NO back-compat shim — both ends in-repo;
  S5 deletes the Raven doc, doesn't cohabit)

Cross-validate against the deliverable:

- `src/Dxs.Consigliere/Configs/ConsigliereSecretsConfig.cs`
  (new) — bind shape, default `Dir = "data/secrets"`.
- `src/Dxs.Consigliere/Data/Runtime/SecretsFileStore.cs`
  (new) — `IRealtimeSourcePolicyOverrideStore` implementation
  backed by `{Dir}/providers.json`. Atomic write via tmp +
  `File.Move(...overwrite: true)`. `chmod 600` on POSIX
  (`File.SetUnixFileMode(UserRead | UserWrite)`); Docker
  read-only secrets mounts swallowed silently.
  `MigrateFromRavenAsync(IDocumentStore)` is the one-shot
  cutover: reads the Raven doc, writes to file, deletes the
  Raven doc. Idempotent — skips if file already exists.
- `src/Dxs.Consigliere/Data/Runtime/RealtimeSourcePolicyOverrideStore.cs`
  (DELETED) — the Raven implementation is gone. Both ends
  of the consumer chain are in-repo, so no shim survives.
- `src/Dxs.Consigliere/Setup/IndexerStateSetup.cs` —
  registers `SecretsFileStore` + forwards
  `IRealtimeSourcePolicyOverrideStore` to it. Singleton.
- `src/Dxs.Consigliere/Setup/CorePlatformSetup.cs` — binds
  `ConsigliereSecretsConfig` from `Consigliere:Secrets`.
- `src/Dxs.Consigliere/Startup.cs` — `InitializeDatabase`
  runs the Raven migration runner THEN calls
  `SecretsFileStore.MigrateFromRavenAsync` synchronously.
  Fail-stop: any throw aborts process startup.
- `src/Dxs.Consigliere/appsettings.Production.json` (new) —
  env-var placeholders for `RavenDb__Urls__0` +
  `RavenDb__DbName`; `Consigliere:Secrets:Dir` pinned at
  `/var/lib/consigliere/secrets`.
- `src/Dxs.Consigliere/appsettings.json` — the
  `BsvNodeApi.Password` placeholder switched from
  `[Bsv node rpc password]` to `${BSV_NODE_RPC_PASSWORD}` so
  `secrets-lint.sh` stays green at HEAD.
- `src/Dxs.Consigliere/appsettings.Test.json` +
  `appsettings.DockerComposeE2E.json` — pin
  `Consigliere:Secrets:Dir` to test / container paths so
  the file store has a writable scratch location.
- `compose.yml` — new named volume `consigliere-secrets`
  bound at `/var/lib/consigliere/secrets`. The consigliere
  service env sets `Consigliere__Secrets__Dir` to that path.
- `.gitignore` — `data/secrets/` + `data/secrets-test/`
  excluded so dev runs never check in providers.json.
- `scripts/secrets-lint.sh` (new) — greps
  `src/Dxs.Consigliere/appsettings*.json` for `"ApiKey" |
  "Password" | "Secret" | "Token"` keys with values that
  start with anything other than `"`, `${`, or `{`. Exit 1
  on any hit.
- `.github/workflows/ci-tests.yml` — new top-level
  `secrets-lint` job runs the script on every push + PR.
- `tests/Dxs.Consigliere.Tests/Secrets/
  SecretsFileStoreTests.cs` (new) — 6 cases pin the
  round-trip, the chmod-600 owner-only mode on POSIX, the
  `ResetAsync` cleanup, and the `UpsertAsync` preservation
  of unrelated fields.
- `tests/Dxs.Consigliere.Tests/Secrets/
  SecretsFileStoreMigrationTests.cs` (new, RavenTestDriver)
  — 3 SkippableFacts cover the happy-path migration (file
  written + Raven deleted), the idempotency when the file
  is already present (Raven doc untouched), and the
  no-Raven-doc no-op. Skipped locally without .NET 8
  runtime; runs in CI if `setup-dotnet` exposes 8.0.x.
- `tests/Dxs.Consigliere.Tests/Data/Runtime/
  RealtimeSourcePolicyOverrideStoreIntegrationTests.cs`
  (DELETED) — the deleted Raven impl's coverage moves onto
  the file store via the new SecretsFileStoreTests.
- `docs/runbook.md` — new "Secrets at rest" section: file
  layout, compose binding, migration semantics, rotation
  procedure, CI grep-gate documentation.

---

## What's in scope for this audit

1. **No plaintext secrets in git.** `bash scripts/secrets-lint.sh`
   at HEAD must exit 0. The CI job pins this; the audit
   should re-run it as a sanity check.
2. **No back-compat shim.** The Raven impl + its test are
   actually deleted (not commented out, not kept "for
   legacy compatibility"). Both ends of the provider-config
   consumer chain live in-repo.
3. **Migration is fail-stop.** Any IO error during
   `MigrateFromRavenAsync` must propagate out of
   `Startup.InitializeDatabase`. A half-migrated host
   running with the Raven doc deleted but the file missing
   would be a security regression.
4. **chmod 600 on POSIX.** Owner-rw only. Verify the
   `File.SetUnixFileMode` call wins on the final
   `_filePath` after the `File.Move(overwrite: true)`.
5. **Idempotency.** Re-running `MigrateFromRavenAsync` after
   the file already exists must NOT overwrite the file or
   delete a fresh Raven doc. The
   `MigrateFromRavenAsync_is_idempotent_when_file_already_exists`
   unit test pins this.
6. **No regression of the wave-A2 contract suite.**
   `pnpm test:contract` must still run 23/23 green. The
   Test environment now writes to `data/secrets-test/` —
   if that path is unwritable the host fails fast which
   would manifest as a missing host URL in globalSetup.

## Verdict + finding format

Verdict line first:
- `APPROVE`
- `APPROVE WITH CHANGES` — minor (L*) findings only
- `MAJOR REVISION REQUIRED` — at least one C/H/M

Findings tagged `C* | H* | M* | L*` (Critical / High /
Medium / Low). Each finding contains:
- file:line of the defect
- why it matters (security / correctness consequence)
- recommended fix (specific, not "consider re-architecting")

Out of scope for this audit (later slices in the wave):
S3 audit log, S4 log stream, S6 NRT, S7 runbook completion.

## Validation evidence I should produce

- `dotnet build -c Release` clean on Consigliere.
- `dotnet test --filter Secrets.SecretsFileStoreTests`
  6/6 green.
- `dotnet test --filter Secrets.SecretsFileStoreMigrationTests`
  3/3 green (requires .NET 8 runtime; auto-skips otherwise
  via SkippableFact).
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract`
  23/23 green.
- `pnpm verify` green.
- `bash scripts/secrets-lint.sh` exits 0 against the
  current appsettings files.
- (Manual) `docker compose --profile dev up -d --build` →
  shell into the consigliere container →
  `ls -la /var/lib/consigliere/secrets/`. After completing
  the setup wizard via the dev URL, the providers.json
  file should exist with mode `-rw-------`.
