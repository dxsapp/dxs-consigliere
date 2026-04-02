using System.Text.Json;

using Dxs.Infrastructure.Bitails.Realtime;

namespace Dxs.Bsv.Tests.Bitails;

public class BitailsRealtimePayloadParserTests
{
    [Fact]
    public void ExtractTxId_FromDirectProperty()
    {
        using var document = JsonDocument.Parse("""
            { "txid": "89abcdefabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba" }
            """);

        var txId = BitailsRealtimePayloadParser.ExtractTxId(document.RootElement);

        Assert.Equal("89abcdefabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba", txId);
    }

    [Fact]
    public void ExtractTxId_FromNestedPayload()
    {
        using var document = JsonDocument.Parse("""
            { "data": { "transaction": { "id": "89abcdefabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba" } } }
            """);

        var txId = BitailsRealtimePayloadParser.ExtractTxId(document.RootElement);

        Assert.Equal("89abcdefabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba", txId);
    }

    [Fact]
    public void ReturnsNull_WhenPayloadHasNoTransactionId()
    {
        using var document = JsonDocument.Parse("""
            { "data": { "address": "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa" } }
            """);

        var txId = BitailsRealtimePayloadParser.ExtractTxId(document.RootElement);

        Assert.Null(txId);
    }

    [Fact]
    public void TryExtractRawHex_FromDirectProperty()
    {
        using var document = JsonDocument.Parse("""
            { "rawtx": "0100000001c6f4b6176d3f4d6c6d9e198ba89a4eb7a1b08e6a705cc8cf0f8f2f3e3bcedf1f000000006b4830450221009af2d63b8ef3ebf8c7a227327d8e1a89f5929087566bbb6d6f74a09a87e2375d022007f8cefa32f6d829bb3f8792dd11e5d8f1cb4e4f4f84f7a8d431fed0b8ff103a4121022b698a0f0a1f1fb43fb8f33c2d72cbe7f3f8d98ef1a304681140f64e5681970fffffffff02e8030000000000001976a91489abcdefabbaabbaabbaabbaabbaabbaabbaabba88ac0000000000000000066a040102030400000000" }
            """);

        var ok = BitailsRealtimePayloadParser.TryExtractRawHex(document.RootElement, out var hex);

        Assert.True(ok);
        Assert.StartsWith("0100000001c6f4b617", hex);
    }

    [Fact]
    public void TryExtractBlockHash_FromNestedHash()
    {
        using var document = JsonDocument.Parse("""
            { "data": { "hash": "89abcdefabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba" } }
            """);

        var ok = BitailsRealtimePayloadParser.TryExtractBlockHash(document.RootElement, out var blockHash);

        Assert.True(ok);
        Assert.Equal("89abcdefabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba", blockHash);
    }

    [Fact]
    public void TryExtractRemoveReason_AndCollidedWithTransaction()
    {
        using var document = JsonDocument.Parse("""
            {
              "reason": "collision-in-block-tx",
              "collidedWith": { "txid": "89abcdefabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba" }
            }
            """);

        var reasonOk = BitailsRealtimePayloadParser.TryExtractRemoveReason(document.RootElement, out var reason);
        var collideOk = BitailsRealtimePayloadParser.TryExtractCollidedWithTransaction(document.RootElement, out var collidedWith);

        Assert.True(reasonOk);
        Assert.Equal("collision-in-block-tx", reason);
        Assert.True(collideOk);
        Assert.Equal("89abcdefabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabbaabba", collidedWith);
    }
}
