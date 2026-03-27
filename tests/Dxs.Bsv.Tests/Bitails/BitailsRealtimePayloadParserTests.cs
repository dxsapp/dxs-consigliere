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
}
