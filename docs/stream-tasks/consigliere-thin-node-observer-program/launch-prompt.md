# Launch — Consigliere Thin-Node Observer Program

## Mission

Grow the existing P2P broadcaster (already shipped through Gate 1–3) into
a full bidirectional BSV thin-node engine inside Consigliere — with
observed-tx ingestion via P2P, header-chain tracking, reorg handling,
per-source metrics, a unified broadcast surface, and a production-ops
hardening layer — **without breaking the public REST/SignalR API,
except for the explicitly-versioned `Broadcast` method contract change
documented in Wave 6 change notes.**

## Package path

`docs/stream-tasks/consigliere-thin-node-observer-program/`

Sources of truth:

- `master.md` — program-level scope, rules, ownership zones, ledger
- `slices.md` — wave decomposition + cross-wave dependencies

This program package stays open across all six child waves. Update
`master.md` Delivery Notes and the Program Ledger as waves close.

## Constraints (frozen, revised post-audit A1)

- This is **vnext** — no backwards-compat with legacy broadcast or
  ingestion paths required. `master.md` Core Rules §6 explicitly removes
  HTTP-provider broadcast paths in Wave 5.
- **Stop-and-audit per wave.** Each wave gets its own Codex audit prompt
  before execution, and `audits/A1.md` after closeout. No wave leaves
  `done` without an audit pass.
- **No Postgres migration in this program.** Stay on RavenDB.
- **No public-API breaking changes outside the single explicitly-versioned
  `Broadcast` method contract change.** REST `api/address/*`,
  `api/token/*`, `api/tx/*` and all `WalletHub.Subscribe*` methods stay
  byte-compatible.
- **Source-agnostic persistence rule.** New observer code must reuse
  the existing `TxObservationJournalWriter` (after Wave 2 extends it
  with `TxObservationSource.P2p` and a source-neutral overload) and
  contribute `SeenBySources` tags — not invent a parallel store.
- **HashSet-based watchlist matching.** No bloom filter in this program.
  Target ≤ 500 K addresses (soft cap; ceiling tested in Wave 2 scale
  slice).
- **Reorg uses existing `Reorged` lifecycle state.** Wave 3 emits
  `BlockDisconnected` observations so the existing
  `TxLifecycleProjectionRebuilder` handles the state transition; no new
  lifecycle state is invented.
- **Hub & PeerSession contract freeze in Wave 1.** Wave 1's first slice
  pre-declares all hub events / subscription groups / `PeerSession`
  observation extension points downstream waves need.
- **Bitails and JungleBus realtime stay alive** under config control;
  per-source metrics will show whether either is still pulling weight.

## Required execution order

1. **Program-level audit** of `master.md` + `slices.md` via Codex.
   (A1 done — `audits/program-audit-A1.md`. If a follow-up audit is
   requested after this revision, it lands as `program-audit-A2.md`.)
2. **Revise** the program package per audit findings; commit docs-only.
   (Done — this commit.)
3. **Wave 1** — open `docs/stream-tasks/bsv-headers-chain-wave/`,
   audit its own files via Codex, execute, close out.
4. **Wave 2** — open `docs/stream-tasks/bsv-mempool-observer-wave/`,
   audit, execute, close out.
5. **Wave 3** — open `docs/stream-tasks/reorg-handling-wave/`, audit,
   execute, close out.
6. **Wave 4** — open `docs/stream-tasks/observation-source-metrics-wave/`,
   audit, execute, close out.
7. **Wave 5** — open `docs/stream-tasks/broadcast-unification-wave/`,
   audit, execute, close out.
8. **Wave 6** — open `docs/stream-tasks/production-ops-wave/`, audit,
   execute, close out.
9. **Program closeout** — write `evidence/closeout.md`, mark program
   `done` in `master.md`.

Wave order is sequential by default. The dependency diagram in
`slices.md` shows what could theoretically run in parallel; the user
has chosen strict sequencing with stop-and-audit gates.

## Current target wave

**Wave 1 — `bsv-headers-chain-wave`** (after this revision is audited
or the user approves direct Wave 1 entry).

## Stop conditions between waves

After each wave closes:

- pause execution
- generate the next wave's audit prompt
- wait for the user's Codex audit response
- only then open the next wave's package

This is the default `next-wave-first` mode per the
`durable-wave-package` skill. Do not chain wave executions silently.

## Validation

Per-wave validation lives in each wave's `master.md` and `slices.md`.

Program-level validation (revised per A1):

- All six wave ledgers complete (or `not_opened` with rationale)
- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors
- `dotnet test` returns no new failures (delta against baseline counted
  as residual)
- A real BSV mainnet transaction survives the full lifecycle
  Submitted → PeerAcked → MempoolSeen → Mined → Confirmed with hub
  events flowing
- Admin panel shows healthy P2P pool (with peer rotation evidence from
  W6), recent headers tip, mempool observation rate, per-source
  metrics, active alerts (if any)
- Public-API change notes for the `Broadcast` contract are published in
  `docs/platform-api/` (W6 deliverable)

## Closeout

- Each child wave: `audits/A1.md`, `evidence/closeout.md`.
- Program: `audits/` accumulates program-level audits (currently
  `program-audit-A1.md`); `evidence/closeout.md` summarises end-state,
  residuals, delivery hashes for the whole program.
- Program `master.md` Delivery Notes lists commit hashes per wave +
  program closeout commit.

## Commit / report expectations

- **Docs-only commit** when this program package or any child wave
  package is created/revised (`docs(p2p): add <wave-name>` style).
- **Implementation commits** during wave execution: prefer one commit
  per closed slice; final wave closeout commit may bundle minor
  follow-ups.
- **Wave closeout commit** records commit hashes inside the program
  ledger and the wave's own `master.md`.
- Final program report goes in `evidence/closeout.md` — result first,
  waves closed, validation passes, honest residuals.
