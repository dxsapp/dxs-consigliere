# Token Rooted History Model

## Purpose

This document defines the security model for tracked token history in `Consigliere`.

Related terminology is defined in:
- `doc/platform-api/root-semantics-glossary.md`

It exists because token history has a stronger attack surface than address history:
- token history cannot trust arbitrary discovered ancestry
- external providers must not become the canonical source of token truth
- unknown roots must not be expanded into canonical token history automatically

## Core Rule

Token `full_history` means:
- full history inside the managed scope
- only for branches that provably descend from explicitly trusted roots

This is a rooted-trust model, not a generic token explorer model.

## Trusted Roots

Tracked token full-history requires explicit trusted roots.

Control-plane rule:
- token `full_history` is invalid without `trustedRoots[]`

Trusted roots are the only allowed starting points for:
- lineage walk
- trusted branch expansion
- token historical backfill completion

## Unknown Root Rule

If token historical sync encounters a branch that resolves to a root outside the trusted set:
- the branch must not be auto-expanded
- the branch must not be promoted into canonical token history
- the branch must be recorded as an unknown-root finding

Default policy:
- `reject_branch`

## Canonical Token History

Canonical token history includes only:
- branches attached to the explicit trusted root set
- history facts discovered inside the managed scope of those trusted branches
- validation-critical lineage required to prove those branches

Canonical token history explicitly excludes:
- unknown-root branches
- provider-discovered token branches that were not rooted in the trusted set
- silent heuristic promotion of new roots into managed scope

## Managed-Scope Completion Rule

Tracked token history becomes `full_history_live` only when:
- all trusted roots are known and accepted
- lineage-critical ancestry for those roots is complete
- managed-scope historical expansion for those roots is complete
- no blocking unknown-root frontier remains

If unknown-root findings remain and they can affect authoritative token truth:
- token history must not become `full_history_live`

## Public Control-Plane Contract

Token history registration and upgrade use:
- `historyPolicy.mode`
- `tokenHistoryPolicy.trustedRoots[]`

Minimum token full-history contract:
- `historyPolicy.mode = full_history`
- `tokenHistoryPolicy.trustedRoots[]` must be non-empty

## Status And Coverage Semantics

Token history status must expose enough information to explain rooted completion.

Recommended status additions:
- `trustedRootCount`
- `completedTrustedRootCount`
- `unknownRootFindingCount`
- `rootedHistorySecure`
- `blockingUnknownRoot`

These fields explain why a token is or is not `full_history_live`.

## Historical Scan Rule

`historical_token_scan` is allowed only inside the trusted-root universe.

That means:
- rooted planner work starts from `trustedRoots[]`
- address-driven historical expansion is allowed only for already-trusted branches
- historical observations still flow into the same canonical journal used by live ingest

## Query Semantics

Token history query behavior follows the main history model:
- authoritative history requires normal history readiness
- partial history requires `acceptPartialHistory = true`

Additional rooted rule:
- partial token history may be returned only from the trusted-root universe
- unknown-root branches must not appear as canonical token history pages

## Upgrade Semantics

Supported:
- registration with `forward_only`
- registration with rooted `full_history`
- upgrade `forward_only -> full_history` with trusted roots

Not supported:
- `full_history -> forward_only`
- `full_history` without trusted roots
- silent auto-acceptance of newly discovered roots

## Security Goal

The design goal is not to know every token-related transaction in the whole network.

The goal is to make authoritative token history defensible:
- explicit trusted roots define the canonical universe
- unknown roots are visible findings, not silent canonical inputs
- token state and history stay honest under adversarial or polluted external history sources
