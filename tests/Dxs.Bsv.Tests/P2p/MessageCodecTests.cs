using System;
using System.Linq;

using Dxs.Bsv.P2p;
using Dxs.Bsv.P2p.Messages;

namespace Dxs.Bsv.Tests.P2p;

public class MessageCodecTests
{
    private static byte[] Repeat(byte value, int count)
    {
        var arr = new byte[count];
        Array.Fill(arr, value);
        return arr;
    }

    // ---------------- Ping / Pong ----------------

    [Fact]
    public void Ping_RoundTrip()
    {
        var ping = new PingMessage(0xDEADBEEFCAFEBABEUL);
        var bytes = ping.Serialize();
        Assert.Equal(8, bytes.Length);
        Assert.Equal(ping, PingMessage.Parse(bytes));
    }

    [Fact]
    public void Pong_EchoesPingNonce()
    {
        var ping = new PingMessage(0x1122334455667788UL);
        var pong = new PongMessage(ping.Nonce);
        Assert.Equal(ping.Serialize(), pong.Serialize());
        Assert.Equal(ping.Nonce, PongMessage.Parse(pong.Serialize()).Nonce);
    }

    // ---------------- Inv / GetData / NotFound ----------------

    [Fact]
    public void InvMessage_RoundTrip()
    {
        var inv = new InvMessage(new[]
        {
            new InvVector(InvType.Tx, Repeat(0xAB, 32)),
            new InvVector(InvType.Block, Repeat(0xCD, 32)),
        });

        var bytes = inv.Serialize();
        // varint(2) = 1 byte + 2 × 36 = 73 total
        Assert.Equal(73, bytes.Length);

        var parsed = InvMessage.Parse(bytes);
        Assert.Equal(2, parsed.Items.Count);
        Assert.Equal(InvType.Tx, parsed.Items[0].Type);
        Assert.Equal(InvType.Block, parsed.Items[1].Type);
        Assert.Equal(Repeat(0xAB, 32), parsed.Items[0].Hash);
        Assert.Equal(Repeat(0xCD, 32), parsed.Items[1].Hash);
    }

    [Fact]
    public void InvMessage_ForTx_AnnounceConvenience()
    {
        var txid = Repeat(0x42, 32);
        var inv = InvMessage.ForTx(txid);

        Assert.Single(inv.Items);
        Assert.Equal(InvType.Tx, inv.Items[0].Type);
        Assert.Equal(txid, inv.Items[0].Hash);
    }

    [Fact]
    public void GetDataMessage_RoundTrip()
    {
        var get = new GetDataMessage(new[] { new InvVector(InvType.Tx, Repeat(0x01, 32)) });
        var bytes = get.Serialize();
        var parsed = GetDataMessage.Parse(bytes);
        Assert.Single(parsed.Items);
        Assert.Equal(InvType.Tx, parsed.Items[0].Type);
    }

    [Fact]
    public void NotFoundMessage_RoundTrip()
    {
        var nf = new NotFoundMessage(new[] { new InvVector(InvType.Block, Repeat(0xFF, 32)) });
        var parsed = NotFoundMessage.Parse(nf.Serialize());
        Assert.Equal(InvType.Block, parsed.Items[0].Type);
    }

    [Fact]
    public void InvMessage_OversizedCount_ThrowsP2pDecodeException()
    {
        // Claim count = 100_000 (over MaxItems=50_000) with no actual bytes.
        var w = new Dxs.Bsv.P2p.Codec.P2pWriter();
        w.WriteVarInt(100_000);
        var bytes = w.ToArray();

        var ex = Assert.Throws<P2pDecodeException>(() => { InvMessage.Parse(bytes); });
        Assert.Contains("exceeds max", ex.Message);
    }

    // ---------------- Reject ----------------

