# wave-A3 S3 — slice-audit prompt

Audit target: wave-A3 S3 (Audit log for destructive operations)
on `codex/consigliere-vnext`. Diff range:
`<S5-fold commit>..<S3 commit>`.

---

You are auditing **the audit log slice**. S3 introduces a
RavenDB-backed forensic record of every destructive admin
operation, fail-stop on write failure (a broadcast must not
go out without a forensic record).

Read first:

- `docs/stream-tasks/admin-ui-wave-A3-security-observability/master.md`
  (S3 row marked done; Delivery Notes updated)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/slices.md`
  § S3 (intent · owned paths · exact task · what-not-to-do
  · validation · completion signal)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/launch-prompt.md`
  (wave constraints: audit log writes are fail-stop)

Cross-validate against the deliverable:

- `src/Dxs.Consigliere/Data/Models/Audit/AuditLogEntry.cs`
  (new) — immutable entity with id prefix
  `audit-log/<unixMs>/<guid8>`. `UpdateableKeys()` returns
  `EmptyKeys` so no admin surface can mutate. Inherits
  `Entity`, NOT `AuditableEntity` (audit records ARE the
  audit trail — no nested CreatedAt/UpdatedAt).
- `src/Dxs.Consigliere/Services/Audit/IAuditLogger.cs` (new)
  — single `RecordAsync(action, targetId, context,
  username?, ct)` returning `Task<bool>`. `AuditActionNames`
  exposes the stable `broadcast_tx` constant.
- `src/Dxs.Consigliere/Services/Audit/AuditLogger.cs` (new)
  — opens a short-lived async session, sets
  `@expires = UtcNow + 365d` metadata, returns `true` on
  clean save and `false` (with a logger.LogError) on any
  exception. Resolves `Username` from
  `HttpContextAccessor.User.FindFirstValue(ClaimTypes.Name)`
  when the caller doesn't pass one explicitly. Enables the
  Raven expiration bundle on first write via
  `ConfigureExpirationOperation`; re-arms if it fails so a
  subsequent call retries.
- `src/Dxs.Consigliere/Services/Impl/BroadcastService.cs`
  — ctor now takes `IAuditLogger`; in `BroadcastAsync`
  the audit write fires AFTER policy-validation (so we
  have a real txid) and BEFORE the `OutgoingStore.SaveAsync`
  + announcer dispatch. On `auditOk == false` the method
  short-circuits with `BroadcastReceipt(null, Failed,
  ..., "audit_write_failed")` — no persist, no announce.
  Context payload is `{ rawHexLength, source }` only; the
  slice's what-not-to-do call-out (no raw hex / no hash)
  is honoured.
- `src/Dxs.Consigliere/Controllers/AdminAuditController.cs`
  (new) — `[Authorize(Policy = AdminAuthDefaults.Policy)]`
  `GET /api/admin/audit-log?since=&action=&username=&lastN=`.
  `ClampLastN` mirrors the wave-A1 alerts pattern with a
  1000-row hard ceiling; `lastN <= 0` defaults to the
  ceiling. Newest-first ordering via `OrderByDescending(x
  => x.UnixMs)`. Returns `AdminAuditLogResponse` with
  `TotalMatched` (pre-pagination) + `Entries`.
- `src/Dxs.Consigliere/Dto/Responses/Admin/
  AdminAuditLogResponse.cs` (new) — frozen wire shape.
- `src/Dxs.Consigliere/Setup/CorePlatformSetup.cs` —
  registers `IAuditLogger` singleton.
- `src/admin-ui/src/types/admin.ts` — hand-mirrored
  `AdminAuditLogResponse` + `AdminAuditLogEntryResponse`.
  Lives in `types/admin.ts` until wave-A3 S6's NRT
  inference swaps every screen onto `api.generated.ts`.
- `src/admin-ui/src/lib/api/routes.ts` — new
  `adminAuditLogPath(opts)` helper with URL-encoded
  filters.
- `src/admin-ui/src/lib/admin/admin-client.ts` —
  `IAdminClient.getAuditLog(opts?)` + real-client impl.
