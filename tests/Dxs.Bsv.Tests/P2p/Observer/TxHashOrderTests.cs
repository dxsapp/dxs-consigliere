using System;

using Dxs.Bsv;
using Dxs.Bsv.P2p.Observer;

namespace Dxs.Bsv.Tests.P2p.Observer;

public class TxHashOrderTests
{
    /// <summary>
    /// BSV mainnet block-0 coinbase tx (genesis): wire-order hash is
    /// 3b a3 ed fd 7a 7b 12 b2 7a c7 2c 3e 67 76 8f 61 7f c8 1b c3
    /// 88 8a 51 32 3a 9f b8 aa 4b 1e 5e 4a; display order is the
    /// byte-reverse 4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b.
    /// (The genesis coinbase txid happens to equal the merkle root.)
    /// </summary>
    private const string CoinbaseDisplay = "4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b";

    [Fact]
    public void WireToDisplayHex_ReversesBytes_AndLowercases()
    {
        // Compose the wire-order bytes (display-order reversed).
        var display = Convert.FromHexString(CoinbaseDisplay);
        Array.Reverse(display);
        var wire = display; // now wire-order

        var result = TxHashOrder.WireToDisplayHex(wire);
        Assert.Equal(CoinbaseDisplay, result);
    }

    [Fact]
    public void DisplayHexToWire_IsInverseOfWireToDisplayHex()
    {
        var wire = TxHashOrder.DisplayHexToWire(CoinbaseDisplay);
        var back = TxHashOrder.WireToDisplayHex(wire);
        Assert.Equal(CoinbaseDisplay, back);
    }

    [Fact]
    public void RoundTrip_RandomBytes()
    {
        var rng = new Random(42);
        for (var i = 0; i < 16; i++)
        {
            var wire = new byte[32];
            rng.NextBytes(wire);
            var display = TxHashOrder.WireToDisplayHex(wire);
            var wireBack = TxHashOrder.DisplayHexToWire(display);
            Assert.Equal(wire, wireBack);
        }
    }

    [Fact]
    public void WireToDisplayHex_RejectsWrongSize()
    {
        Assert.Throws<ArgumentException>(() => TxHashOrder.WireToDisplayHex(new byte[31]));
        Assert.Throws<ArgumentException>(() => TxHashOrder.WireToDisplayHex(new byte[33]));
    }

    [Fact]
    public void DisplayHexToWire_RejectsWrongLength()
    {
        Assert.Throws<ArgumentException>(() => TxHashOrder.DisplayHexToWire(""));
        Assert.Throws<ArgumentException>(() => TxHashOrder.DisplayHexToWire(new string('0', 63)));
        Assert.Throws<ArgumentException>(() => TxHashOrder.DisplayHexToWire(new string('0', 65)));
    }

    [Fact]
    public void WireToDisplayHex_MatchesBitcoinHelpers_GetTxId()
    {
        // BitcoinHelpers.GetTxId is the existing canonical txid
        // helper used by Transaction.Id + Bitails / JungleBus
        // realtime. Pin that our wire→display normalisation
        // produces the same byte order so the two paths dedupe
        // on the same id.
        var anyBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        // GetTxId hashes the bytes then reverses → display-order.
        var fromHelper = BitcoinHelpers.GetTxId(anyBytes);

        // Same end result via wire-order intermediate:
        var wire = Hash.Sha256Sha256(anyBytes);
        var fromOrder = TxHashOrder.WireToDisplayHex(wire);

        Assert.Equal(fromHelper, fromOrder);
    }
}
