# wave-A3 S6 — slice-audit prompt

Audit target: wave-A3 S6 (Swashbuckle NRT inference + screen
migration) on `codex/consigliere-vnext`. Diff range:
`<S4 commit>..<S6 commit>`.

---

You are auditing the **NRT-driven contract tightening
slice**. S6 lands a Swashbuckle schema filter that walks
CLR nullability and populates the OpenAPI `required` array
+ flips schema-level `nullable: false` for NotNull
properties. The admin-ui side migrates the freshly-landed
S3 `AdminAuditLog*` DTOs onto generated re-exports as the
first concrete example of the pattern. Wider hand-mirrored-
interface sweep is documented as a wave-A4 residual.

Read first:

- `docs/stream-tasks/admin-ui-wave-A3-security-observability/master.md`
  (S6 row marked done; Delivery Notes updated; residual
  called out)
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/slices.md`
  § S6 (intent · owned paths · exact task · what-not-to-do
  · validation · completion signal)

Cross-validate against the deliverable:

- `src/Dxs.Consigliere/Swagger/RequiredFromNrtFilter.cs`
  (new) — `ISchemaFilter` walks every property on the
  source CLR type with `NullabilityInfoContext`. When
  `WriteState == NotNull` or `ReadState == NotNull` the
  property is added to `schema.Required` AND
  `OpenApiSchema.Nullable = false` so openapi-typescript
  drops the `| null` from the generated TS union. Handles
  `PropertyInfo` + `FieldInfo`; swallows exotic-generics
  exceptions rather than over-tightening. Member resolution
  is case-insensitive (OpenAPI camelCases).
- `src/Dxs.Consigliere/Setup/PublicApiSetup.cs` —
  registers `c.SchemaFilter<RequiredFromNrtFilter>()` inside
  `AddSwaggerGen`.
- `src/Dxs.Consigliere/Dto/Responses/Admin/AdminAuditLogResponse.cs`
  — `#nullable enable` opt-in + per-property NotNull
  annotations on the wave-A3 S3 audit-log DTOs. Demonstrates
  the per-file migration pattern.
- `src/admin-ui/contracts/swagger.json` +
  `src/admin-ui/src/types/api.generated.ts` regenerated.
  Net effect at this point: 293 lines of new `"required":
  [...]` arrays in the OpenAPI doc, 187 generated-TS lines
  flipped from `?: T | null` to `: T` / `?: T`.
- `src/admin-ui/src/types/admin.ts` — adds
  `import type { components } from "@/types/api.generated";`
  at the top + replaces the wave-A3 S3 hand-mirrored
  `AdminAuditLogEntryResponse` / `AdminAuditLogResponse`
  interfaces with `export type X = components["schemas"]["X"];`
  re-exports. The audit-log screen + store still import
  from `@/types/admin`, so the cutover is behaviorally inert.
- `tests/Dxs.Consigliere.Tests/Swagger/
  RequiredFromNrtFilterTests.cs` (new) — 5 cases pin
  (a) NotNull reference property → required, (b) value-type
  property → required, (c) existing required entries are
  preserved, (d) properties without a CLR match are
  ignored, (e) schemas with no properties no-op.

## Documented residual (NOT in this slice)

The slice's completion signal calls for **every**
hand-mirrored interface in `types/admin.ts` +
`types/auth.ts` to become a re-export. The project's
`Nullable=disable` setting means the wider sweep needs
per-Dto `#nullable enable` + per-property `string?` vs
`string` review for ~30 response files. That's a
disciplined per-file migration with non-trivial
correctness risk per property — the slice ledger logs it
as a wave-A4 residual instead of force-landing under S6.

The pattern is established here on the S3 audit-log DTOs;
each future Dto migration follows the same recipe:

1. Add `#nullable enable` to the .cs file.
2. Annotate every reference property as `string` (NotNull)
   or `string?` (nullable).
3. Initialise NotNull properties (`= string.Empty` /
   collection initialiser) so the compiler is satisfied.
4. Regen swagger + api.generated.ts; the corresponding
   schema gains a `required` entry + `nullable: false`.
5. Replace the hand-mirrored TS interface with the
   re-export.

---

## What's in scope for this audit

1. **Filter correctness.** NotNull reference + value-type
   properties land in `required` AND get `nullable: false`.
   Nullable ones stay optional + nullable. The 5 unit cases
   pin the matrix.
2. **No over-tightening on exotic types.** Exceptions out
   of `NullabilityInfoContext.Create(...)` are swallowed
   per-property; one bad property doesn't poison the
   schema.
3. **Existing `[Required]` entries survive.** The filter
   builds a `SortedSet<string>` seeded from
   `schema.Required ?? new()`. A property tagged
   `[Required]` whose CLR type is nullable stays required.
4. **Audit-log re-export is behaviorally identical.** The
   admin UI's audit-log screen + store still compile; the
   contract test (`GET /api/admin/audit-log →
   AdminAuditLogResponse`) still validates against the
   ajv-compiled generated schema.
5. **No regression of any other admin UI screen.**
   `pnpm verify` still green — the screen surface is
   unchanged, the only TS shapes that moved are the two
   S3 DTOs.

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

Out of scope (residual): wholesale hand-mirrored-interface
→ generated re-export sweep across `types/admin.ts` and
`types/auth.ts` — logged as a wave-A4 residual.

## Validation evidence I should produce

- `dotnet build -c Release` clean on Consigliere.
- `dotnet test --filter Swagger.RequiredFromNrtFilterTests`:
  5/5 green.
- `pnpm verify` green — including the regenerated
  `api.generated.ts` + the audit-log re-export.
- `pnpm test:contract`: 24/24 green.
- `bash scripts/secrets-lint.sh`: exits 0.
- (Manual) `grep -E '"required":' src/admin-ui/contracts/
  swagger.json | wc -l` — confirms the post-S6 doc carries
  many more `required` arrays than before the filter
  landed.
