using System;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;

namespace Dxs.Bsv.Tests.P2p;

public class VersionMessageTests
{
    /// <summary>
    /// Real bitcoind /Bitcoin SV:1.2.1/ outbound version frame captured on
    /// the wire to peer 15.235.232.121:8333 during Spike E. Payload only
    /// (after the 24-byte frame header). 122 bytes including 18-byte association ID tail.
    /// </summary>
    private const string RealBitcoindVersionPayloadHex =
        "801101002000000000000000bce7056a00000000210000000000000000000000000000000000ffff0febe879208d2000000000000000000000000000000000000000000000000000c8feedbd64700d3d122f426974636f696e2053563a312e322e312f5f7b0e00011100a1559341562746648edb6e403ccb5e7b";

    [Fact]
    public void Parse_RealBitcoindCapture_DecodesAllFieldsCorrectly()
    {
        var payload = Convert.FromHexString(RealBitcoindVersionPayloadHex);

        var msg = VersionMessage.Parse(payload);

        Assert.Equal(70016, msg.ProtocolVersion);
        Assert.Equal(0x20UL, msg.Services);
        Assert.Equal(1778771900L, msg.TimestampUnixSeconds);

        // addr_recv: 15.235.232.121:8333, services NODE_NETWORK+NODE_BITCOIN_CASH (0x21)
        Assert.Equal(0x21UL, msg.AddrRecv.Services);
        Assert.Equal("15.235.232.121", msg.AddrRecv.TryGetIPv4());
        Assert.Equal((ushort)8333, msg.AddrRecv.Port);

        // addr_from: zeros / port 0 / services 0x20
        Assert.Equal(0x20UL, msg.AddrFrom.Services);
        Assert.Null(msg.AddrFrom.TryGetIPv4()); // all-zero IPv6, not IPv4-mapped
        Assert.Equal((ushort)0, msg.AddrFrom.Port);

        // User agent
        Assert.Equal("/Bitcoin SV:1.2.1/", msg.UserAgent);

        // Start height
        Assert.Equal(949087, msg.StartHeight);

        // Relay
        Assert.True(msg.Relay);

        // Association ID present, 17 bytes: [IDType=UUID=0x00][16-byte UUID]
        Assert.NotNull(msg.AssociationId);
        Assert.Equal(17, msg.AssociationId!.Length);
        Assert.Equal(0x00, msg.AssociationId[0]); // IDType::UUID
    }

    [Fact]
    public void Build_AndSerialize_NoAssociationId_MatchesPhase1Spec()
    {
        // Phase 1 default: NO association ID tail. Payload must be 104 bytes.
        var msg = new VersionMessage(
            ProtocolVersion: 70016,
            Services: 0x25,
            TimestampUnixSeconds: 1700000000L,
            AddrRecv: P2pAddress.FromIPv4(0x01, "1.2.3.4", 8333),
            AddrFrom: P2pAddress.Anonymous(0x25),
            Nonce: 0xDEADBEEFCAFEBABEUL,
            UserAgent: "/ConsigliereThinNode:0.1.0/",
            StartHeight: 0,
            Relay: true,
            AssociationId: null);

        var bytes = msg.Serialize();

        // 4 + 8 + 8 + 26 + 26 + 8 + 1 + 27 (UA) + 4 + 1 = 113
        // UA len = "/ConsigliereThinNode:0.1.0/" = 27 chars; +1 varint = 28
        // total = 4+8+8+26+26+8+28+4+1 = 113
        Assert.Equal(113, bytes.Length);

        // Verify version field (LE)
        Assert.Equal(0x80, bytes[0]);
        Assert.Equal(0x11, bytes[1]);
        Assert.Equal(0x01, bytes[2]);
        Assert.Equal(0x00, bytes[3]);

        // Verify services (0x25 in first byte)
        Assert.Equal(0x25, bytes[4]);
        for (var i = 5; i < 12; i++) Assert.Equal(0, bytes[i]);

        // Verify last byte = relay = 0x01
        Assert.Equal(0x01, bytes[^1]);
    }

    [Fact]
    public void Build_WithBitcoinSV121UserAgent_NoAssocId_HasPayload104()
    {
        // Match the post-audit Phase 1 spec exactly:
        // services=0x25, UA=/Bitcoin SV:1.2.1/ (18 chars), no assoc_id → 104 bytes total.
        var msg = new VersionMessage(
            ProtocolVersion: 70016,
            Services: 0x25,
            TimestampUnixSeconds: 1700000000L,
            AddrRecv: P2pAddress.FromIPv4(0x01, "1.2.3.4", 8333),
            AddrFrom: P2pAddress.Anonymous(0x25),
            Nonce: 1UL,
            UserAgent: "/Bitcoin SV:1.2.1/",
            StartHeight: 0,
            Relay: true,
            AssociationId: null);

        Assert.Equal(104, msg.Serialize().Length);
    }

