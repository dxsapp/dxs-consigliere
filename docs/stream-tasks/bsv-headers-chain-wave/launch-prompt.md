# Launch — Wave 1: BSV Headers Chain + Contract Freeze

## Mission

Land the BSV header-chain tracker inside Consigliere and, in the same
wave, **freeze every program-wide hub event, `PeerSession` extension
point, and `PeerTelemetry` field** that downstream waves (W2-W6) will
consume — so no later wave needs to touch `PeerSession.cs` or
`IWalletHub.cs` to add new surfaces.

End state at wave close:

- Mainnet chain tip and trailing ≤200 headers tracked purely from BSV
  P2P (`headers` / `inv(MSG_BLOCK)` / `getheaders`).
- `BlockHeaderDocument` rows in RavenDB; `BlockHeaderStore` API
  available to downstream waves.
- `IWalletHub.OnNewBlock(BlockTipDto)` live; `OnReorg(ReorgEventDto)`
  stubbed (body in W3).
- `PeerSession` callbacks `OnHeadersReceived`,
  `OnInvReceived(InvMessage)`, `OnRejectReceived` frozen with bodies
  dispatching to consumer-provided delegates.
- `PeerTelemetry` record + `IPeerTelemetrySink` interface frozen with
  `NullPeerTelemetrySink` registered by default; W6 swaps in the
  production sink without re-opening this wave.
- Admin endpoints `GET /api/admin/p2p/headers/tip` and
  `GET /api/admin/p2p/headers/recent`.
- 24 h `HeadersSoakRecorder` evidence with measured p95 lag ≤ 2 s.

## Package path

`docs/stream-tasks/bsv-headers-chain-wave/`

Sources of truth:

- `master.md` — wave-level scope, rules, ownership, slice ledger
- `slices.md` — slice-level decomposition + dependency graph

Parent program: `docs/stream-tasks/consigliere-thin-node-observer-program/`.

## Prerequisite-slice gating (mandatory)

This wave has a **prerequisite slice** per the program launch
prompt:

- **S0 — Program-Wide Contract Freeze** has `depends_on = —` (none).
- **S1-S7** all have `depends_on = S0`.

S0 lands first, on its own, with a slice-level audit at
`audits/S0-A1.md`. **No main slice (S1-S7) opens until S0's slice
audit returns APPROVE.** This is the audit-gate-per-slice the program
launch prompt requires for Waves 1 and 2.

## Constraints (frozen)

- **No new hub events or `PeerSession` callbacks outside S0.** If
  S1-S7 (or any downstream wave) need a surface S0 missed, open an
  explicit contract-freeze amendment slice in this wave's package
  before touching the frozen files.
- **No reorg recovery in W1.** Competing tips are stored side-by-side
  by S3 without any rebuild logic; W3 owns recovery.
- **No block-body fetch in W1.** Headers only.
- **No watchlist or mempool observation in W1.** That's W2.
- **No source metrics, broadcast unification, peer scoring,
  alerts.** Those are W4 / W5 / W6.
- **`OnReorg` body stays a no-op until W3.** Subscription works,
  group exists, but no message is ever broadcast in W1.
- **PoW header validation enforced** — every accepted header has its
  double-SHA-256 recomputed against the encoded target.
- **Stop-and-audit per slice on S0, per wave on S1-S7.**

## Required execution order

1. **Wave-level audit** of this package via Codex
   (`audits/A1.md` after wave closes; pre-execution audit prompt
   produced when the wave opens).
2. **Open S0** (contract freeze). Implement, commit, run S0
   slice-level audit (`audits/S0-A1.md`).
3. **Only after S0 audit APPROVEs**, open the main slices:
   - S1 (headers chain logic) and S2 (header doc + store) can run in
     parallel by ownership; default operator preference is strict
     sequential S1 → S2.
   - S3 (hosted service) depends on S1 + S2.
   - S4 (Bitails bootstrap) depends on S2 only — can run alongside
     S1/S3.
   - S5 (hub event live), S6 (admin API), S7 (soak spike) all
     depend on S3 and are mutually independent.
4. **Soak (S7)** runs ≥ 24 h on a fresh VPS; results recorded in
   `evidence/headers-soak.md`.
5. **Wave closeout.** Write `evidence/closeout.md`, mark wave `done`
   in this `master.md` and in the parent program `master.md` ledger.
6. **Stop.** Generate the Wave 2 audit prompt; wait for the user's
   Codex response before opening `bsv-mempool-observer-wave`.

## Validation

Per-slice validation lives in `slices.md`. Wave-level:

- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- `dotnet test` returns no new failures vs the pre-W1 baseline
  (baseline counts recorded at S0 start; delta counted as residual).
- Contract-freeze reflection test green (every declared member
  present on `IWalletHub`, `PeerSession`, `IPeerTelemetrySink`).
- Grep proves no `OnBlockInvReceived` or `OnInvReceived(tx)` leftover
  patterns (audit A2 N2 reconciliation).
- `GET /api/admin/p2p/headers/tip` matches WhatsOnChain `chain/info`
  at validation time.
- `evidence/headers-soak.md` records 24 h soak with measured p95
  lag ≤ 2 s vs WhatsOnChain.

## Closeout

- `audits/S0-A1.md` — slice-level audit on the contract freeze.
- `audits/A1.md` — wave-level audit after all slices close.
- `evidence/headers-soak.md` — soak-recorder output and p95 analysis.
- `evidence/closeout.md` — end-state metrics, delivery hashes per
  slice, residuals, handoff facts for W2/W3/W4/W5/W6.
- Parent program `master.md` Delivery Notes gets the Wave 1 closeout
  commit hash; program ledger row for Wave 1 transitions to `done`.

## Commit / report expectations

- **Docs-only commit** when this package is created / revised:
  `docs(p2p): add bsv-headers-chain-wave package`.
- **Implementation commits** during execution: one per closed slice
  is preferred. The S0 commit message must call out the frozen
  surface explicitly so reviewers can spot any later silent additions.
- **Wave closeout commit** records commit hashes inside this
  wave's `master.md` Delivery Notes and the parent program ledger.

## Stop conditions

- After S0 closes: pause execution, generate the S0 slice-level
  audit prompt, wait for the user's Codex audit response, only then
  open S1-S7.
- After the wave closes: pause execution, generate the Wave 2 audit
  prompt, wait for the user's Codex audit response, only then open
  `bsv-mempool-observer-wave`.

This is the default `next-wave-first` mode per the
`durable-wave-package` skill. Do not chain slices or waves silently.
