---
created: 2026-05-20
type: slice-audit-followup
parent: docs/stream-tasks/admin-ui-wave-A3-security-observability/audits/S3-slice-audit-prompt.md
status: applied
---

# wave-A3 S3 slice-audit followup (MAJOR REVISION REQUIRED)

Codex slice-audit on `b12fb2f`: MAJOR REVISION REQUIRED
(0C / 0H / 1M / 1L). Both findings folded.

## M1 — Expiration-bundle failure was swallowed; broadcast proceeded without retention

**Verified:** `AuditLogger.EnsureExpirationConfiguredAsync`
caught the exception from `ConfigureExpirationOperation`
and only emitted a warning. The outer try block then went
on to store the entry — the document landed with an
`@expires` metadata header, but the Raven cluster wasn't
configured to honour it. The destructive broadcast cleared
its fail-stop gate while the retention contract silently
held no guarantee. A future operator-side
"why is the audit log growing forever?" investigation would
have rediscovered this.

**Revision applied:**

- The lazy "configure bundle once per process" path now
  re-throws on failure. `RecordAsync`'s outer catch turns
  the exception into `return false`; `BroadcastService`
  short-circuits with `audit_write_failed` and the
  broadcast aborts.
- The "configured" flag is `Interlocked.Exchange`d back to
  `0` before the rethrow so the very next call retries
  the bundle-enable. Transient Raven hiccups don't
  permanently lock the audit subsystem.
- The bundle-enable code moved into a new
  `IAuditRetentionConfigurator` interface +
  `RavenAuditRetentionConfigurator` implementation. The
  indirection exists exclusively so the fail-stop contract
  is unit-testable — Raven's
  `MaintenanceOperationExecutor` is sealed and can't be
  cleanly mocked.
- DI: `services.AddSingleton<IAuditRetentionConfigurator,
  RavenAuditRetentionConfigurator>()` next to the existing
  `IAuditLogger` registration.
- New unit suite `tests/Services/Audit/AuditLoggerTests.cs`
  (3 cases):
  - `RecordAsync_returns_false_when_retention_configurator_throws`
    — strict-mode `IDocumentStore` mock; verifies
    `OpenAsyncSession` is **never** called when the
    configurator throws. Pins the absence of a side-effect.
  - `Failed_configuration_re_arms_so_the_next_call_retries`
    — drives two `RecordAsync` calls against a
    throwing-stub configurator; asserts both reached it.
  - `RavenAuditRetentionConfigurator_rethrows_on_failure_and_re_arms_the_lazy_guard`
    — production-class pin against an unreachable
    `DocumentStore` (`http://127.0.0.1:1`). Confirms both
    invocations throw (the second one *does* try again
    instead of short-circuiting).

## L1 — Mock-mode `broadcastRaw` didn't generate a fresh audit entry

**Verified:** `MockAdminClient.broadcastRaw` returned a
receipt but the audit list was a static seed. The slice's
documented mock-mode smoke — "trigger a broadcast → see the
entry in /audit-log" — was only validating that the
hard-coded seed entries rendered.

**Revision applied:**

- Audit entries promoted to instance state on
  `MockAdminClient`; seeded lazily on first read or write.
- `broadcastRaw` now prepends a freshly-stamped entry
  matching the wire shape the real backend writes: same
  `targetId` as the returned receipt's `txId`, same
  `unixMs` as `createdAtMs`, context `{ rawHexLength,
  source: "admin-ui" }`.
- `getAuditLog` reads from the instance list, preserving
  the slice's `since` / `action` / `username` / `lastN`
  filter semantics + newest-first ordering.
- New vitest suite `src/lib/mock/admin.test.ts` (3 cases):
  - txid match between receipt + new audit entry
  - context carries `rawHexLength` + `source` but never
    the raw hex bytes (slice "what not to do" check)
  - `totalMatched` grows by exactly one per broadcast.

## Validation

- `dotnet build -c Release` clean on the full solution
  (50 warnings, 0 errors — unchanged from S3).
- `dotnet test --filter Services.Audit.AuditLoggerTests`:
  3/3 green.
- `dotnet test --filter Broadcast.BroadcastServiceBehaviorTests`:
  8/8 green (no regression — the existing
  `FakeAuditLogger` is interface-typed, so the new
  configurator parameter doesn't touch the suite).
- `pnpm test:contract`: 24/24 green.
- `pnpm verify`: green (incl. 3 new
  `lib/mock/admin.test.ts` cases for L1).
- `bash scripts/secrets-lint.sh` exits 0.

## Residual risk

Manual browser smoke (live Raven + admin UI + the
broadcast queue) NOT re-run — the audit prompt's
last-step smoke was already noted as residual when the
audit landed. The behavioural pins (above) cover both
findings end-to-end at the unit + integration boundary.

## Result

2/2 findings folded. The slice's fail-stop contract is
now actually enforced even when Raven refuses the
expiration bundle, and the mock-mode broadcast→audit-log
round-trip is wired so the documented smoke is a real
verification rather than a stale-seed display.
