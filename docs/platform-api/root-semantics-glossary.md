# Root Semantics Glossary

This glossary defines the canonical meaning of root-related terms used by `Consigliere`.

These terms are intentionally split into:
- protocol-validation semantics
- managed-scope trust semantics

They must not be collapsed into a single idea of "the correct root".

## `valid root`

A root issue transaction that is valid under `(D)STAS` lineage rules.

This means:
- ancestry resolves to a valid token issue
- the issue is recognized as a legal issue transaction
- the lineage does not contain protocol-illegal root ancestry

Short form:
- `valid root = protocol-valid origin issue`

## `trusted root`

A `valid root` that the operator explicitly approved as canonical for a tracked token.

This means:
- the root is protocol-valid
- the root appears in `trustedRoots[]`
- rooted token history may be expanded canonically from this root

Short form:
- `trusted root = operator-approved canonical valid root`

## `illegal root`

A root or lineage branch that makes token ancestry invalid under protocol rules.

This means:
- ancestry resolves to an invalid issue
- or lineage violates `(D)STAS` rules
- or the transaction cannot be proven as a legal continuation of token lineage

Short form:
- `illegal root = protocol-invalid root lineage`

## `unknown root`

A root that is not in the tracked token's trusted root set.

Important:
- an unknown root may still be protocol-valid
- unknown does not mean illegal
- unknown means `not trusted for this managed scope`

Short form:
- `unknown root = not yet trusted for this tracked token`

## `B2G resolved`

Back-to-Genesis lineage is sufficiently reconstructed to reach a root verdict.

This means:
- required ancestry is available
- lineage can be judged as rooted in a valid, illegal, or unknown root
- no critical missing dependency still blocks the verdict

Short form:
- `B2G resolved = lineage reconstructed enough to judge the root`

## `rooted history`

Token history that is canonical only inside the explicit trusted-root universe.

This means:
- canonical token history is anchored to `trustedRoots[]`
- unknown-root branches are visible as findings, not canonical facts
- rooted-history readiness is stricter than ordinary protocol legality

Short form:
- `rooted history = canonical token history bounded by trusted roots`

## Canonical Distinction

A token transaction can be:
- protocol-valid
- but still outside the canonical rooted-history universe

That is the key distinction:
- `valid root` answers whether lineage is legal under protocol rules
- `trusted root` answers whether lineage is accepted as canonical in the managed product model
