---
created: 2026-05-18
type: runbook
status: production
parent: consigliere-thin-node-observer-program (Wave 6)
related: thin-node-gate2-soak-runbook.md
---

# Consigliere Thin-Node — Production Operations Runbook

Operator-facing runbook for the BSV thin-node observer in
steady-state production. Wave 6 deliverable.

## Status of `thin-node-gate2-soak-runbook.md`

`thin-node-gate2-soak-runbook.md` (Wave 2) covers the 24-hour
soak procedure operators run BEFORE turning the thin-node loose
on real traffic — connect-rate sampling, subnet-/24 diversity
verification, fallback-seed exercise. It remains the
authoritative document for that gate.

This document is its production complement: it covers
steady-state operation AFTER Gate 2 passes. Read the gate-2
runbook before deploying; read this one once it's running.

## Admin surfaces

All routes live under `/api/admin/` and require the
`AdminAuthDefaults.Policy` policy.

| Route | Wave | Purpose |
|---|---|---|
| `GET /api/admin/p2p/health` | W6 (extended) | Pool size, subnet diversity, active peer keys, **inbound-listener decision** |
| `GET /api/admin/p2p/peers` | W2 | Every known peer + connect stats |
| `GET /api/admin/p2p/headers/tip` | W1 | Current headers-chain tip |
| `GET /api/admin/p2p/headers/recent?count=N` | W1 | Last N headers (height-desc) |
| `GET /api/admin/p2p/alerts?lastN=N&since=unixMs` | **W6 new** | Alert event log, newest-first |
| `GET /api/admin/metrics/sources?lastN=N` | W4 | Per-source observation/visibility metrics |

### Alert types

The W6 alert poller fires four operator-actionable rules
(`P2pAlertType` enum):

| Type | Trigger | Default threshold |
|---|---|---|
| `PoolSizeBelowThreshold` | `BsvP2pHealth.PoolSize < Alert.MinPoolSize` | 5 |
| `RelayBackRateBelowThreshold` | Per-tick Δ ratio of relay-back invs vs getdata requests across all peers | 0.30, suppressed when ΔgetData = 0 |
| `ReorgDepthExceeded` | `BsvP2pHealth.LastDegradedReorgAt` within `ReorgDepthWindowMs` | 5 min |
| `SourceFirstDropout` | A source (`p2p` / `bitails` / `junglebus`) had zero new `FirstSeen` over `SourceFirstDropoutWindowMs` AND another source did not | 1 hour |

All thresholds + the 60 s poll cadence + 720-event retention
are operator-tunable via the `Consigliere:Broadcast:P2p:Alert`
section in `appsettings.json`:

```json
{
  "Consigliere": {
    "Broadcast": {
      "P2p": {
        "Alert": {
          "Enabled": true,
          "AlertPollIntervalMs": 60000,
          "AlertRetentionEvents": 720,
          "MinPoolSize": 5,
          "MinRelayBackRate": 0.30,
          "ReorgDepthWindowMs": 300000,
          "SourceFirstDropoutWindowMs": 3600000
        }
      }
    }
  }
}
```

Alert events are append-only Raven documents
(`p2p/alerts/{unixMs:D14}`). The poller never updates an
existing document; the production repository refuses duplicate
ids (slice-audit M1 fix).

## Peer scoring + rotation

W6 introduces a per-peer score derived per tick from current
`PeerTelemetry`. Score formula:

```
latency_penalty  = clamp(PingRttP95Ms / 10.0, 0, 60)
reject_penalty   = clamp(5 * sum(RejectByClass), 0, 50)   // saturating
relay_back_bonus = min(RelayBackInvCount, 50)
score            = clamp(100 - latency_penalty - reject_penalty + relay_back_bonus, 0, 100)
```

`MinimumScoreToRetain` (default 30) is the eviction floor.
Each maintenance tick the `PeerRotationPlanner` evaluates the
lowest-scoring active peer; if its score is strictly below the
floor, that peer is evicted **at most one per tick** (Core
Rule §5: prevents flap loops). A pool whose worst peer is at
or above the floor is left alone.

Score is **never persisted** on `PeerRecord`. It is recomputed
each tick from live telemetry. Restarting the service starts
the rotation clean.

## Inbound P2P listener