    [Fact]
    public void RoundTrip_AllFieldsPreserved()
    {
        var original = new VersionMessage(
            ProtocolVersion: 70016,
            Services: 0x25,
            TimestampUnixSeconds: 1700000000L,
            AddrRecv: P2pAddress.FromIPv4(0x01, "127.0.0.1", 18333),
            AddrFrom: P2pAddress.FromIPv4(0x25, "10.0.0.1", 8333),
            Nonce: 0xABCDEF0123456789UL,
            UserAgent: "/test:0.1/",
            StartHeight: 12345,
            Relay: false,
            AssociationId: new byte[] { 0x00, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0x00 });

        var bytes = original.Serialize();
        var parsed = VersionMessage.Parse(bytes);

        Assert.Equal(original.ProtocolVersion, parsed.ProtocolVersion);
        Assert.Equal(original.Services, parsed.Services);
        Assert.Equal(original.TimestampUnixSeconds, parsed.TimestampUnixSeconds);
        Assert.Equal(original.AddrRecv.Services, parsed.AddrRecv.Services);
        Assert.Equal(original.AddrRecv.RawIpv6, parsed.AddrRecv.RawIpv6);
        Assert.Equal(original.AddrRecv.Port, parsed.AddrRecv.Port);
        Assert.Equal(original.AddrFrom.Services, parsed.AddrFrom.Services);
        Assert.Equal(original.AddrFrom.RawIpv6, parsed.AddrFrom.RawIpv6);
        Assert.Equal(original.AddrFrom.Port, parsed.AddrFrom.Port);
        Assert.Equal(original.Nonce, parsed.Nonce);
        Assert.Equal(original.UserAgent, parsed.UserAgent);
        Assert.Equal(original.StartHeight, parsed.StartHeight);
        Assert.Equal(original.Relay, parsed.Relay);
        Assert.Equal(original.AssociationId, parsed.AssociationId);
    }

    [Fact]
    public void Parse_TruncatedPayload_ThrowsP2pDecodeException()
    {
        // Only first 20 bytes — not enough for full version field set.
        var truncated = new byte[20];

        var ex = Assert.Throws<P2pDecodeException>(() => { VersionMessage.Parse(truncated); });
        Assert.Contains("addr_recv", ex.Message);
    }

    [Fact]
    public void Parse_OversizedUserAgent_ThrowsP2pDecodeException()
    {
        // Build a payload with user_agent length claimed > MaxUserAgentLength (256).
        // After addr_from+nonce we have: ua varint 0xFD 0xFF 0x07 = 2047 (claimed length).
        // Followed by 0 bytes of UA — should fail length-bound check first.
        var writer = new Dxs.Bsv.P2p.Codec.P2pWriter();
        writer.WriteInt32Le(70016);
        writer.WriteUInt64Le(0);
        writer.WriteInt64Le(0);
        // addr_recv (26 zero bytes)
        writer.WriteBytes(new byte[26]);
        // addr_from (26 zero bytes)
        writer.WriteBytes(new byte[26]);
        // nonce
        writer.WriteUInt64Le(0);
        // user_agent length varint = 0xFD 0xFF 0x07 = 2047
        writer.WriteByte(0xFD);
        writer.WriteByte(0xFF);
        writer.WriteByte(0x07);

        var bytes = writer.ToArray();
        var ex = Assert.Throws<P2pDecodeException>(() => { VersionMessage.Parse(bytes); });
        Assert.Contains("user_agent", ex.Message);
    }

    [Fact]
    public void Parse_OldStyleShortPayload_HandlesGracefully()
    {
        // Just version + services + timestamp + addr_recv (52 bytes). Older clients allowed.
        var writer = new Dxs.Bsv.P2p.Codec.P2pWriter();
        writer.WriteInt32Le(60002);
        writer.WriteUInt64Le(0x01);
        writer.WriteInt64Le(1600000000L);
        // addr_recv (26 bytes, ipv4-mapped)
        var addr = P2pAddress.FromIPv4(0x01, "192.0.2.1", 8333);
        addr.Write(writer);

        var msg = VersionMessage.Parse(writer.ToArray());

        Assert.Equal(60002, msg.ProtocolVersion);
        Assert.Equal("192.0.2.1", msg.AddrRecv.TryGetIPv4());
        Assert.Empty(msg.UserAgent);
        Assert.Equal(0, msg.StartHeight);
        Assert.Null(msg.AssociationId);
    }
}