    [Fact]
    public void Reject_RoundTrip_WithHash()
    {
        var reject = new RejectMessage(
            Message: "tx",
            Code: RejectCode.Invalid,
            Reason: "bad-txns-inputs-missingorspent",
            Hash: Repeat(0xAA, 32));

        var bytes = reject.Serialize();
        var parsed = RejectMessage.Parse(bytes);

        Assert.Equal("tx", parsed.Message);
        Assert.Equal(RejectCode.Invalid, parsed.Code);
        Assert.Equal("bad-txns-inputs-missingorspent", parsed.Reason);
        Assert.Equal(Repeat(0xAA, 32), parsed.Hash);
    }

    [Fact]
    public void Reject_RoundTrip_NoHash()
    {
        var reject = new RejectMessage("version", RejectCode.Obsolete, "Version must be 70016 or greater", null);
        var parsed = RejectMessage.Parse(reject.Serialize());

        Assert.Equal("version", parsed.Message);
        Assert.Equal(RejectCode.Obsolete, parsed.Code);
        Assert.Null(parsed.Hash);
    }

    [Theory]
    [InlineData("bad-txns-inputs-missingorspent", RejectClass.Conflicted)]
    [InlineData("txn-mempool-conflict", RejectClass.Conflicted)]
    [InlineData("mandatory-script-verify-flag-failed", RejectClass.Invalid)]
    [InlineData("bad-txns-too-small", RejectClass.Invalid)]
    [InlineData("dust", RejectClass.PolicyRejected)]
    [InlineData("min-fee-not-met", RejectClass.PolicyRejected)]
    [InlineData("insufficient-fee", RejectClass.PolicyRejected)]
    [InlineData("mempool-full", RejectClass.Transient)]
    [InlineData("txn-already-known", RejectClass.Transient)]
    [InlineData("some-future-reason-we-dont-know", RejectClass.Unknown)]
    public void Reject_Classify_MatchesDesignTable(string reason, RejectClass expected)
    {
        var r = new RejectMessage("tx", RejectCode.Invalid, reason, null);
        Assert.Equal(expected, r.Classify());
    }

    // ---------------- Addr / GetAddr ----------------

    [Fact]
    public void AddrMessage_RoundTrip()
    {
        var addr = new AddrMessage(new[]
        {
            new TimedAddress(1700000000, P2pAddress.FromIPv4(0x01, "192.0.2.1", 8333)),
            new TimedAddress(1700000060, P2pAddress.FromIPv4(0x25, "10.0.0.1", 18333)),
        });

        var parsed = AddrMessage.Parse(addr.Serialize());

        Assert.Equal(2, parsed.Addresses.Count);
        Assert.Equal("192.0.2.1", parsed.Addresses[0].Address.TryGetIPv4());
        Assert.Equal((ushort)8333, parsed.Addresses[0].Address.Port);
        Assert.Equal(1700000000U, parsed.Addresses[0].TimestampUnixSeconds);
    }

    [Fact]
    public void AddrMessage_Empty_HasSingleZeroByte()
    {
        var bytes = AddrMessage.Empty.Serialize();
        Assert.Single(bytes);
        Assert.Equal(0, bytes[0]);
    }

    [Fact]
    public void AddrMessage_OversizedCount_ThrowsP2pDecodeException()
    {
        var w = new Dxs.Bsv.P2p.Codec.P2pWriter();
        w.WriteVarInt(2000);          // > 1000 limit
        var bytes = w.ToArray();

        var ex = Assert.Throws<P2pDecodeException>(() => { AddrMessage.Parse(bytes); });
        Assert.Contains("exceeds max", ex.Message);
    }

    // ---------------- Headers / GetHeaders ----------------

    [Fact]
    public void GetHeaders_RoundTrip()
    {
        var get = new GetHeadersMessage(
            ProtocolVersion: 70016,
            Locator: new[] { Repeat(0xAB, 32), Repeat(0xCD, 32) },
            StopHash: Repeat(0, 32));

        var parsed = GetHeadersMessage.Parse(get.Serialize());

        Assert.Equal(70016U, parsed.ProtocolVersion);
        Assert.Equal(2, parsed.Locator.Count);
        Assert.Equal(Repeat(0xAB, 32), parsed.Locator[0]);
        Assert.Equal(Repeat(0, 32), parsed.StopHash);
    }

