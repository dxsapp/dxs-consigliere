# Bitails ZMQ Proxy Wave Closeout

## Result
The wave is complete.

Bitails `zmq` is now a real runtime path implemented as a socket.io proxy transport:
- it no longer relies on a websocket-only runner guard
- it no longer pretends to be direct NetMQ access
- it maps confirmed proxy topics into existing Consigliere tx/block buses

## Implemented Topic Set
- `rawtx2`
- `removedfrommempoolblock`
- `discardedfrommempool`
- `hashblock2`

## Payload Assumptions Used
- `rawtx2` may deliver either a raw transaction hex payload or an object containing raw hex and, optionally, a transaction id.
- `removedfrommempoolblock` and `discardedfrommempool` are treated as JSON-style remove events carrying at least a transaction id and optionally:
  - `reason`
  - `collidedWith`
  - `blockHash`
- `hashblock2` may deliver either a direct block-hash string or an object containing a hash field.

## Behavioral Summary
- the transport planner now freezes Bitails proxy mode to the four confirmed topics instead of deriving operator-specific topics
- the websocket Bitails client and the ZMQ proxy client now publish a shared generic event model
- the runtime runner maps:
  - tx add -> `TxMessage.AddedToMempool`
  - tx remove -> `TxMessage.RemovedFromMempool`
  - block connect -> `IBlockMessageBus`
- tx-added dedupe still runs through the existing recently-seen guard
- tx-added events can use embedded raw transaction bytes directly, avoiding an extra raw fetch when Bitails proxy supplies the payload

## Config Reality
- runtime still uses the existing split fields:
  - `connection.zmq.txUrl`
  - `connection.zmq.blockUrl`
- in practice, the current proxy implementation uses one shared socket.io endpoint
- if both URLs are supplied, validation now requires them to be the same

## Honest Residual
- this wave does not rename or simplify the current `zmq` config fields; that cleanup belongs to later config polish
- the vendor endpoint remains config-driven because Bitails has not frozen the proxy address yet
- no speculative extra Bitails proxy topics were added beyond the ones explicitly confirmed
