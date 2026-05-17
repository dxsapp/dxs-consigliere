# HeadersSoakRecorder — Wave 1 S7 soak harness

Throwaway recorder for validating the headers-chain p95 lag claim
in `docs/stream-tasks/bsv-headers-chain-wave/slices.md` §S7. Not
shipped in production artifacts.

## What it does

Runs the BSV P2P peer pool (no Consigliere, no Raven) and, in
parallel, polls WhatsOnChain `/v1/bsv/main/chain/info` once per
second. For every new tip seen on either side it appends a JSONL
record. After SOAK_MINUTES the recorder exits; analyze the JSONL
offline with `analyze.fsx` to compute p50/p95/p99 lag.

## JSONL schema (slices.md §S7)

One JSON object per line:

```
{ "type":        "p2p"|"woc"|"http_error"|"decode_error",
  "ts_utc_ms":   <int64 UTC ms since epoch>,
  "height":      <int64|null>,
  "tip_hash":    <string|null>, // wire-order lowercase hex, no 0x
  "prev_hash":   <string|null>,
  "header_timestamp_ms": <int64|null>, // p2p only
  "source_seq":  <int64 monotonic per type>,
  "extra":       <object|null> }
```

`woc` records use **wire-order** hashes (byte-reverse of
WhatsOnChain's display order) so they join byte-equal against
`p2p` records.

## Reproducibility rules (slices.md §S7)

- **Clock.** Host must have NTP enabled before start (`chronyd` or
  `systemd-timesyncd`); offset < 50 ms.
- **Soak window.** ≥ 24h continuous (`SOAK_MINUTES=1440`). Minimum
  valid run is 128 joined blocks (≈ a day on mainnet; below 128 the
  run is reported as inconclusive rather than failed).
- **Join rule.** For each `woc` record with height H, find the
  `p2p` record with the matching `tip_hash`. If no match arrives
  within 600 s, count the block as missed.
- **Lag formula.** `lag_ms = p2p.ts_utc_ms - woc.ts_utc_ms`.
  Negative lag (P2P observed first) **counts as zero** for the p95
  calculation; raw values preserved in the JSONL for review.
- **Missing samples.** If WhatsOnChain polling drops below 90 %
  uptime (per `http_error` density), the run is invalid.
- **Quantile.** Linear-interpolation p95 (numpy `method='linear'`
  or F# Stat equivalent within 1 ms).
- **Pass condition.** `p95 ≤ 2000 ms` over ≥ 128 joined blocks;
  `missed < 5 %` of total `woc` height changes.

## Run

Local quick smoke (60 min):

```
SOAK_MINUTES=60 dotnet run -c Release \
  --project tests/Spikes/P2p/HeadersSoakRecorder
```

Full 24h soak on a VPS (recommended: same DigitalOcean droplet
class as `docs/platform-api/thin-node-gate2-soak-runbook.md`):

```
# verify NTP
chronyc tracking | grep -i offset
# run
SOAK_MINUTES=1440 OUTPUT_DIR=/var/log/headers-soak \
  dotnet run -c Release \
  --project tests/Spikes/P2p/HeadersSoakRecorder
```

Output: `headers-soak-<YYYYMMDD-HHMMSS>.jsonl` in `OUTPUT_DIR`.

## Analyze

`analyze.fsx` (alongside `Program.cs`) reads the JSONL, joins by
`tip_hash`, computes the metrics above, and writes
`evidence/headers-soak.md` per
`docs/stream-tasks/bsv-headers-chain-wave/`.

The analysis script intentionally lives in this folder rather than
the wave package — it is operational tooling for one run, not a
documented contract.

## Configuration

| Env var | Default | Notes |
|---|---|---|
| `SOAK_MINUTES` | `1440` (24h) | total runtime |
| `POOL_SIZE` | `8` | target peer pool size |
| `WOC_POLL_INTERVAL_SEC` | `1` | WhatsOnChain poll cadence |
| `OUTPUT_DIR` | `.` | JSONL output directory |
| `ENABLE_FALLBACK` | `true` | use hardcoded fallback peers if DNS fails |

## What this does NOT cover

- Reorg detection — that's W3.
- Per-source metrics — W4.
- Production telemetry / scoring — W6.
- Bitails / JungleBus realtime parity — already covered by
  existing Gate-2 soak runbook.

If a run fails the pass condition, the next debugging step is
inspecting the JSONL for late-arriving p2p records vs early woc
records, not modifying this recorder.