- `src/admin-ui/src/lib/mock/admin.ts` — `MockAdminClient.
  getAuditLog` returns three seed entries with realistic
  context blobs for the mock-mode demo. Mock filter logic
  honours `since` / `action` / `username` / `lastN` the
  same way the real backend does.
- `src/admin-ui/src/screens/audit-log/audit-log.store.ts`
  (new) — `AuditLogStore` with action / username / since
  filter chips, single inflight `AbortController`,
  ISO-8601-or-unix-ms `since` parser, `loading | ready |
  error` state machine.
- `src/admin-ui/src/screens/audit-log/AuditLogPage.tsx`
  (new) — MUI DataGrid with columns (when / user /
  action chip / target id / View JSON button → modal).
  Read-only — no mutate / delete affordance anywhere.
- `src/admin-ui/src/app/routes.ts` — `audit-log` entry
  under the System section using the `FactCheck` icon.
- `src/admin-ui/src/app/App.tsx` — lazy-loaded route
  `/audit-log`.
- `src/admin-ui/src/screens/audit-log/
  audit-log.store.test.ts` (new) — 5 vitest cases pin
  the happy-path refresh, the filter mutators, the
  ISO-8601 since conversion, the inflight-abort behaviour,
  and the error-surface.
- `src/admin-ui/tests/contract/admin.test.ts` — new
  describe `GET /api/admin/audit-log → AdminAuditLogResponse`.
  Empty-array baseline (the test host has no broadcast
  history); `expectShape` runs on the wrapper shape.
- `tests/Dxs.Consigliere.Tests/Broadcast/
  BroadcastServiceBehaviorTests.cs` — `Build()` now
  returns a fifth tuple element (`FakeAuditLogger`); two
  new cases pin (a) the happy-path audit record fires
  with the slice-mandated context shape, (b) audit failure
  aborts the broadcast with `audit_write_failed` and
  zero persist / zero announce.
- `src/admin-ui/src/screens/{dashboard,p2p}/*.test.ts` —
  the local `adminStub` helpers gain `getAuditLog` so
  they satisfy the extended `IAdminClient`.
- `src/admin-ui/contracts/swagger.json` +
  `src/admin-ui/src/types/api.generated.ts` regenerated
  with the new endpoint + response shape.

---

## What's in scope for this audit

1. **Fail-stop semantics.** A `false` from `RecordAsync`
   MUST stop the broadcast. The
   `BroadcastAsync_AuditFailure_AbortsBroadcastWithFailedReceipt`
   test pins this. If the audit write throws (not just
   returns false), the logger swallows + returns false so
   the upstream receipt path is consistent.
2. **No sensitive payload leaks.** Context is
   `{ rawHexLength, source }` only — no rawHex / no
   hashes / no addresses / no API keys. Re-read every
   call site that constructs a context.
3. **Read-only forever.** `UpdateableKeys()` returns
   `EmptyKeys`. There is no `PUT` / `DELETE` /
   `PATCH` endpoint. The admin UI has no mutate
   affordance.
4. **Retention via `@expires`.** The metadata header is
   set on every save. Audit on master / RavenDB confirms
   the expiration bundle is enabled before the first
   write (lazy on the singleton).
5. **Username resolution honours the rate-limited
   anonymous case.** When `HttpContext` is null (called
   from a background task) or the user is unauthenticated,
   the record lands with `username = "anonymous"` instead
   of throwing.
6. **No regression of the wave-A2 contract suite.**
   `pnpm test:contract` runs 24/24 green (was 23; +1
   audit-log describe).
7. **`AdminAuditController` admin-only.** Policy
   `AdminAuthDefaults.Policy`, like every other admin
   read.

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
S4 log stream, S6 NRT screen migration, S7 runbook
completion.

## Validation evidence I should produce

- `dotnet build -c Release` clean on Consigliere.
- `dotnet test --filter Broadcast.BroadcastServiceBehaviorTests`
  8/8 green (was 6; +2 audit-fail-stop pins).
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract`
  24/24 green (was 23; +1 audit-log describe).
- `pnpm verify` green (includes the 5 new
  audit-log.store unit cases).
- `bash scripts/secrets-lint.sh` exits 0.
- (Manual smoke) Trigger a broadcast in mock mode via
  the broadcast queue, navigate to `/audit-log`, see a
  freshly-stamped entry.
