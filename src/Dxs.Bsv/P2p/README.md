# `Dxs.Bsv.P2p` — Bitcoin SV P2P Message Codec

Pure byte-level codec for the BSV P2P wire protocol. **No network code in this layer** —
this is the conformance layer (Gate 1 of [consigliere-thin-node-design.md](../../../doc/platform-api/consigliere-thin-node-design.md)).

## Layout

```
P2p/
├── P2pNetwork.cs           # Mainnet magic + DNS seeds
├── P2pCommands.cs          # Canonical command name constants
├── P2pAddress.cs           # 26-byte CAddress encoding (no nTime)
├── Frame.cs                # Wire frame structural constants
├── FrameCodec.cs           # Encode + streaming-friendly TryDecode
├── P2pDecodeException.cs   # Single structured error type
├── Codec/
│   ├── P2pWriter.cs        # Append-only payload builder
│   └── P2pReader.cs        # Forward-only payload reader (ref struct)
└── Messages/
    ├── VersionMessage.cs   # The big one. Phase 1 ships WITHOUT association ID
    ├── PingMessage.cs      # ping + pong (same wire format)
    ├── InventoryMessages.cs# inv / getdata / notfound (shared InvVector list)
    ├── RejectMessage.cs    # reject + RejectClass classification
    ├── AddrMessage.cs      # addr (gossip)
    ├── HeadersMessages.cs  # headers + getheaders (proper locator parsing)
    ├── ProtoconfMessage.cs # BSV-specific protoconf (sent after our verack)
    └── FeeFilterMessage.cs # peer's min-fee-per-kb (respect before announcing)
```

## Frame encoding

```csharp
var payload = new PingMessage(Nonce: 42).Serialize();
var frame = FrameCodec.Encode(P2pNetwork.Mainnet, P2pCommands.Ping, payload);
// frame is now the full 24-byte header + payload, ready to write to the wire.
```

## Frame decoding (streaming-friendly)

```csharp
var result = FrameCodec.TryDecode(
    P2pNetwork.Mainnet,
    buffer,
    maxPayloadLength: Frame.LegacyMaxPayloadLength,
    out var frame,
    out var consumed);

switch (result)
{
    case DecodeResult.Ok:
        ProcessFrame(frame!);
        buffer = buffer.Slice(consumed);
        break;
    case DecodeResult.NeedMore:
        await ReadMore(); break;
    case DecodeResult.BadMagic:
    case DecodeResult.BadChecksum:
    case DecodeResult.BadCommand:
    case DecodeResult.OversizedPayload:
        // Disconnect or banscore on the network layer (Gate 2).
        break;
}
```

## Empty-payload messages

`verack`, `getaddr`, and `mempool` carry zero payload bytes. No message
class is provided — just encode directly:

```csharp
var verack = FrameCodec.Encode(P2pNetwork.Mainnet, P2pCommands.Verack, ReadOnlySpan<byte>.Empty);
```

## Error handling contract

Every parser throws `P2pDecodeException` (and **only** that type) for any
malformed input. The network layer (Gate 2) must catch this and translate
it into a peer-disconnect / banscore action — it must **never** propagate
unhandled into the host process.

Specifically the codec rejects:

| Failure | Where |
|---|---|
| short buffer | `P2pReader.ReadByte/Bytes/...` |
| oversized var_bytes / var_str (length > bound) | `P2pReader.ReadVarBytes` |
| oversized inv / addr / locator / headers count | per-message Parse |
| bad magic / bad checksum / bad command | `FrameCodec.TryDecode` returns enum |
| oversized frame payload | `FrameCodec.TryDecode` returns `OversizedPayload` |

## Reference materials this codec was built against

- `bitcoin-sv` master — `src/net/net_processing.cpp`, `src/net/net.cpp`,
  `src/protocol.cpp`, `src/net/net_message.cpp`
- Teranode — `services/legacy/peer/peer.go`,
  `services/legacy/peer/association.go`
- `bsv-p2p` (kevinejohn) — `src/messages/version.ts`, `src/index.ts`
- Live captured frames from `/Bitcoin SV:1.2.1/` peer (Spike E in design doc §16)

The `VersionMessageTests.Parse_RealBitcoindCapture_DecodesAllFieldsCorrectly`
test consumes a real captured frame and validates every field — this is the
canonical conformance vector.

## What's NOT in this layer

- TCP sockets, retries, keepalive, peer pool — Gate 2.
- Transaction parsing / `tx` message body — out of scope (we just forward
  raw bytes when serving `getdata`).
- `extmsg` extended framing (for payloads > 4 GiB) — Gate 4.
- Authch / authresp / sendheaders / sendcmpct / sendhdrsen — accepted by
  network layer but no parsing needed.
