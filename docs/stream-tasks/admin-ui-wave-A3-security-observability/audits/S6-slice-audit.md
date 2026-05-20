APPROVE WITH CHANGES

# wave-A3 S6 slice audit

Audit range checked: `cef3663..a36e2a9` on
`codex/consigliere-vnext`. Validation ran at HEAD `9e83d15`
after the later S3 audit fold.

## Findings

### L1 - Unit tests do not pin the nullable=false half of the filter contract

file: `tests/Dxs.Consigliere.Tests/Swagger/RequiredFromNrtFilterTests.cs:22`

The S6 contract is two-part: a NotNull CLR member must be added to the
OpenAPI `required` array and its property schema must get
`Nullable = false`, because `openapi-typescript` uses that flag to drop the
`| null` union. The production filter implements both behaviours in
`RequiredFromNrtFilter.cs:47-60`, but the unit tests only assert membership in
`schema.Required`. A future edit could remove the `openApiProperty.Nullable =
false` assignment and all five filter tests would still pass.

Why it matters: the generated admin UI type would drift back toward
`field: T | null` or `field?: T | null` for NotNull DTO members, weakening the
contract-tightening slice without tripping the focused backend test suite.

Recommended fix: initialize the test schema properties with `Nullable = true`
for the NotNull reference/value cases and assert they become `false`. Also
assert nullable reference / nullable value members remain nullable, and in the
existing-required test assert a nullable CLR member preserved by `[Required]`
does not get forced to `nullable: false`.

## Positive Checks

- `RequiredFromNrtFilter` resolves public properties and fields
  case-insensitively, covering Swashbuckle's default camelCase names.
- The filter preserves pre-existing `schema.Required` entries by seeding a
  `SortedSet<string>` from the existing collection.
- The filter catches per-member `NullabilityInfoContext.Create(...)`
  exceptions, so one exotic property does not over-tighten or poison the whole
  schema.
- `PublicApiSetup` registers `RequiredFromNrtFilter` inside `AddSwaggerGen`.
- `AdminAuditLogResponse.cs` is opted into `#nullable enable`; NotNull
  strings are initialized, `Entries` is initialized to `[]`, and `Context`
  remains `string?`.
- The generated OpenAPI schema marks audit-log NotNull properties as
  required, keeps `context` nullable, and the generated TS aliases have
  required `id`, `unixMs`, `username`, `action`, `targetId`, `totalMatched`,
  and `entries`.
- `types/admin.ts` re-exports only the S3 `AdminAuditLog*` DTOs from
  `api.generated.ts`; the wider hand-mirrored DTO sweep remains the documented
  wave-A4 residual.
- No `JsonPropertyName` override was found in the public DTO paths audited
  here, so the current case-insensitive CLR-member resolution does not miss an
  in-scope renamed DTO member.

## Validation Evidence

- `git diff --check cef3663..a36e2a9`: passed.
- `dotnet build Dxs.Consigliere.sln -c Release`: passed, 0 warnings, 0
  errors.
- `dotnet test Dxs.Consigliere.sln -c Release --filter Swagger.RequiredFromNrtFilterTests`:
  passed 5/5.
- `pnpm verify`: passed (typecheck, lint, 195 unit tests, build, budget,
  inventory, contracts check).
- `RAVEN_URL=http://127.0.0.1:18080 pnpm test:contract`: passed 24/24.
- `bash scripts/secrets-lint.sh`: passed.
- `grep -E '"required":' src/admin-ui/contracts/swagger.json | wc -l`:
  `76`.

## Residual Risk

Manual browser smoke was not run. The code-level and contract evidence covers
the S6 cutover, but a browser pass through `/audit-log` would still be useful
after folding L1 and any pending S4 audit work.
