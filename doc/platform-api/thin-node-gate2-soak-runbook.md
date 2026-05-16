# Gate 2 Soak Runbook — BSV Thin-Node P2P

Owner: Consigliere runtime
Last updated: 2026-05-14
Gate doc: [consigliere-thin-node-design.md §3.2, §14 Gate 2](./consigliere-thin-node-design.md)

This runbook describes the 24-hour soak test that gates promotion of the
BSV thin-node design from `Draft` → `Approved`. The test exercises the
peer pool against the live BSV mainnet with no transactions actually
broadcast — that's Gate 3's job.

## Prerequisites

1. **Fresh VPS.** Public IPv4, no prior BSV traffic, not on operator
   banlists. Hetzner / DO / Linode / Vultr all work. Memory: 512 MB is
   plenty.
2. **.NET 9 runtime** installed.
3. **Consigliere build** with this design committed. Tag:
   `gate2-soak-<date>` recommended.
4. **`Consigliere:Broadcast:P2p:Enabled = true`** in the runtime config.
   All other knobs at default unless you have a reason.

## What to run

```bash
# On the VPS, in the deployed Consigliere working directory:
ASPNETCORE_ENVIRONMENT=Production dotnet Dxs.Consigliere.dll
```

The startup log line to look for:

```
BSV P2P thin-node started: target pool size 8, UA /ConsigliereThinNode:0.1.0/
```

Within 1–2 minutes you should see at least one:

```
Peer connected: 1.2.3.4:8333 ua=/Bitcoin SV:1.2.1/ ver=70016
```

## Diagnostic endpoints

Authenticate with the admin policy and hit:

| Endpoint | Returns |
|---|---|
| `GET /api/admin/p2p/health` | `{ Bound, PoolSize, TargetPoolSize, Subnet24Diversity, ActivePeers[] }` |
| `GET /api/admin/p2p/peers` | full peer roster: every IP we've seen with success/fail counts, last connect, ban state |

Sample one-liners for continuous monitoring:

```bash
# Pool size over time
while true; do
  curl -s -u admin:$ADMIN_PASS https://your-host/api/admin/p2p/health \
    | jq -r '"[\(now|todate)] pool=\(.poolSize)/\(.targetPoolSize) subnets=\(.subnet24Diversity)"';
  sleep 60;
done | tee soak.log

# End-of-soak peer breakdown
curl -s -u admin:$ADMIN_PASS https://your-host/api/admin/p2p/peers \
  | jq '{total, successful, failed, distinctSubnets}'
```

## Acceptance thresholds (design §3.2)

Run for at least **24 hours**. PASS if all thresholds hold for the full
window:

| Metric | PASS | INVESTIGATE | FAIL |
|---|---|---|---|
| `poolSize` p50 over 24h | ≥ 8 | 4–7 | < 4 |
| Cold-start handshake acceptance (first hour) | ≥ 30% | 10–30% | < 10% |
| `subnet24Diversity` p50 | ≥ 3 | 2 | ≤ 1 |
| Session uptime per peer (median) | ≥ 4h | 1–4h | < 1h |

If any metric is in `INVESTIGATE`, look at:
- Peers in `peers` roster with high `failCount` and `lastFailureReason ==
  "PeerClosed"`: likely UA-banlist or accumulated-misbehavior bans.
- Peers with all attempts in same `/24` subnet: cold-start chose
  poorly-diverse seeds.

If any metric is in `FAIL`, **stop** and re-read [design §15.2 risk #1](./consigliere-thin-node-design.md).
Options: whitelist negotiation, embedded bitcoind sidecar.

## What's not tested in Gate 2

- Outgoing tx broadcast: there is no `BroadcastTracked` API yet. That's
  Gate 3.
- `TxRelayCoordinator`, `OutgoingTransactionStore`,
  `OutgoingTransactionMonitor`: not wired. Gate 3.
- Inbound peer listening: disabled. Gate 4.
- Admin UI surfaces: Gate 4.

Gate 2 just answers: **"can we sustain a real peer pool from a fresh VPS
under the corrected protocol from Gate 1?"**

## Reporting back

When the soak finishes, copy the following into the design doc as
evidence behind §3.2:

1. Peer acceptance rate distribution (% of attempts that completed
   handshake), bucketed hourly.
2. Final peer roster from `/api/admin/p2p/peers` — `total`, `successful`,
   `failed`, `distinctSubnets`, top-10 stable peers.
3. Notable failure reasons (`lastFailureReason` counts).
4. Pool-size timeline (`soak.log`).

Move design status `Draft` → `Approved for Gate 3 implementation` only
after this evidence is attached.

## Quick teardown

```bash
# Stop the process
pkill -f Dxs.Consigliere

# Wipe peer cache (in-memory only in Gate 2 — restart is enough)
```
