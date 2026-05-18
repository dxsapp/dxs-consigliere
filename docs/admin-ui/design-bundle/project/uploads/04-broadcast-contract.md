---
created: 2026-05-18
type: migration-notes
status: production
parent: consigliere-thin-node-observer-program (Wave 5 closed)
audience: downstream wallet teams / external API consumers
---

# Broadcast Subsystem — Wave 5 Changeout Notes

Standalone migration document for downstream wallet teams and
external API consumers. Wave 5 (`broadcast-unification-wave`,
closed `00f9cfc`) collapsed the legacy multi-provider broadcast
pipeline onto the W2-era BSV P2P announce path. This document
is the operator + integrator reference for the contract change.

## TL;DR

The old multi-provider broadcast pipeline is **gone**. There is
now exactly one broadcast surface:

- **REST**: `POST /api/tx/broadcast` with body `{ "rawHex": "..." }`
- **SignalR**: `wallethub.invoke('Broadcast', rawHex)`

Both return a `BroadcastReceiptDto`. The legacy
`POST /api/tx/broadcast/{raw}` URL form and the legacy
`Broadcast(rawHex) → bool` SignalR shape are removed. There is
no compatibility shim.

## Contract change

```diff
- // OLD (legacy multi-provider HTTP path, returned a Raven `Broadcast` doc):
- POST /api/tx/broadcast/${rawHex}        → { Success: bool, Attempts: [...] }
- wallethub.invoke('Broadcast', rawHex)   → bool

+ // NEW (Wave 5 unified P2P path):
+ POST /api/tx/broadcast  body: { "rawHex": "..." }  → BroadcastReceiptDto
+ wallethub.invoke('Broadcast', rawHex)               → BroadcastReceiptDto
+
+ // BroadcastReceiptDto shape (frozen by W1 S0.8):
+ //   { TxId, State, CreatedAtMs, FailReason? }
+ // Subsequent state transitions stream via SignalR OnBroadcastStateChanged.
```

### `BroadcastReceiptDto` shape

| Field | Type | Notes |
|---|---|---|
| `TxId` | string | Hex txid (display order); empty when validation failed |
| `State` | string | `OutgoingTxState` enum name — see below |
| `CreatedAtMs` | long | Unix milliseconds at receipt time |
| `FailReason` | string \| null | Operator-readable detail on PolicyInvalid / Failed receipts |

### `OutgoingTxState` values

The receipt's initial `State` is one of `Validated`,
`PolicyInvalid`, or `Failed`. Subsequent transitions
(`Dispatching` → `PeerRelayed` → `Mined`, or terminal
`Failed`) stream over SignalR via `OnBroadcastStateChanged`.
Non-terminal states (`Validated`, `Dispatching`,
`PeerRelayed`) are bounded by `OutgoingTxStates.IsTerminal`.

**A2 H1 contract pin:** when the announce path reports zero
ready peers, the tx persists in `Dispatching`, NOT `Failed`.
The W3 lifecycle monitor / rebroadcaster retries on the next
peer-reconnect. Clients receiving a `Validated` receipt
followed by a long quiet period should NOT assume failure;
poll the SignalR stream for the eventual terminal state.

## Migration steps for wallet clients

1. **Replace the URL** for the REST call. `POST /api/tx/broadcast/${rawHex}`
   → `POST /api/tx/broadcast` with a JSON body
   `{ "rawHex": "..." }`. The Content-Type is
   `application/json`.

2. **Adapt the response handler.** The legacy response was
   `{ Success: bool, Attempts: [...] }`. The new response is
   `BroadcastReceiptDto` (4 fields above). Map your
   downstream "broadcast succeeded" boolean from
   `State == "Validated"`; map "broadcast failed" from
   `State == "PolicyInvalid"` OR `State == "Failed"`. For
   any other state, the broadcast is in flight and a
   SignalR event will deliver the terminal status.

3. **Update SignalR shape.** Clients that called
   `wallethub.invoke('Broadcast', rawHex)` and read a boolean
   now read a `BroadcastReceiptDto` from the same invocation.
   The method name + signature are unchanged.

4. **Drop `IBroadcastProvider` references.** The interface
   was split: broadcast went to the unified P2P path;
   fee-rate stayed under the renamed `IFeeRateProvider`. If
   you instantiated the consigliere `IBroadcastProvider`
   directly (uncommon), switch to `IBroadcastService` for
   broadcast + `IFeeRateProvider` for fee lookups.

5. **Drop reads of the Raven `Broadcast` document model.**
   Historical documents remain in the DB but no new writers
   exist after `00f9cfc`. Replace any forensic read of the
   `Broadcast` doc with a query of `OutgoingTransaction`
   (the W2 lifecycle model).

## Behavioural notes

### Idempotency

`BroadcastService.BroadcastAsync` is idempotent on txid
(W5 A2 M1 fix). A second submission of the same raw hex
returns the existing `OutgoingTransaction`'s receipt
without re-persisting or re-announcing. Wallets that retry
on transient network errors can submit the same hex
safely.

### Policy validation

The W2 `TxPolicyValidator` runs before any P2P announce:
hex size cap (default 2 MB; `BsvP2pConfig.TxPolicy.MaxRawSizeBytes`),
parse validation (round-trip via `Transaction.TryParse`), and
existing-txid dedupe. Failed validation returns
`State = PolicyInvalid` with `FailReason` populated; the tx
is NOT persisted.

### Disabled subsystem

If `Consigliere:Broadcast:P2p:Enabled = false`, every call to
`BroadcastAsync` returns `State = Failed` with
`FailReason = "P2P broadcast subsystem not enabled"`. Wallet
clients integrating against an environment with broadcast
disabled (e.g. a read-only mirror) should handle this
explicitly.

## What did NOT change

- The endpoint authentication / authorization model.
- The SignalR hub name (`wallethub`) and method names.
- The `OutgoingTransaction` Raven document shape.
- The W2-era P2P pool configuration
  (`Consigliere:Broadcast:P2p:*`).
- The W3 rebroadcast policy (still kicks in on peer reconnect
  for stuck `Dispatching` txs).

## See also

- `consigliere-public-api-contract-v1.md` — full public API
  contract.
- `p2p-broadcaster-design.md` — broadcast subsystem
  architecture.
- `thin-node-prod-runbook.md` — operator runbook including
  the W6 alert poller that monitors this surface.
- `docs/stream-tasks/broadcast-unification-wave/evidence/closeout.md`
  — full W5 evidence package + audit trail.
