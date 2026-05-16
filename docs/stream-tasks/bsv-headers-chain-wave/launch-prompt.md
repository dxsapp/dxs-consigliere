# Launch — Wave 1: BSV Headers Chain + Contract Freeze

Revised 2026-05-17 per wave audit A1.

## Mission

Land the BSV header-chain tracker inside Consigliere and, in the same
wave, **freeze every program-wide hub event, server method,
`PeerSession` callback, send-helper, and `PeerTelemetry` field** that
downstream waves (W2-W6) will consume — so no later wave needs to
touch `PeerSession.cs` or `IWalletHub.cs` to add new surfaces.

End state at wave close:

- Mainnet chain tip and trailing ≤200 headers tracked purely from BSV
  P2P (`headers` / `inv(MSG_BLOCK)` / `getheaders`).
- `BlockHeaderDocument` rows in RavenDB; `BlockHeaderStore` API
  available to downstream waves.
- `IWalletHub.OnNewBlock(BlockTipDto)` live; `OnReorg(ReorgEventDto)`
  stubbed (body in W3).
- `PeerSession` callbacks `OnHeadersReceived`,
  `OnInvReceived(InvMessage)`, `OnRejectReceived` frozen with
  **additive dispatch** (existing `IncomingMessages` channel still
  delivers those frames).
- `PeerSession.SendGetHeadersAsync(GetHeadersMessage, CancellationToken)`
  send helper added.
- `PeerTelemetry` record + `IPeerTelemetrySink` rich-event interface
  frozen with `NullPeerTelemetrySink` as the per-session default;
  W6 swaps the construction site (inside `PeerManager`, which W6
  owns) without re-opening this wave. No registry indirection.
- `TxRelayCoordinator` gets a small telemetry hook — direct
  `session.Telemetry.<...>` calls on the session that emitted the
  frame — so served/requested getdata and relay-back invs are
  recorded against the same sink W6 will consume.
- `BroadcastReceiptDto` shape frozen (comment + manifest). Server
  methods `IWalletServer.Broadcast` / `BroadcastTracked` are
  **explicitly not frozen**; W5 may collapse them.
- Admin endpoints `GET /api/admin/p2p/headers/tip` and
  `GET /api/admin/p2p/headers/recent`.
- 24 h `HeadersSoakRecorder` evidence with measured p95 lag ≤ 2 s,
  produced per the JSONL schema and reproducibility rules in
  `slices.md` §S7.
- Manifest approval test (`ContractFreezeApprovalTests`) and
  additive-dispatch regression (`PeerSessionAdditiveDispatchTests`)
  green.

## Package path

`docs/stream-tasks/bsv-headers-chain-wave/`

Sources of truth:

- `master.md` — wave-level scope, rules, ownership, handoff table,
  slice ledger
- `slices.md` — slice-level decomposition + dependency graph + JSONL
  schema for the soak harness
- `audits/wave1-audit-A1.md` — Codex pre-execution audit (MAJOR
  REVISION REQUIRED → folded into this revision)

Parent program: `docs/stream-tasks/consigliere-thin-node-observer-program/`.

## Prerequisite-slice gating (mandatory)

This wave has a **prerequisite slice** per the program launch prompt:

- **S0 — Program-Wide Contract Freeze** has `depends_on = —` (none).
- **S1-S7** all have `depends_on` that includes S0 explicitly:
  - S1: `S0`
  - S2: `S0`
  - S3: `S0, S1, S2`
  - S4: `S0, S1, S2, S3`
  - S5: `S0, S3`
  - S6: `S0, S3`
  - S7: `S0, S5, S6`

S0 lands first, on its own, with a slice-level audit at
`audits/S0-A1.md`. **No main slice (S1-S7) opens until S0's slice
audit returns APPROVE.** This is the audit-gate-per-slice the program
launch prompt requires for Waves 1 and 2.

## Constraints (frozen)

- **No new hub events, server methods, `PeerSession` callbacks /
  send helpers, or telemetry fields outside S0.** Amendments require
  an explicit contract-freeze amendment slice in this wave's package.
- **Additive dispatch invariant.** New callbacks do **not** remove
  frames from `IncomingMessages`. Existing consumers
  (`TxRelayCoordinator` and friends) keep working unchanged. A
  regression test in S0 enforces this.
- **No reorg recovery in W1.** Competing tips are stored side-by-side
  by S3 without any rebuild logic; W3 owns recovery.
