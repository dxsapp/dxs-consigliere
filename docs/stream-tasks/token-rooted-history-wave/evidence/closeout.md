# Token Rooted History Wave Closeout

## Outcome

Rooted token history wave is implemented.

The repo now enforces trusted-root semantics for token `full_history` and exposes rooted status/readiness in the tracked-history surface. The token historical backfill runner is no longer a stub: it works through `historical_token_scan`, hydrates trusted-root branches via Bitails, and keeps unknown-root branches out of canonical outward token state/history.

## Landed Areas

- rooted token request DTOs and bulk upgrade contract
- tracked token rooted security state persistence
- rooted full-history boundary enforcement in admin endpoints
- rooted token backfill scheduling and runtime worker
- rooted outward read filtering in token state, balances, utxos, and history
- readiness/history responses with rooted token security payload
- focused verification wave for registration, readiness, runner, and outward reads

## Validation Summary

- Focused rooted token wave: passed (`23/23`)
- `Dxs.Consigliere.Benchmarks` Release build: passed
- Additional broader sanity pass found one unrelated existing watch item in address history paging tests.

## Commits

- docs package base: `931920d`
- implementation wave: current implementation commit
