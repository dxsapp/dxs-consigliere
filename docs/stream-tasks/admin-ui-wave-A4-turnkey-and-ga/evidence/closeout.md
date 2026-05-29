# Wave-A4 — closeout

## Result

Consigliere now has a single coherent self-host path — pull the
published image → `docker compose -f compose.local.yml up -d` →
`http://localhost:5000` → complete the wizard → live-ingesting
product, with no domain, no ACME, no curl, zero mandatory env.
The wave-A3 S6 NRT residual is closed (admin UI consumes
generated types end-to-end), and the GA-validation harness +
checklist are shipped with the real-infra run left honestly
operator-pending.

## Key files

- `compose.local.yml` — standalone plain-HTTP localhost stack on
  the published image.
- `src/Dxs.Consigliere/appsettings.Local.json` — `Local` env
  overlay (cookieSecure=SameAsRequest), inherits the real
  Mainnet/tasks-on base config.
- `README.md` — "Run locally" quickstart as the primary docker
  story.
- `scripts/soak-watch.sh` + `evidence/S2-ga-checklist.md` —
  GA-validation harness + operator checklist.
- `src/admin-ui/src/types/{admin,auth}.ts` — pure generated
  re-exports (only `BlockTipDto` stays hand-written: SignalR
  push, no REST schema).
- `src/Dxs.Consigliere/Dto/Responses/Admin/AdminPeersResponse.cs`
  — peers endpoint sealed into a typed, schema-bearing shape.
- `src/Dxs.Consigliere/Swagger/RequiredFromNrtFilter.cs` +
  `src/admin-ui/tests/contract/_schema-validator.ts` — the
  nullable-object-ref bridge (`{nullable,allOf:[$ref]}` → TS
  `T|null`; collapsed to `anyOf:[ref,null]` for AJV).

## Behavioral summary

- A competent operator gets from zero to a working product in
  the browser, on plain HTTP, with no infrastructure — the
  "download → run → use" story is real, not two disjoint
  half-paths.
- Prod posture is untouched: the `prod` profile still
  materialises TLS-always + `cookieSecure=Always`; the
  plain-HTTP downgrade is confined to the local profile.
- A backend DTO rename is now a screen-side TS compile error,
  not a silently-absorbed contract diff.

## Residuals (honest)

- The published image must be cut (first `vX.Y.Z` release) before
  the literal "pull our image" step works end-to-end; validated
  against a local build until then.
- S2's real-VM/real-domain bring-up, ≥24h soak, broadcast-to-
  real-peers, and <45-min runbook stopwatch are operator-run,
  evidence-pending — infrastructure the engineering loop cannot
  self-certify.
- Out of scope (rationale in `master.md`): multi-instance HA
  (A5), RBAC/multi-user, OpenTelemetry tracing, Raven
  encryption-at-rest.

## Delivery

| slice | commit | status |
|---|---|---|
| S1 | `fd67479` (+ backfill `6f31b4e`) | done |
| S2 | `f4eb1d7` | prepared — operator-run pending |
| S3 | `bf68fe2` | done |
