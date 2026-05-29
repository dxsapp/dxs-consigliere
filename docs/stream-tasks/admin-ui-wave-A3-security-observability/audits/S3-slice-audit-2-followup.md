---
created: 2026-05-21
type: slice-audit-followup
parent: docs/stream-tasks/admin-ui-wave-A3-security-observability/audits/A1.md
status: applied
---

# wave-A3 S3 slice-audit-2 followup (wave-closeout re-audit)

Source: the wave-closeout re-audit (fresh-model pass over the
full `c4d50ce..c192dcd` range), recorded in this session. The
re-audit found one cross-slice defect that no per-slice codex
audit could have caught, because it lives in the **interaction**
between S3 (audit log) and the three real callers of
`IBroadcastService.BroadcastAsync` — something each per-slice
diff review structurally never saw.

Verdict on the re-audit: wave APPROVE WITH CHANGES; S3 carried
one Medium. This is the fold.

## The finding (Medium) — false provenance + background fail-stop coupling

S3's `BroadcastService` hardcoded `source: "admin-ui"` in the
audit context and applied fail-stop unconditionally. But
`BroadcastAsync` has **three** callers with different
provenance:

- `TransactionController` (`POST /api/tx/broadcast`) — the admin
  UI hits this **with a cookie** (authenticated), but external
  wallet API clients hit the same endpoint **anonymously**.
- `WalletHub` (SignalR) — wallet clients.
- `UnconfirmedTransactionsMonitor` — a **background task**, no
  human actor.

Two consequences:

1. **False attribution.** Wallet + system broadcasts were
   stamped `source: "admin-ui"`. A system auto-rebroadcast was
   indistinguishable from — and falsely attributed to — an
   operator action in the forensic log, undermining the exact
   thing S3 exists to provide.
2. **Background-task fail-stop regression.** The unconditional
   fail-stop meant `UnconfirmedTransactionsMonitor`'s Wave-5
   "always-eventually-broadcast" retry loop would silently halt
   whenever the Raven audit write failed. S3's fail-stop was
   designed for the operator-initiated destructive action, not
   the background retry.

### Correction to the re-audit's first draft

The re-audit initially called this **High**, claiming the audit
"can never answer who" because `/api/tx/broadcast` is anonymous.
That was wrong: `UseAuthentication()` runs the default cookie
scheme on **every** request and populates `HttpContext.User`
regardless of `[Authorize]` (authentication ≠ authorization).
`CreatePrincipal` sets `ClaimTypes.Name`, so an authenticated
admin-UI broadcast **does** record the real username. The
operator "who" works today; the defect is provenance + the
background coupling, hence Medium.

## Resolution (wide variant, per operator decision)

Decision: audit **all** paths with honest provenance ("don't
skimp on logs until it's well-tested"), fail-stop **only** the
operator path.

- New `BroadcastSource` enum (`Operator` / `Api` / `Wallet` /
  `System`) + `ToWire()` lowercase mapping, in
  `Services/IBroadcastService.cs`.
- `BroadcastAsync` gains a required `BroadcastSource source`
  parameter (p[1]). The audit context now carries
  `source = source.ToWire()` instead of the hardcoded
  `"admin-ui"`.
- Fail-stop is now `if (!auditOk && source == Operator)`. Wallet
  / api / system broadcasts are audited best-effort (RecordAsync
  logs its own failure) and proceed regardless.
- Callers stamp their true provenance:
  - `TransactionController` derives it —
    `User?.Identity?.IsAuthenticated == true ? Operator : Api`.
  - `WalletHub` → `Wallet`.
  - `UnconfirmedTransactionsMonitor` → `System`.
- `IBroadcastServiceShapeTests` updated to pin the new 4-param
  canonical signature (source is required — no default — so
  every caller must declare provenance).
- `BroadcastServiceBehaviorTests`: all call sites pass an
  explicit source; the happy-path audit test now asserts the
  recorded `source` is `"operator"`; **two new pins**:
  - `BroadcastAsync_HonestSource_StampedPerCaller` — wallet /
    system / api each land their own wire string.
  - `BroadcastAsync_AuditFailure_NonOperator_ProceedsAnyway` —
    a System broadcast whose audit write fails still persists +
    announces (the background-fail-stop regression pin).
- admin-ui mock (`lib/mock/admin.ts`) + its test updated:
  `broadcastRaw` and the seed entries now use `source:
  "operator"` to match the backend's new vocabulary.

## Validation

- `dotnet build Dxs.Consigliere.sln -c Release`: clean.
- `dotnet test --filter Broadcast.BroadcastServiceBehaviorTests|
  Broadcast.IBroadcastServiceShapeTests|Services.Audit.AuditLoggerTests`:
  16/16 green.
- `pnpm verify`: green.
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract`: 24/24
  green.
- `bash scripts/secrets-lint.sh`: exits 0.

## Re-audit findings NOT folded (logged)

The same re-audit surfaced four Low items, left as documented
residuals (none exploitable as-shipped):

- **L1 (S0):** `ProxyHeadersSetup.cs` comment still points at the
  non-functional `ForwardedHeadersOptions__KnownProxies__0` env
  knob that S0-audit L1 corrected only in the runbook. Stale
  comment.
- **L2 (S0/S1):** `KnownNetworks`+`KnownProxies` cleared → Kestrel
  trusts `X-Forwarded-For` from any direct caller. Safe
  as-shipped (Caddy overwrites XFF; Kestrel is `expose`-only),
  but there's no working knob to tighten it.
- **L3 (S2):** `/health/ready` is anonymous + rate-limit-exempt +
  fires 3 outbound HEAD probes per hit — mild amplification /
  self-DoS surface.
- **L4 (S5):** `WriteFileAsync` creates the tmp file at default
  umask before chmod 600 — brief world-readable window on a
  multi-user host.

These are tracked for a future hardening pass; the operator
decision was to fold only the Medium now.
