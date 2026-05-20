MAJOR REVISION REQUIRED

# wave-A3 S3 slice audit

Audit range checked: `d6bc356..b12fb2f` on
`codex/consigliere-vnext`. Validation ran at HEAD `fc6cc19`
after later S4/S6 commits.

## Findings

### M1 — Expiration setup failure still allows the destructive operation to proceed

file: `src/Dxs.Consigliere/Services/Audit/AuditLogger.cs:99`

`RecordAsync` calls `EnsureExpirationConfiguredAsync`, but that helper
catches every exception from `ConfigureExpirationOperation`, logs a
warning, resets `_expirationConfigured`, and then returns normally. The
outer `RecordAsync` continues to store the audit entry and returns
`true`, so `BroadcastService` proceeds with the broadcast even though
the S3 retention contract was not established. This contradicts the
slice contract that Raven expiration is enabled before the first write
and the interface comment that an audit infrastructure failure must
make callers fail-stop.

Why it matters: a Raven permission/configuration issue can silently turn
the audit log into an unbounded collection while still allowing
destructive operations. Operators get a warning in logs, but the API
receipt says the broadcast path succeeded.

Recommended fix: make expiration configuration part of the successful
audit write. Let `EnsureExpirationConfiguredAsync` throw, or return
`false` from `RecordAsync` when `ConfigureExpirationOperation` fails,
while still re-arming `_expirationConfigured` for a later retry. Add a
unit/integration test where the maintenance operation fails and assert
`RecordAsync` returns `false` and `BroadcastService` returns
`audit_write_failed` with zero persist / zero announce.

### L1 — Mock-mode broadcast smoke cannot create the expected fresh audit entry

file: `src/admin-ui/src/lib/mock/admin.ts:167`

The audit prompt asks for a mock-mode smoke where an operator triggers a
broadcast and then sees a freshly-stamped `/audit-log` entry. The mock
client's `broadcastRaw` only returns a receipt; `getAuditLog` always
generates the same three seeded entries from `nowMs()` and does not
append an entry for the broadcasted tx. The page can render seeded rows,
but the mock smoke does not exercise the actual user flow.

Why it matters: this weakens the frontend proof for the S3 workflow and
can make a demo look successful even when the broadcast action did not
feed the audit surface.

Recommended fix: keep an in-memory audit-entry array on
`MockAdminClient`; have `broadcastRaw` prepend a `broadcast_tx` entry
with the returned txid, `rawHexLength`, and `source: "admin-ui"`, then
have `getAuditLog` filter/page the combined dynamic + seed entries.

## Positive Checks

- `AuditLogEntry` inherits `Entity`, uses the `audit-log/<unixMs>/<guid8>`
  id prefix, and returns `EmptyKeys` from `UpdateableKeys()`.
- There are no `PUT`, `PATCH`, or `DELETE` audit endpoints; the backend
  surface is a single admin-authorized `GET /api/admin/audit-log`.
- `BroadcastService` audits valid broadcasts after policy validation and
  before `OutgoingStore.SaveAsync` / announcer dispatch.
- The broadcast context payload is limited to `{ rawHexLength, source }`;
  no raw hex, hashes, addresses, or secrets are included in the context.
- `AuditLogger` resolves missing/unauthenticated users to
  `"anonymous"` instead of throwing.
- `AdminAuditController` applies `AdminAuthDefaults.Policy`, clamps
  `lastN` to 1000, reports `TotalMatched` before pagination, and returns
  newest-first rows.
- Admin UI `/audit-log` is read-only and uses a JSON context dialog
  without mutate/delete affordances.

## Validation Evidence

- `bash scripts/secrets-lint.sh`: passed.
- `dotnet build Dxs.Consigliere.sln -c Release`: passed, 0 errors
  (existing warnings remain).
- `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj
  -c Release --no-build --filter "Broadcast.BroadcastServiceBehaviorTests"`:
  passed 8/8.
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract`: passed 24/24.
- `pnpm verify`: passed (typecheck, lint, 192 unit tests, build, budget,
  inventory, contracts check).

## Residual Risk

The manual browser smoke from the prompt was not run during this audit.
The mock-mode limitation above should be folded before using that smoke
as evidence for the end-to-end audit-log workflow.