    [Fact]
    public void GetHeaders_OversizedLocator_ThrowsP2pDecodeException()
    {
        var w = new Dxs.Bsv.P2p.Codec.P2pWriter();
        w.WriteUInt32Le(70016);
        w.WriteVarInt(500);                 // > 101 max
        var bytes = w.ToArray();

        var ex = Assert.Throws<P2pDecodeException>(() => { GetHeadersMessage.Parse(bytes); });
        Assert.Contains("exceeds max", ex.Message);
    }

    [Fact]
    public void Headers_Empty_HasSingleZeroByte()
    {
        var bytes = HeadersMessage.Empty.Serialize();
        Assert.Single(bytes);
        Assert.Equal(0, bytes[0]);
    }

    [Fact]
    public void Headers_RoundTrip_NonEmpty()
    {
        var hdrs = new HeadersMessage(new[] { new BlockHeader(Repeat(0x55, BlockHeader.Size)) });
        var parsed = HeadersMessage.Parse(hdrs.Serialize());

        Assert.Single(parsed.Headers);
        Assert.Equal(Repeat(0x55, BlockHeader.Size), parsed.Headers[0].Bytes80);
    }

    // ---------------- Protoconf ----------------

    [Fact]
    public void Protoconf_PhaseOneDefault_SerializesCorrectly()
    {
        var bytes = ProtoconfMessage.PhaseOneDefault.Serialize();
        // 1 (varint count=2) + 4 (uint32 max_recv) + 1 (varint UA len=7) + 7 ("Default") = 13
        Assert.Equal(13, bytes.Length);
        Assert.Equal(2, bytes[0]);                                // numberOfFields
        Assert.Equal(0x00, bytes[1]);                             // 0x00200000 LE
        Assert.Equal(0x00, bytes[2]);
        Assert.Equal(0x20, bytes[3]);
        Assert.Equal(0x00, bytes[4]);
        Assert.Equal(7, bytes[5]);                                // varint UA len
        Assert.Equal((byte)'D', bytes[6]);
    }

    [Fact]
    public void Protoconf_RoundTrip()
    {
        var pc = new ProtoconfMessage(MaxRecvPayloadLength: 4 * 1024 * 1024, StreamPolicies: "BlockPriority,Default");
        var parsed = ProtoconfMessage.Parse(pc.Serialize());
        Assert.Equal(4U * 1024 * 1024, parsed.MaxRecvPayloadLength);
        Assert.Equal("BlockPriority,Default", parsed.StreamPolicies);
    }

    [Fact]
    public void Protoconf_OnlyFirstField_StillParses()
    {
        // Older peers may send numberOfFields=1, no streamPolicies.
        var w = new Dxs.Bsv.P2p.Codec.P2pWriter();
        w.WriteVarInt(1);
        w.WriteUInt32Le(1_000_000);
        var bytes = w.ToArray();

        var parsed = ProtoconfMessage.Parse(bytes);
        Assert.Equal(1_000_000U, parsed.MaxRecvPayloadLength);
        Assert.Equal("Default", parsed.StreamPolicies); // default fallback
    }

    [Fact]
    public void Protoconf_NumberOfFieldsZero_Throws()
    {
        var w = new Dxs.Bsv.P2p.Codec.P2pWriter();
        w.WriteVarInt(0);
        var bytes = w.ToArray();

        var ex = Assert.Throws<P2pDecodeException>(() => { ProtoconfMessage.Parse(bytes); });
        Assert.Contains("at least 1 field", ex.Message);
    }

    // ---------------- FeeFilter ----------------

    [Fact]
    public void FeeFilter_RoundTrip()
    {
        var ff = new FeeFilterMessage(FeePerKbSat: 500);
        Assert.Equal(8, ff.Serialize().Length);
        Assert.Equal(500L, FeeFilterMessage.Parse(ff.Serialize()).FeePerKbSat);
    }
}
