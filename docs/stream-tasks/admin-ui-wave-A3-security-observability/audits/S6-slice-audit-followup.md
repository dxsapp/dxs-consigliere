---
created: 2026-05-20
type: slice-audit-followup
parent: docs/stream-tasks/admin-ui-wave-A3-security-observability/audits/S6-slice-audit-prompt.md
status: applied
---

# wave-A3 S6 slice-audit followup (APPROVE WITH CHANGES)

Codex slice-audit on `a36e2a9`: APPROVE WITH CHANGES
(0C / 0H / 0M / 1L). Single finding folded.

## L1 — Tests pinned only the `required` half of the filter contract

**Verified:** the production filter
(`RequiredFromNrtFilter.cs:47-60`) implements BOTH halves
of the S6 contract: NotNull CLR members are added to the
OpenAPI `required` array AND their schema gets `Nullable =
false`. The `Nullable = false` half is what
`openapi-typescript` keys on to drop the `| null` union
from the generated TS. The original test suite only
asserted membership in `schema.Required`. A future edit
that dropped the `openApiProperty.Nullable = false`
assignment would have left all 5 tests green while
regressing the slice's contract-tightening — generated
admin-ui types would drift back toward
`field: T | null` / `field?: T | null` for NotNull DTO
members.

**Revision applied:**

- Test fixtures now seed every OpenAPI property with
  `Nullable = true`. The assertion that the filter flips
  it to `false` for NotNull CLR members can no longer be
  satisfied by accident — the seed value is the explicit
  pre-condition.
- Two test methods renamed to
  `Adds_NotNull_reference_property_to_required_AND_flips_nullable_false`
  +
  `Adds_value_type_property_to_required_AND_flips_nullable_false`;
  each now asserts BOTH `Required` membership and the
  `Nullable` flip on the matching property, AND asserts the
  nullable sibling property's `Nullable: true` survives
  unchanged.
- `Preserves_existing_required_entries` renamed to
  `_without_forcing_nullable_to_false_for_nullable_members`
  and extended: a CLR-nullable property pre-marked
  `required` (e.g. via `[Required]` on a nullable field)
  must stay in `required` but its `Nullable: true` MUST
  survive — the property is intrinsically nullable; over-
  tightening would generate wrong TS.
- `Ignores_properties_with_no_clr_match` extended: the
  unmatched property's `Nullable: true` survives — the
  filter cannot tighten what it can't reason about.
- 5 tests total, same headline coverage, every case now
  pins both halves of the filter contract.

## Validation

- `dotnet build -c Release` clean on the full solution.
- `dotnet test --filter Swagger.RequiredFromNrtFilterTests`:
  5/5 green.
- `pnpm test:contract`: 24/24 green.
- `pnpm verify`: green (regenerated swagger + api.generated
  unchanged).
- `bash scripts/secrets-lint.sh` exits 0.

## Residual

Manual browser smoke through `/audit-log` after the S3 +
S4 + S6 folds remains the standing wave-A3 residual; the
backend + ajv contract gate covers the wire shape, and the
S3-audit L1 mock-mode wiring covers the UX flow without
needing a live Raven.

The wider hand-mirrored-interface sweep across the
remaining `Dto.cs` files is the standing wave-A4 residual
that S6 itself called out — not part of this fold.

## Result

1/1 finding folded. The NRT filter's nullable-flag flip is
now pinned by the same test methods that pin `required`
membership, closing the audit's "silent regression" risk.