- **No block-body fetch in W1.** Headers only.
- **No watchlist or mempool observation in W1.** That's W2.
- **No source metrics, broadcast unification, peer scoring,
  alerts.** Those are W4 / W5 / W6.
- **`OnReorg` body stays a no-op until W3.** Subscription works,
  group exists, but no message is ever broadcast in W1.
- **`IWalletServer.Broadcast` / `BroadcastTracked` server method
  signatures are NOT frozen.** W5 is authorised to collapse them.
  Only `BroadcastReceiptDto` is frozen in W1.
- **PoW header validation enforced** — every accepted header has its
  double-SHA-256 recomputed against the encoded target.
- **Stop-and-audit per slice on S0, per wave on S1-S7.**
- **Manifest approval test, not reflection-existence smoke test.**

## Required execution order

1. **Wave-level audit** of this package via Codex
   (`audits/wave1-audit-A1.md` done; produced MAJOR REVISION
   REQUIRED, addressed in this revision). A follow-up audit on this
   revised package may be requested before opening S0; it would land
   as `wave1-audit-A1-followup.md`. Wave-execution audit lands as
   `wave1-audit-A2.md` after S1-S7 close.
2. **Open S0** (contract freeze). Implement, commit, run S0
   slice-level audit (`audits/S0-A1.md`).
3. **Only after S0 audit APPROVEs**, open the main slices per the
   dependency edges above. Default operator preference is strict
   sequential S0 → S1 → S2 → S3 → S4 → S5 → S6 → S7. Parallelism is
   allowed only with per-slice audit gates.
4. **Soak (S7)** runs ≥ 24 h on a fresh VPS per `slices.md` §S7;
   results recorded in `evidence/headers-soak.md`.
5. **Wave closeout.** Write `evidence/closeout.md`, mark wave `done`
   in this `master.md` and in the parent program `master.md` ledger.
6. **Stop.** Generate the Wave 2 audit prompt; wait for the user's
   Codex response before opening `bsv-mempool-observer-wave`.

## Validation

Per-slice validation lives in `slices.md`. Wave-level:

- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- `dotnet test` returns no new failures vs the pre-W1 baseline
  (baseline counts recorded at S0 start; delta counted as residual).
- `ContractFreezeApprovalTests` green — reflected surface
  byte-equal to `manifest.json`, with manifest sections for:
  - `IWalletHub` client callbacks (must include `OnNewBlock`,
    `OnReorg`; must NOT contain `SubscribeTo*`)
  - `IWalletServer` server methods (must include
    `SubscribeToBlockTip`, `SubscribeToReorg`; must NOT contain
    `OnNewBlock` / `OnReorg`)
  - frozen `PeerSession` members (including `SendGetHeadersAsync`,
    `Telemetry`, and the three new callbacks)
  - `PeerTelemetry`, `IPeerTelemetrySink`
  - `BlockTipDto`, `ReorgEventDto`, `BroadcastReceiptDto`.
- `PeerSessionAdditiveDispatchTests` green — callback fires AND
  `IncomingMessages` still receives `inv` / `reject` / `headers`.
- Grep proves no `OnBlockInvReceived` or `OnInvReceived(tx)`
  patterns; `SendGetHeadersAsync` is present in `PeerSession.cs`.
- `GET /api/admin/p2p/headers/tip` matches WhatsOnChain
  `chain/info` at validation time.
- `evidence/headers-soak.md` records 24 h soak per the JSONL schema
  with measured p95 lag ≤ 2 s and missed-block ratio < 5 % over
  ≥ 128 joined blocks.

## Closeout

- `audits/S0-A1.md` — slice-level audit on the contract freeze.
- `audits/wave1-audit-A1.md` — pre-execution wave audit (this
  revision is the response to it).
- `audits/wave1-audit-A2.md` — wave-level audit after all slices
  close.
- `evidence/headers-soak.md` — soak-recorder output and p95 analysis.
- `evidence/closeout.md` — end-state metrics, delivery hashes per
  slice, residuals, handoff facts for W2/W3/W4/W5/W6 (cross-reference
  the handoff table in `master.md`).
- Parent program `master.md` Delivery Notes gets the Wave 1 closeout
  commit hash; program ledger row for Wave 1 transitions to `done`.

## Commit / report expectations

- **Docs-only commit** when this package is created / revised:
  `docs(p2p): add bsv-headers-chain-wave package` for creation;
  `docs(p2p): revise wave1 package per audit A1` for this revision.
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
