MINOR REVISION REQUIRED

# wave-A3 S4 slice audit

Audit range checked: `b12fb2f..1db7948` on `codex/consigliere-vnext`.
HEAD during validation: `fc6cc19`.

Note: the working tree had pre-existing local S3 audit follow-up edits while
this audit ran:

- `src/Dxs.Consigliere/Services/Audit/AuditLogger.cs`
- `src/Dxs.Consigliere/Setup/CorePlatformSetup.cs`
- `src/admin-ui/src/lib/mock/admin.ts`
- `tests/Dxs.Consigliere.Tests/Services/Audit/`
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/audits/S3-slice-audit.md`

Those paths are outside the S4 diff range, but they affect full solution
build validation.

## Findings

### L1 - Client refresh discards the server snapshot sent by SubscribeToLogs

file: `src/admin-ui/src/screens/logs/logs.store.ts:106`

`LogStreamHub.SubscribeToLogs` flushes the ring buffer snapshot before the
SignalR invocation completes (`LogStreamHub.cs:38-42`). That is the right
server-side contract for late joiners and filter refreshes. The client,
however, calls `SubscribeToLogs` and only then clears `entries`
(`logs.store.ts:106-113`). Any snapshot events delivered during the invoke
are appended by `handle()` and then removed after the await completes. The
current test pins the buggy behaviour by expecting resubscribe to leave the
store empty (`logs.store.test.ts:96-106`).

Impact: after an operator changes filters or clicks refresh, the UI can show
an empty log tail even though the server did send the current 1000-entry
ring. The stream recovers only when new backend log events arrive, reducing
the usefulness of the live-tail observability panel.

Recommended fix: clear the local view before invoking `SubscribeToLogs`, or
tag a resubscribe generation and ignore only stale pre-invoke entries. Update
the store test to simulate the hub emitting a snapshot during `invoke()` and
assert that the seeded entries remain visible after `resubscribe()` resolves.

## Positive Checks

- `LogStreamProvider` sanitizes both formatted `Message` and
  `Exception.ToString()` before publishing to `LogStreamBuffer`.
- `LogStreamBuffer` is an in-memory 1000-entry ring and drops the oldest entry
  on overflow without blocking publishers.
- Subscriber callback failures are swallowed so a bad client-side fan-out path
  cannot take down the publisher loop.
- `LogStreamHub` is protected with
  `[Authorize(Policy = AdminAuthDefaults.Policy)]` and is mapped at `/ws/logs`.
- `SubscribeToLogs` replaces the previous per-connection subscription after
  registering the new one, so repeated calls do not accumulate duplicate live
  subscriptions.
- The frontend re-runs the wave-A1 sanitizer rules over both `message` and
  `exception` before rendering.
- The client store creates a fresh SignalR connection per page mount and does
  not persist subscriptions across reconnects.

## Validation Evidence

- `bash scripts/secrets-lint.sh`: passed.
- `dotnet build Dxs.Consigliere.sln -c Release`: failed in current dirty
  worktree due non-S4 local audit follow-up files:
  `AuditLoggerTests.cs` still references `IMaintenanceOperationExecutor`, and
  calls the old `AuditLogger` constructor without `IAuditRetentionConfigurator`.
- `dotnet test tests/Dxs.Consigliere.Tests/Dxs.Consigliere.Tests.csproj -c Release --no-build --filter "Logging"`:
  passed 12/12.
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract`: passed 24/24.
- `pnpm verify`: passed (typecheck, lint, 192 unit tests, build, budget,
  inventory, contracts check).

## Residual Risk

Manual Docker/browser smoke from the prompt was not run during this audit:
`docker compose --profile dev up -d --build`, login as admin, open `/logs`,
generate backend traffic, verify live streaming, verify cookie-less `/ws/logs`
is rejected, and confirm no token/WIF/API-key/plain long-hex payload is visible
on the wire.