`BsvP2pConfig.Inbound.Enabled` is a **config-only stub** in
the W6 release. Setting it `true` logs a warning at startup
and leaves the operator-visible flag
`BsvP2pHealth.InboundEnabled = true`, but no listener thread
spins. The flag exists so a future wave can land inbound
without a contract amendment.

Recommendation: leave `Inbound.Enabled = false` until the
inbound implementation ships.

## Recovery procedures

### Pool size collapses

1. Check `GET /api/admin/p2p/alerts?lastN=50` for
   `PoolSizeBelowThreshold` events — are they sustained or
   transient?
2. Check `GET /api/admin/p2p/health` — `PoolSize` /
   `TargetPoolSize` / `Subnet24Diversity`.
3. If sustained:
   - Inspect peer connect failures via
     `GET /api/admin/p2p/peers` (look for high `failCount` +
     `negativeUntil` future timestamps).
   - Restart the service to flush negative-cooldowns if
     necessary. The fallback-seed bootstrap path
     (`BsvP2pConfig.EnableFallbackSeeds = true`) gives a
     guaranteed reconnect path.
4. If the pool repeatedly drops on otherwise-healthy peers
   right after rotation, lower `MinimumScoreToRetain` (e.g.
   to 20) so fewer peers are evicted per tick. Default 30
   is the W6 baseline.

### Relay-back rate alerts

A sustained `RelayBackRateBelowThreshold` means broadcast
announcements are going out but the network is not echoing
them back via inv. Operator actions:

- Verify the broadcast subsystem (`Consigliere:Broadcast`) is
  enabled and the W5 unified `BroadcastService` is reaching
  peers.
- Inspect `GET /api/admin/p2p/peers` for `userAgent`
  diversity — a homogeneous pool of misbehaving peers can
  produce this.
- Tighten `Alert.MinRelayBackRate` (e.g. to 0.50) if your
  acceptable rate is higher; relax it (e.g. to 0.10) if your
  application tolerates lower throughput.

### Degraded reorg

`ReorgDepthExceeded` fires when the W3 reorg detector observes
a fork point below the retained header window — i.e. a reorg
deeper than the chain can resolve from cached headers.
Operator actions:

- Inspect the W3 reorg log via the consigliere log stream.
- Verify `BsvP2pHealth.LastDegradedReorgAt` (admin/health).
- If the alert persists across multiple ticks, manually
  rebootstrap the headers chain from a trusted seed.

### Source dropout

A `SourceFirstDropout` alert means one of the three
observation sources (`p2p`, `bitails`, `junglebus`) saw zero
new first-seen txs across the configured window while at
least one OTHER source kept moving. Operator actions:

- Check the W4 source metrics:
  `GET /api/admin/metrics/sources?lastN=10` — confirm the
  `FirstSeen` counters per source.
- If the dropped source is `p2p`, the pool may be stuck
  (cross-reference the `PoolSize` alert).
- If `bitails` or `junglebus`, check the corresponding
  ingest runner's logs for connection / API errors.

## Configuration reference

Full W6-touched configuration (defaults shown):

```json
{
  "Consigliere": {
    "Broadcast": {
      "P2p": {
        "Enabled": false,
        "Network": "mainnet",
        "PoolSize": 8,
        "Alert": {
          "Enabled": false,
          "AlertPollIntervalMs": 60000,
          "AlertRetentionEvents": 720,
          "MinPoolSize": 5,
          "MinRelayBackRate": 0.30,
          "ReorgDepthWindowMs": 300000,
          "SourceFirstDropoutWindowMs": 3600000
        },
        "Inbound": {
          "Enabled": false,
          "ListenPort": 8333
        }
      }
    }
  }
}
```

To activate the W6 alert subsystem in production: set
`Consigliere:Broadcast:P2p:Alert:Enabled = true` and tune
thresholds to your operating envelope. To activate peer
rotation: the wirer registers `IPeerScoringPolicy` +
`PeerRotationPolicy` at S7 (see `BsvP2pSetup`); no operator
action required beyond the existing `P2p:Enabled = true`.

## See also

- `thin-node-gate2-soak-runbook.md` — pre-deployment soak
  procedure (W2)
- `broadcast-w5-changeout.md` — wallet-side broadcast
  migration notes (W6)
- `consigliere-thin-node-design.md` — overall architecture
- `p2p-broadcaster-design.md` — broadcast subsystem reference
