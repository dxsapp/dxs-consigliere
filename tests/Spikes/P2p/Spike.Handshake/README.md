# Spike: P2P Handshake Pinger

Throwaway console app that validates the assumptions behind the
[P2P Broadcaster design](../../../../doc/platform-api/p2p-broadcaster-design.md).

## Goal

For each candidate BSV mainnet peer:

1. Open a TCP socket.
2. Send a Bitcoin P2P `version` message.
3. Receive the peer's `version`, send `verack`, receive `verack`.
4. Send a `ping` (random nonce), receive the matching `pong`.
5. Record: success/failure, handshake latency, ping RTT, peer user-agent,
   peer protocol version, peer reported services.

## Why

Validates three things before we commit to writing production P2P code:

- That we can speak the BSV P2P wire format end-to-end (framing, checksums,
  varints, the BSV `0xE3E1F3E8` magic).
- That candidate peers from public DNS seeds are reachable and responsive
  from our network.
- That we have at least 5–8 usable peers for the hardcoded fallback seed
  list referenced by the design.

## Run

```bash
cd tests/Spikes/P2p/Spike.Handshake
dotnet run --configuration Release
```

The peer list is hardcoded in `Program.cs`. Adjust there.

## Disposable

This project is **not** added to `Dxs.Consigliere.sln`. It will be deleted
before the Phase 1 PR.
