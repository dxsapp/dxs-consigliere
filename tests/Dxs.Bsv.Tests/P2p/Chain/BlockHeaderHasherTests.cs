using System;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;

namespace Dxs.Bsv.Tests.P2p.Chain;

public class BlockHeaderHasherTests
{
    /// <summary>
    /// BSV/BTC mainnet genesis header (block 0). Hash in display order is
    /// 000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f
    /// (wire/LE order is the byte-reverse of that).
    /// </summary>
    private const string GenesisHeaderHex =
        "01000000" +                                                          // version
        "0000000000000000000000000000000000000000000000000000000000000000" + // prev_block
        "3ba3edfd7a7b12b27ac72c3e67768f617fc81bc3888a51323a9fb8aa4b1e5e4a" + // merkle_root
        "29ab5f49" +                                                         // timestamp
        "ffff001d" +                                                         // bits
        "1dac2b7c";                                                          // nonce

    private const string GenesisHashWireHex =
        "6fe28c0ab6f1b372c1a6a246ae63f74f931e8365e15a089c68d6190000000000";

    [Fact]
    public void Hash_OfGenesisHeader_MatchesKnownValue()
    {
        var header = new BlockHeader(Convert.FromHexString(GenesisHeaderHex));
        var hash = BlockHeaderHasher.Hash(header);
        Assert.Equal(GenesisHashWireHex, Convert.ToHexString(hash).ToLowerInvariant());
    }

    [Fact]
    public void PrevBlock_OfGenesisHeader_IsAllZero()
    {
        var header = new BlockHeader(Convert.FromHexString(GenesisHeaderHex));
        var prev = BlockHeaderHasher.PrevBlock(header).ToArray();
        Assert.Equal(32, prev.Length);
        Assert.All(prev, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Bits_OfGenesisHeader_Is_1d00ffff()
    {
        var header = new BlockHeader(Convert.FromHexString(GenesisHeaderHex));
        Assert.Equal(0x1d00ffffu, BlockHeaderHasher.Bits(header));
    }

    [Fact]
    public void TimestampUnixSeconds_OfGenesisHeader_Is_1231006505()
    {
        var header = new BlockHeader(Convert.FromHexString(GenesisHeaderHex));
        Assert.Equal(1231006505u, BlockHeaderHasher.TimestampUnixSeconds(header));
    }

    [Fact]
    public void MeetsTarget_GenesisHeader_True()
    {
        var header = new BlockHeader(Convert.FromHexString(GenesisHeaderHex));
        Assert.True(BlockHeaderHasher.MeetsTarget(header));
    }

    [Fact]
    public void MeetsTarget_TamperedNonce_FailsPow()
    {
        var bytes = Convert.FromHexString(GenesisHeaderHex);
        // Flip the nonce — almost certainly fails the 0x1d00ffff target.
        bytes[76] ^= 0xff;
        var header = new BlockHeader(bytes);
        Assert.False(BlockHeaderHasher.MeetsTarget(header));
    }

    [Fact]
    public void MeetsTarget_RegtestDifficulty_AlmostAnyHashPasses()
    {
        // bits=0x207fffff is the regtest minimum (highest possible target).
        var header = HeaderTestUtil.Build(prev: new byte[32], bits: 0x207fffffu);
        Assert.True(BlockHeaderHasher.MeetsTarget(header));
    }

    [Theory]
    [InlineData(0x80000000u)] // sign-bit set
    [InlineData(0x00000000u)] // zero mantissa
    public void TryExpandTarget_RejectsInvalidEncodings(uint compact)
    {
        Assert.False(BlockHeaderHasher.TryExpandTarget(compact, out _));
    }

    [Fact]
    public void ToDisplayHex_OfGenesisHash_MatchesExplorerOrder()
    {
        // Audit A2 H3: external API must surface display order (what
        // explorers show). Wire-order genesis hash reversed must equal
        // the well-known display-order genesis hash.
        var wire = Convert.FromHexString(GenesisHashWireHex);
        var display = BlockHeaderHasher.ToDisplayHex(wire);
        Assert.Equal("000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f", display);
    }

    [Fact]
    public void ToDisplayHex_IsReversibleRoundTrip()
    {
        var wire = new byte[32];
        for (var i = 0; i < 32; i++) wire[i] = (byte)i;
        var display = BlockHeaderHasher.ToDisplayHex(wire);
        var displayBytes = Convert.FromHexString(display);
        Array.Reverse(displayBytes);
        Assert.Equal(wire, displayBytes);
    }

    [Fact]
    public void TryExpandTarget_GenesisBits_ExpandsToKnownTarget()
    {
        // 0x1d00ffff → target = 0x00000000 ffff 0000…0000 (24 trailing zero bytes
        // because exponent 0x1d = 29, mantissa 0x00ffff at byteOffset 26..28 in LE).
        Assert.True(BlockHeaderHasher.TryExpandTarget(0x1d00ffffu, out var target));
        Assert.Equal(32, target.Length);
        // Display-order target string: "00000000ffff0000...000000". Wire-order
        // is the reverse: 24 zero bytes (LE positions 0..23), then ff,ff,00 at
        // positions 26..28, then zeros above.
        Assert.Equal(0xff, target[26]);
        Assert.Equal(0xff, target[27]);
        Assert.Equal(0x00, target[28]);
        for (var i = 0; i < 26; i++) Assert.Equal(0, target[i]);
        for (var i = 29; i < 32; i++) Assert.Equal(0, target[i]);
    }
}
