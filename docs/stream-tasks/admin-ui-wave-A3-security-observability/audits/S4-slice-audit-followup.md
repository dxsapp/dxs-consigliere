---
created: 2026-05-20
type: slice-audit-followup
parent: docs/stream-tasks/admin-ui-wave-A3-security-observability/audits/S4-slice-audit-prompt.md
status: applied
---

# wave-A3 S4 slice-audit followup (MINOR REVISION REQUIRED)

Codex slice-audit on `1db7948`: MINOR REVISION REQUIRED
(0C / 0H / 1M / 0L). Single finding folded.

## M1 — `LogsStore.resubscribe` wiped the server-flushed snapshot

**Verified:** the SignalR `LogStreamHub.SubscribeToLogs(...)`
implementation flushes the ring-buffer snapshot via
fire-and-forget `Clients.Caller.SendAsync(...)` BEFORE
returning. By the time the client's
`await connection.invoke("SubscribeToLogs", ...)` promise
resolves, the `OnLogEvent` handler has already pumped
those snapshot frames through `this.handle(...)` and into
`this.entries`. The old `resubscribe` then ran
`this.entries = []` AFTER the invoke await — wiping every
snapshot frame and leaving the operator staring at an
empty live tail until the next live emission landed.

In practice this turned every filter-change into a stalled
view: tens of seconds of "Connected. Waiting for backend
log emissions…" while the snapshot the operator had asked
for sat dropped on the floor.

**Revision applied:**

- `LogsStore.resubscribe` now clears `entries` BEFORE the
  invoke, not after. The new snapshot frames arrive into
  the cleared buffer instead of being overwritten by the
  clear. Doc comment in the file makes the snapshot-then-
  ack sequencing explicit so a future maintainer doesn't
  re-introduce the bug.
- `logs.store.test.ts` rewrites the existing "resubscribe
  clears + re-invokes" case as
  `resubscribe clears the old entries BEFORE invoking,
  replays the filters, and keeps the new snapshot`. The
  test uses a new `makeHubWithSnapshotOnSubscribe(...)`
  helper that emits the snapshot via the `OnLogEvent`
  handler DURING the `SubscribeToLogs` invoke — mirroring
  the production sequence. Pre-fix the snapshot is wiped;
  post-fix the snapshot becomes the only content the
  operator sees with the new filters applied.

## Validation

- `dotnet build -c Release` clean on the full solution.
- `pnpm test:contract`: 24/24 green.
- `pnpm verify`: green (5 logs.store cases — the renamed
  pin still counts as the same coverage line, just stricter).
- `bash scripts/secrets-lint.sh` exits 0.

## Residual risk

Manual browser smoke (live Raven + dev compose + paste a
filter change while live emissions are flowing) NOT re-run.
The behavioural test pin covers the same ordering hazard
deterministically (mid-invoke snapshot emit), which the
sequential live-flow smoke can only hit by chance.

## Result

1/1 finding folded. Filter changes in `/logs` no longer
stall the view; the server-side snapshot is the
authoritative content of the cleared buffer at the moment
the new filters take effect.
