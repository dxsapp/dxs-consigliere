# wave-A3 S4 — slice-audit prompt

Audit target: wave-A3 S4 (Backend log streaming) on
`codex/consigliere-vnext`. Diff range:
`<S3 commit>..<S4 commit>`.

---

You are auditing **the live log tail slice**. S4 replaces
the wave-A1 admin-ui paste-box `/logs` screen with a real
SignalR-backed live tail of the backend log stream. The
sanitizer regex set moves to the BACKEND as the primary
pass; the client retains the same rules as defence-in-depth.

Read first:

- `docs/stream-tasks/admin-ui-wave-A3-security-observability/master.md`
  (S4 row marked done; Delivery Notes updated)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/slices.md`
  § S4 (intent · owned paths · exact task · what-not-to-do
  · validation · completion signal)

Cross-validate against the deliverable:

- `src/Dxs.Consigliere/Logging/LogEventDto.cs` (new) —
  frozen wire shape `{ UnixMs, Level, Category, Message,
  Exception }`. Comment makes the wire/sanitization
  contract explicit.
- `src/Dxs.Consigliere/Logging/LogSanitizer.cs` (new) —
  `[GeneratedRegex]`-compiled regex set, ports every rule
  from the wave-A1 admin-ui paste box. `Apply(string raw)`
  runs the rules in order; idempotent on already-clean input.
- `src/Dxs.Consigliere/Logging/LogStreamBuffer.cs` (new) —
  in-process ring (`Queue<LogEventDto>`) with `Capacity =
  1000`, drop-front-on-overflow (NOT block). `Subscribe`
  registers a per-connection callback; a misbehaving
  subscriber cannot take down the publisher (every callback
  is wrapped in `try { ... } catch { /* swallow */ }`).
- `src/Dxs.Consigliere/Logging/LogStreamProvider.cs` (new)
  — `ILoggerProvider` implementation (MEL) that captures
  EVERY `ILogger<T>` emission. The slice owned-path called
  for a Serilog sink; the file's doc comment explicitly
  notes that the project doesn't wire `UseSerilog(...)`
  into the host, so an MEL provider has the broadest
  surface. Sanitization runs at emit time, before the
  entry enters the buffer.
- `src/Dxs.Consigliere/WebSockets/LogStreamHub.cs` (new)
  — `[Authorize(Policy = AdminAuthDefaults.Policy)]` hub
  at `Route = "/ws/logs"`. `SubscribeToLogs(minLevel?,
  categoryFilter?)` flushes the ring snapshot first
  (filtered), then subscribes to live emissions. Per-
  connection subscriptions tracked in a static
  `ConcurrentDictionary<string, IDisposable>`; `OnDisconnectedAsync`
  + `UnsubscribeFromLogs` both dispose the entry.
  `SendAsync` is fire-and-forget so a slow client cannot
  back-pressure the publisher (the slice's "Don't make the
  SignalR subscription persistent across reconnects"
  contract is honoured because the buffer is in-memory).
- `src/Dxs.Consigliere/Setup/CorePlatformSetup.cs` —
  registers `LogStreamBuffer` singleton + adds
  `LogStreamProvider` as an `ILoggerProvider` factory.
- `src/Dxs.Consigliere/Setup/SignalRSetup.cs` — adds
  `MapHub<LogStreamHub>(LogStreamHub.Route)` next to the
  existing `WalletHub` mapping with the same
  `HttpTransportType.WebSockets` constraint.
- `tests/Dxs.Consigliere.Tests/Logging/LogSanitizerTests.cs`
  (new) — 7 cases pin the per-rule redaction set
  (authorization, cookie, WIF, double + single quoted
  api-key, long hex, clean passthrough).
- `tests/Dxs.Consigliere.Tests/Logging/LogStreamBufferTests.cs`
  (new) — 5 cases pin snapshot ordering, drop-oldest
  eviction, subscription fan-out, dispose, and the
  throwing-subscriber resilience.
- `src/admin-ui/src/screens/logs/sanitizer.ts` (new) —
  the wave-A1 paste-box sanitizer rules extracted into a
  module so both the wire-payload defence-in-depth and the
  retained unit suite can import them.
- `src/admin-ui/src/screens/logs/logs.store.ts` (new) —
  `LogsStore` owns its own HubConnection for `/ws/logs`
  (the global signalr client is hardwired to
  `/ws/consigliere`). Status state machine `idle →
  connecting → live | error | stopped`. `setMinLevel` /
  `setCategoryFilter` mutate the filter; `resubscribe()`
  flushes the local buffer and re-invokes
  `SubscribeToLogs` so the next snapshot starts clean.
  Test seam: `buildConnection` factory injected.
- `src/admin-ui/src/screens/logs/LogsPage.tsx` —
  rewritten. Paste box gone. MUI Paper with overflow-y
  scroll of the latest 1000 entries; min-level chip,
  category substring filter, refresh + clear icon
  buttons.
- `src/admin-ui/src/screens/logs/LogsPage.test.tsx` —
  retargeted at `./sanitizer` so the same 8 regex pins
  cover the now-extracted client sanitizer module.
- `src/admin-ui/src/screens/logs/logs.store.test.ts` (new,
  5 cases) — start-subscribes-with-filters, OnLogEvent
  appends + re-sanitizes, buffer-cap eviction, error
  surface when the invoke throws, resubscribe clears +
  re-invokes.

---

## What's in scope for this audit

1. **Server-side sanitizer is the primary.** Every
   `LogStreamProvider.Log` emission MUST go through
   `LogSanitizer.Apply` before reaching the buffer.
   Verify Message + Exception both run through.
2. **Buffer doesn't block on overflow.** `LogStreamBuffer.Publish`
   under a 1500-entry burst MUST drop the oldest 500
   without throwing or blocking the caller.
3. **Hub is admin-only.** `[Authorize(Policy = AdminAuthDefaults.Policy)]`
   is on the class. An unauthenticated WebSocket upgrade
   request to `/ws/logs` MUST be rejected — same gate as
   the rest of the admin surface.
4. **No raw sensitive payload on the wire.** Trace a
   message through `LogStreamProvider.Log` →
   `LogSanitizer.Apply` → buffer → hub `SendAsync`. The
   payload that hits the wire is sanitized.
5. **No regression of the wave-A1 sanitizer rule set.**
   The 8 admin-ui sanitizer cases still pass after
   retargeting to the extracted `./sanitizer` module.
6. **Client retains defence-in-depth.** `LogsStore.handle`
   re-runs the same regex pass — even if a hot path
   future-bypasses LogSanitizer the rendered DOM stays
   clean.
7. **Subscription is not cross-reconnect-durable.** Backend
   restart wipes the ring; client `start()` re-seeds from
   whatever is in memory at subscribe time. Per the slice's
   what-not-to-do constraint.

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

Out of scope (later slices): S6 NRT screen migration, S7
runbook completion.

## Validation evidence I should produce

- `dotnet build -c Release` clean on Consigliere.
- `dotnet test --filter Logging`: 12/12 green.
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract`:
  24/24 green.
- `pnpm verify`: green (incl. 13 logs-module unit cases
  total: 8 sanitizer + 5 store).
- `bash scripts/secrets-lint.sh` exits 0.
- (Manual smoke) `docker compose --profile dev up -d
  --build`, log into the admin UI, navigate to `/logs`,
  trigger any backend activity (POST /api/admin/auth/me),
  see the entry render in the live tail.
