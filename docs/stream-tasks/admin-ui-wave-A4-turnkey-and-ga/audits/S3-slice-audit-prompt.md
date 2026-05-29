# wave-A4 S3 — slice-audit prompt

Audit target: wave-A4 S3 (NRT hand-mirrored-interface sweep) on
`codex/consigliere-vnext`. Diff range: `f4eb1d7..bf68fe2`.

---

You are auditing the **slice that finishes wave-A3 S6's
documented residual**: the admin UI must consume generated wire
types end-to-end, so a backend DTO rename becomes a screen-side
TS compile error — not just a `contracts:check` CI diff that a
hand-mirrored interface would silently absorb.

Read first:
- `docs/stream-tasks/admin-ui-wave-A4-turnkey-and-ga/master.md`
- `docs/stream-tasks/admin-ui-wave-A4-turnkey-and-ga/slices.md` § S3
- wave-A3 S6's closeout note on the NRT filter + the residual it
  deferred (the ~30 un-migrated `Dto.cs` files).

## What landed

- ~18 response DTOs migrated to `#nullable enable` with
  per-property nullability; `NotNull` props are initialised so
  they lose the generated `?`. Families: AdminProviders /
  ProviderConfig / Catalog, AdminTracked* (address / token /
  summaries), TrackedHistory* / RootedToken* /
  TrackedEntityReadiness, Setup* (status / complete / requests),
  AdminAuthStatus / AdminLogin, BroadcastReceiptDto, metrics.
- `types/admin.ts` + `types/auth.ts` collapsed to
  `export type X = components["schemas"]["X"]` re-exports. Kept
  the non-generated helpers: `OutgoingTxState` union + `TX_*`
  literal tables, `SOURCE_KEYS`.
- **peers endpoint SEALED**: new `AdminPeersResponse` +
  `AdminPeerRow` DTOs; `AdminP2pController` now returns the typed
  shape (was an anonymous object → emitted NO schema, so the UI
  could not re-export it). Now generated + re-exported.
- `RequiredFromNrtFilter`: a nullable object-typed property is
  emitted as `{ nullable: true, allOf: [{ $ref }] }`
  (OpenAPI 3.0 idiom — `nullable` can't sit beside a bare
  `$ref`), so openapi-typescript yields `T | null` not
  `T | undefined` and screen null-checks survive.
- `_schema-validator.ts` (contract test): collapses that
  `nullable + allOf + $ref` shape to `anyOf: [ref, null]` — AJV
  rejects `nullable` on a typeless schema
  ("nullable cannot be used without type").
- screen cascade fixes from the type tightening: `p2p.store`
  (`lastSeen ?? null`), `ConfigurationPage`, `TokenDetailPage`,
  `tracking-stages`, `stores/root`.

## What's in scope

1. **Completion signal.** `grep -rn "^export interface
   (Admin|P2p|Source|Setup|Tracked|Rooted|Outgoing|Block|
   Broadcast|Headers)" src/admin-ui/src/types/{admin,auth}.ts`
   returns ONLY `BlockTipDto`. Confirm BlockTipDto is a
   legitimate exception (a SignalR PUSH dto — not a REST
   endpoint, so Swashbuckle emits no schema to re-export) and
   that its comment says so.
2. **No silent contract loosening.** The migrated DTOs must not
   have turned a genuinely-nullable wire field into a required
   one (which would make the UI trust a field the backend may
   omit). Spot-check that `NotNull` initialisation matches the
   actual serializer output — e.g. a list response whose
   `summary` can be null must stay nullable, not get a
   non-null initialiser.
3. **peers seal is faithful.** `AdminPeersResponse` /
   `AdminPeerRow` shape matches what `AdminP2pController` used to
   return anonymously (field names, nullability of `lastSeen`).
   No field dropped or renamed silently.
4. **The `nullable+allOf` filter path is sound.** Verify the
   filter only wraps object-typed (`$ref`) nullable props, never
   a scalar (scalars already carry `type` + `nullable`). Verify
   `_schema-validator.ts` only collapses the exact
   single-element `allOf`+`$ref`+`nullable:true` shape and does
   not swallow a legitimate multi-member `allOf`.
5. **Gates green.** `pnpm verify`, `pnpm test:contract` 24/24,
   `dotnet build Dxs.Consigliere.sln -c Release`, `secrets-lint`.

## Known residuals (documented, not defects to fold)

- **Recovery integration.** The sweep was delegated; the agent's
  terminal report was lost to a transient 500 before it
  committed. The operator integrated the landed work and fixed
  two cascades the agent missed (the `p2p.store` `lastSeen` type
  and the AJV validator's `nullable+allOf` handling), then
  re-ran all four gates green. The audit should treat the diff
  on its merits — the provenance does not change the contract.
- **BlockTipDto stays hand-written** — documented exception
  (SignalR push, no REST schema). This is the single remaining
  hand-authored shape and is intentional.

## Validation evidence (this run)

- `dotnet build Dxs.Consigliere.sln -c Release`: clean.
- `pnpm verify`: green (195 unit).
- `RAVEN_URL=http://127.0.0.1:18080 pnpm --dir src/admin-ui
  test:contract`: 24/24 (was 6/24 failing on
  `"nullable" cannot be used without "type"` until the
  `_schema-validator.ts` collapse landed).
- `bash scripts/secrets-lint.sh`: 0.
- completion grep: ONLY `BlockTipDto`.

## Verdict + finding format

Verdict first (`APPROVE` / `APPROVE WITH CHANGES` / `MAJOR
REVISION REQUIRED`); findings tagged `C*|H*|M*|L*` with
file:line, why it matters, and a specific fix.
