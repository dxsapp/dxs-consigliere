# Bitails ZMQ Proxy Wave Slices

## Overview

This wave finishes an already-partial product branch.

Today the repo already has:
- `BitailsRealtimeTransportMode.Zmq`
- config fields for Bitails ZMQ URLs
- a scope provider that can build a `CreateZmqPlan(...)`

But runtime still blocks it:
- `BitailsRealtimeIngestRunner` rejects every non-websocket Bitails transport
- there is no dedicated proxy implementation for Bitails socket.io ZMQ topics

The wave goal is to close that gap cleanly.

## Slice Breakdown

### `BZ1` Bitails Proxy Adapter Contract

Intent:
- add the actual Bitails socket.io proxy client and freeze the topic/payload mapping

Owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Infrastructure/Bitails/Realtime/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Infrastructure/Bitails/BitailsProviderDiagnostics.cs` only if needed for transport wording

Exact task:
- add a dedicated Bitails ZMQ proxy ingest client or transport branch
- connect to configurable proxy endpoint(s) over socket.io
- subscribe only to confirmed topics:
  - `rawtx2`
  - `removedfrommempoolblock`
  - `discardedfrommempool`
  - `hashblock2`
- parse payloads into a transport-level model suitable for runtime mapping
- keep API key optional

Do not do:
- direct NetMQ integration
- speculative support for unconfirmed topics
- hardcode `https://zmq.bitails.io` as a stable runtime default

Validation:
- adapter tests or fixture-style payload parsing proof
- exact note of topic names and payload assumptions

Completion signal:
- there is a concrete adapter that can connect to the proxy and expose the four confirmed topic families cleanly

### `BZ2` Runtime Ingest Integration

Intent:
- let the realtime ingest orchestration actually use the Bitails proxy transport

Owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/BackgroundTasks/Realtime/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Services/Impl/**` only if a tiny runtime bridge is needed

Exact task:
- remove the websocket-only guard from `BitailsRealtimeIngestRunner`
- map proxy events into existing buses using current semantics:
  - `rawtx2` -> `TxMessage.AddedToMempool`
  - `removedfrommempoolblock` -> `TxMessage.RemovedFromMempool`
  - `discardedfrommempool` -> `TxMessage.RemovedFromMempool`
  - `hashblock2` -> `IBlockMessageBus`
- keep the existing websocket path intact
- keep recently-seen dedupe sane for raw tx intake

Do not do:
- change node ZMQ behavior
- change public API contracts
- mix broad provider rewiring into this slice

Validation:
- runner-focused tests
- branch-selection proof for `bitailsTransport = zmq`

Completion signal:
- selecting Bitails `zmq` becomes a working runtime path rather than a rejected config

### `BZ3` Config, Startup, And Diagnostics Honesty

Intent:
- make config and diagnostics describe the transport truthfully and keep startup behavior stable

Owned paths:
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Configs/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Consigliere/Setup/**`
- `/Users/imighty/Code/dxs-consigliere/src/Dxs.Infrastructure/Common/**` only if settings access must expand

Exact task:
- keep endpoint URLs configurable and validate only what the runtime really requires
- do not require API key for proxy transport
- make diagnostics wording distinguish proxy transport from direct ZMQ
- tolerate current split `ZmqTxUrl` / `ZmqBlockUrl` shape unless it blocks runtime support

Do not do:
- broad config-schema rename across setup/admin pages unless strictly necessary
- promise a stable vendor URL in defaults/docs

Validation:
- startup/build check
- config validation scenarios for missing/valid proxy URL combinations

Completion signal:
- service can start cleanly with the new transport path and diagnostics do not misdescribe it

### `BZ4` Focused Verification

Intent:
- prove the new transport is real and bounded

Owned paths:
- `/Users/imighty/Code/dxs-consigliere/tests/**`
- closeout docs for this package

Exact task:
- add focused tests for:
  - payload parsing / mapping
  - runtime branch selection for Bitails `zmq`
  - remove-from-mempool reason mapping where applicable
  - block signal handling for `hashblock2`
- capture exact validation commands in closeout

Do not do:
- broad end-to-end environment tests unless already cheap and available

Validation:
- focused test commands
- build commands if code touched startup/config

Completion signal:
- the wave can close with explicit evidence instead of “should work” claims

## Dependency Order

1. `BZ1`
2. `BZ2`
3. `BZ3`
4. `BZ4`

## Validation Matrix

- `BZ1`: adapter tests or payload fixture replay
- `BZ2`: realtime runner tests + branch-selection check
- `BZ3`: build/startup/config validation checks
- `BZ4`: final focused test command log + closeout notes

## Closeout Requirements

At closeout add:
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bitails-zmq-proxy-wave/audits/A1.md`
- `/Users/imighty/Code/dxs-consigliere/doc/stream-tasks/bitails-zmq-proxy-wave/evidence/closeout.md`

Closeout must include:
- exact topic set implemented
- exact payload assumptions used
- whether one shared URL or split tx/block URLs were required in practice
- whether any config cleanup was deferred
- honest residuals if Bitails proxy contract still looks provisional
