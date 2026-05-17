# Backlog — post-release ideas

Things we want to do **after** the current release ships
(consigliere-thin-node-observer-program). One-line per item; expand
into a `docs/stream-tasks/<slug>/` package when an item is ready
to plan.

## Multi-chain thin-node (hype play)

**What:** Generalise `tools/BsvBroadcastNode/` (and the P2P layer
under `src/Dxs.Bsv/P2p/`) into a `BitcoinThinNode` that can talk
to **any** Bitcoin-fork chain — BSV, BCH, BTC — via a single binary
parametrised by `--network=bsv|bch|btc`.

**Why:** The Bitcoin P2P wire protocol is essentially the same
across the three forks; we already implement it. Difference is
config: magic byte, DNS seeds, default port, user-agent, service
flags, and one BSV-specific extension (`protoconf` after `verack`).
BSV and BCH even share the same magic byte (`e3 e1 f3 e8`) since
the chains shared history through 2018. BTC needs the magic change
(`f9 be b4 d9`).

**Scope:**
- Parametrise `P2pNetwork` (currently only `Mainnet` = BSV) — add
  `Bsv`, `Bch`, `Btc` variants with their own magic / seed list /
  default port.
- Make `PeerDiscovery` consume the chain-specific DNS seed list.
- Make UA + service flags + `SendProtoconfAfterVerack` come from
  config rather than hardcoded BSV values.
- Rename `tools/BsvBroadcastNode/` → something like
  `tools/BitcoinThinBroadcastNode/` with a `--network` CLI flag.
- Verification spike: real handshake against a known BTC node and
  a known BCH node, prove our codec round-trips on both magics.
- Optionally: broadcast a test tx into BCH testnet.

**Out of scope for this backlog item:**
- STAS/DSTAS token parsing (BSV-only — lives in
  `src/Dxs.Bsv/Script/`, not P2P layer).
- Consigliere business logic (watchlist, lifecycle projection).
  Multi-chain support is at the thin-node + broadcaster layer
  only; Consigliere itself stays BSV.

**Estimated effort:** small. The hard work (codec, session,
peer pool, broadcast lifecycle) is already done and chain-agnostic
at the wire level. Most of the work is config plumbing + a 1-day
spike to prove BTC/BCH handshakes.

**Suggested trigger:** after the consigliere-thin-node-observer-program
ships and is operating cleanly on mainnet BSV.

---

*Add new entries below in the same shape: What / Why / Scope / Out of
scope / Estimated effort / Suggested trigger.*
