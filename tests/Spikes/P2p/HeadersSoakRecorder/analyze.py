#!/usr/bin/env python3
"""Wave 1 S7 — offline JSONL analyzer for HeadersSoakRecorder.

Reads the recorder's JSONL output, joins p2p and woc records by
tip_hash, and computes the metrics specified in
docs/stream-tasks/bsv-headers-chain-wave/slices.md §S7
"Reproducibility rules".

Usage:
    python3 analyze.py <path-to.jsonl> [<output-md-path>]

Exit codes (audit A2 H2 alignment with §S7):
    0 = PASS    — p95_lag_ms <= 2000 AND missed_ratio < 0.05
                   AND joined_blocks >= 128 AND woc_uptime >= 0.90
    2 = INCONCLUSIVE — soak too short: joined_blocks < 128
                       (re-run for a longer window before fail/pass)
    1 = FAIL    — joined_blocks >= 128 but one of the other gates missed
"""
from __future__ import annotations

import json
import statistics
import sys
from collections import defaultdict
from pathlib import Path


def main() -> int:
    if len(sys.argv) < 2:
        print("usage: analyze.py <jsonl> [<out.md>]", file=sys.stderr)
        return 2
    jsonl = Path(sys.argv[1])
    out_md = Path(sys.argv[2]) if len(sys.argv) > 2 else None

    p2p = {}  # tip_hash -> earliest ts_utc_ms
    woc = {}  # tip_hash -> (ts_utc_ms, height)
    woc_height_changes = 0
    http_errors = 0
    decode_errors = 0
    total_records = 0

    for line in jsonl.read_text().splitlines():
        if not line.strip():
            continue
        try:
            rec = json.loads(line)
        except json.JSONDecodeError:
            decode_errors += 1
            continue
        total_records += 1
        t = rec.get("type")
        if t == "p2p":
            h = rec.get("tip_hash")
            ts = rec.get("ts_utc_ms")
            if h and isinstance(ts, int):
                p2p.setdefault(h, ts)
        elif t == "woc":
            h = rec.get("tip_hash")
            ts = rec.get("ts_utc_ms")
            height = rec.get("height")
            if h and isinstance(ts, int):
                woc[h] = (ts, height)
                woc_height_changes += 1
        elif t == "http_error":
            http_errors += 1
        elif t == "decode_error":
            decode_errors += 1

    joined = []
    missed = []
    JOIN_WINDOW_MS = 600_000  # 600s per §S7 join rule
    for hsh, (woc_ts, height) in woc.items():
        p2p_ts = p2p.get(hsh)
        if p2p_ts is None or (p2p_ts - woc_ts) > JOIN_WINDOW_MS:
            missed.append((hsh, height, woc_ts))
        else:
            # Negative lag clamps to zero for p95 calc (§S7 lag formula).
            raw_lag = p2p_ts - woc_ts
            joined.append({"tip_hash": hsh, "height": height,
                           "woc_ts": woc_ts, "p2p_ts": p2p_ts,
                           "raw_lag_ms": raw_lag,
                           "lag_ms": max(0, raw_lag)})

    n_joined = len(joined)
    n_missed = len(missed)
    n_woc = woc_height_changes
    missed_ratio = (n_missed / n_woc) if n_woc else 0.0

    if joined:
        lags = sorted(j["lag_ms"] for j in joined)
        p50 = quantile_linear(lags, 0.50)
        p95 = quantile_linear(lags, 0.95)
        p99 = quantile_linear(lags, 0.99)
    else:
        p50 = p95 = p99 = float("nan")

    # WoC uptime estimate: woc records per total records (excluding
    # explicit http_error). A coarse proxy; the pass gate uses
    # http_error density as a tolerance bound.
    uptime_proxy = 1.0 if (n_woc + http_errors) == 0 else (n_woc / (n_woc + http_errors))

    # Pass gate per §S7. Below 128 joined blocks the run is INCONCLUSIVE
    # (exit 2), not FAIL — audit A2 H2 split. Only with enough sample do
    # we judge the run pass/fail.
    if n_joined < 128:
        verdict = "INCONCLUSIVE"
        exit_code = 2
    else:
        passed = (p95 <= 2000 and missed_ratio < 0.05 and uptime_proxy >= 0.90)
        verdict = "PASS" if passed else "FAIL"
        exit_code = 0 if passed else 1

    report = format_report(
        jsonl=jsonl,
        total_records=total_records,
        n_woc=n_woc,
        n_joined=n_joined,
        n_missed=n_missed,
        missed_ratio=missed_ratio,
        p50=p50, p95=p95, p99=p99,
        http_errors=http_errors,
        decode_errors=decode_errors,
        uptime_proxy=uptime_proxy,
        verdict=verdict,
    )
    print(report)
    if out_md is not None:
        out_md.write_text(report + "\n")

    return exit_code


def quantile_linear(xs: list[int], q: float) -> float:
    if not xs:
        return float("nan")
    if len(xs) == 1:
        return float(xs[0])
    pos = q * (len(xs) - 1)
    lo = int(pos)
    hi = min(lo + 1, len(xs) - 1)
    frac = pos - lo
    return xs[lo] + frac * (xs[hi] - xs[lo])


def format_report(**kw) -> str:
    return (
        f"# Headers soak result — {kw['jsonl'].name}\n\n"
        f"- total records: {kw['total_records']}\n"
        f"- woc height changes: {kw['n_woc']}\n"
        f"- joined blocks: {kw['n_joined']}\n"
        f"- missed (no p2p match within 600s): {kw['n_missed']}\n"
        f"- missed ratio: {kw['missed_ratio']:.3f}\n"
        f"- lag p50 / p95 / p99 (ms): {kw['p50']:.0f} / {kw['p95']:.0f} / {kw['p99']:.0f}\n"
        f"- http errors: {kw['http_errors']}\n"
        f"- decode errors: {kw['decode_errors']}\n"
        f"- woc uptime proxy: {kw['uptime_proxy']:.3f}\n\n"
        f"## Verdict\n"
        f"{kw['verdict']} per slices.md §S7 pass condition "
        f"(p95 <= 2000 ms AND missed_ratio < 0.05 AND joined >= 128 AND uptime >= 0.90; "
        f"INCONCLUSIVE when joined < 128).\n"
    )


if __name__ == "__main__":
    sys.exit(main())
