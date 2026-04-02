# Bitails ZMQ Proxy Wave

## Goal

Add a real Bitails `zmq` transport mode for realtime ingest using Bitails' socket.io proxy service rather than direct NetMQ.

After this wave:
- `bitailsTransport = zmq` is a real runtime path, not a half-wired configuration branch
- Consigliere can ingest Bitails proxy topics for mempool add/remove and block-connect signals
- the transport is modeled honestly as a Bitails ZMQ proxy over sockets, not as direct ZMQ access
- endpoint URLs remain config-driven because Bitails may still change the proxy address

## Core Decision

This wave implements `Bitails ZMQ` as a distinct proxy transport profile.

It is:
- not direct `NetMQ`
- not the same thing as the existing Bitails websocket path
- not allowed to hardcode `https://zmq.bitails.io` as product truth

Use clear language in code and docs:
- `ZMQ proxy`
- `socket.io proxy`
- `Bitails ZMQ proxy transport`

Do not pretend this is node-style ZMQ.

## Scope

In scope:
- Bitails realtime adapter implementation for socket.io-based ZMQ proxy topics
- runtime orchestration changes needed to allow `bitailsTransport = zmq`
- config/startup validation for proxy transport inputs
- targeted tests for payload mapping and runtime wiring
- minimal provider diagnostics updates if needed to describe the proxy transport honestly

Out of scope:
- changing top-level admin UX flow
- renaming every existing `zmq` field in one go
- direct NetMQ support for Bitails
- speculative support for topics Bitails has not confirmed
- broad provider cleanup unrelated to Bitails realtime ingest

## Core Rules

- Keep endpoint URLs config-driven; do not hardcode the Bitails proxy host as a stable default.
- Treat the transport as a dedicated Bitails proxy implementation, not a reuse of node ZMQ code.
- Reuse existing tx/block buses and mempool-removal semantics where possible.
- Support only confirmed topics in this wave:
  - `rawtx2`
  - `removedfrommempoolblock`
  - `discardedfrommempool`
  - `hashblock2`
- Keep API key handling optional and non-required for this transport path.
- If current config shape (`ZmqTxUrl` / `ZmqBlockUrl`) is awkward, tolerate it in v1 and record any cleanup as residual rather than blocking runtime support.

## Ownership Zones

| slice | zone lead | owner | status | depends_on | validation | done_when |
|---|---|---|---|---|---|---|
| `BZ1` | `external-chain-adapters` | `operator/integration` | `todo` | - | adapter tests + payload replay note | Bitails ZMQ proxy client and topic mapping contract are frozen |
| `BZ2` | `indexer-ingest-orchestration` | `operator/runtime` | `todo` | `BZ1` | runner tests + integration wiring check | `bitailsTransport = zmq` runs through realtime ingest without websocket-only guardrails |
| `BZ3` | `service-bootstrap-and-ops` | `operator/platform` | `todo` | `BZ1`,`BZ2` | config validation + startup/build check | config/diagnostics/startup describe proxy transport honestly and accept optional no-auth operation |
| `BZ4` | `verification-and-conformance` | `operator/verification` | `todo` | `BZ1`,`BZ2`,`BZ3` | focused tests + command log | payload-to-bus behavior and runtime branch are proven |

## Definition of Done

- Bitails proxy `zmq` mode is implemented as its own socket.io-based ingest path.
- `BitailsRealtimeIngestRunner` no longer rejects `BitailsRealtimeTransportMode.Zmq`.
- Confirmed proxy topics map into existing Consigliere tx/block buses correctly.
- No direct-ZMQ naming confusion is introduced in runtime code or diagnostics.
- Transport endpoint remains configurable and does not rely on a hardcoded host.
- Focused validation covers adapter parsing, runtime branch selection, and startup/config acceptance.

## Delivery Notes

- Package path: `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bitails-zmq-proxy-wave/`
- Expect cross-zone execution in this order:
  1. `BZ1 external-chain-adapters`
  2. `BZ2 indexer-ingest-orchestration`
  3. `BZ3 service-bootstrap-and-ops`
  4. `BZ4 verification-and-conformance`
- If Bitails confirms additional topics or changes payload shape mid-wave, record the exact contract drift in closeout rather than silently broadening scope.
