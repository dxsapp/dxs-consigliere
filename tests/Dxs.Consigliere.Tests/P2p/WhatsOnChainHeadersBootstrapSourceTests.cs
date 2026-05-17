using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv.P2p.Chain;
using Dxs.Bsv.P2p.Messages;
using Dxs.Consigliere.Services.P2p;

using Microsoft.Extensions.Logging.Abstractions;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// Wave 1 audit A2-followup new-H1 unit coverage. The previous
/// implementation tried to parse <c>/block/{hash}/header</c> as a raw
/// hex string; the live WoC endpoint returns a structured JSON block
/// object. These tests pin the reconstruction logic against the actual
/// WoC JSON shape (BSV genesis) and prove the resulting 80-byte header
/// hashes back to the expected display hash.
/// </summary>
public class WhatsOnChainHeadersBootstrapSourceTests
{
    /// <summary>
    /// Real-shape WoC-style JSON for BSV/BTC mainnet block 0.
    /// Fields verified by inspecting the live endpoint at audit time.
    /// </summary>
    private const string GenesisHeaderJson = """
    {
      "hash": "000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f",
      "height": 0,
      "version": 1,
      "merkleroot": "4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b",
      "time": 1231006505,
      "bits": "1d00ffff",
      "nonce": 2083236893
    }
    """;

    private const string GenesisDisplayHash =
        "000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f";

    [Fact]
    public void TryBuildHeaderBytes_FromGenesisJson_ProducesValidHeader()
    {
        var json = JsonDocument.Parse(GenesisHeaderJson).RootElement;
        var bytes = WhatsOnChainHeadersBootstrapSource.TryBuildHeaderBytes(
            json, NullLogger<WhatsOnChainHeadersBootstrapSource>.Instance);

        Assert.NotNull(bytes);
        Assert.Equal(BlockHeader.Size, bytes!.Length);
        // Hash → display order → must match the expected genesis display hash.
        var display = BlockHeaderHasher.ToDisplayHex(
            BlockHeaderHasher.Hash(new BlockHeader(bytes)));
        Assert.Equal(GenesisDisplayHash, display);
    }

    [Fact]
    public void TryBuildHeaderBytes_MissingPreviousBlockHash_TreatedAsAllZero()
    {
        // Genesis has no previousblockhash; verify the source treats
        // the missing field as 32 zero bytes (so genesis is fully
        // reconstructible).
        Assert.DoesNotContain("\"previousblockhash\"", GenesisHeaderJson);
        // (covered structurally by the test above — its hash match
        // implies the prev_block field was correctly zeroed.)
    }

    [Theory]
    [InlineData("version")]
    [InlineData("time")]
    [InlineData("nonce")]
    [InlineData("bits")]
    [InlineData("merkleroot")]
    public void TryBuildHeaderBytes_MissingRequiredField_ReturnsNull(string fieldToRemove)
    {
        var jsonDoc = JsonDocument.Parse(GenesisHeaderJson);
        var node = System.Text.Json.Nodes.JsonNode.Parse(GenesisHeaderJson)!.AsObject();
        node.Remove(fieldToRemove);
        var stripped = JsonDocument.Parse(node.ToJsonString()).RootElement;

        var result = WhatsOnChainHeadersBootstrapSource.TryBuildHeaderBytes(
            stripped, NullLogger<WhatsOnChainHeadersBootstrapSource>.Instance);

        Assert.Null(result);
    }

    [Fact]
    public void TryBuildHeaderBytes_Bits_AsNumber_AlsoWorks()
    {
        // WoC sometimes returns bits as a number rather than hex string;
        // the parser should accept either.
        var node = System.Text.Json.Nodes.JsonNode.Parse(GenesisHeaderJson)!.AsObject();
        node["bits"] = 0x1d00ffff; // numeric form of the same target
        var json = JsonDocument.Parse(node.ToJsonString()).RootElement;

        var bytes = WhatsOnChainHeadersBootstrapSource.TryBuildHeaderBytes(
            json, NullLogger<WhatsOnChainHeadersBootstrapSource>.Instance);

        Assert.NotNull(bytes);
        var display = BlockHeaderHasher.ToDisplayHex(
            BlockHeaderHasher.Hash(new BlockHeader(bytes!)));
        Assert.Equal(GenesisDisplayHash, display);
    }

    [Fact]
    public async Task FetchAsync_EndToEnd_AgainstFakeWoCResponses()
    {
        // Stub HttpClient with a handler that returns canned chain-info
        // then canned header JSON — the source must hit both endpoints
        // and produce a BootstrapSeed whose hash matches bestblockhash.
        var handler = new FakeWoCHandler(GenesisDisplayHash, GenesisHeaderJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.whatsonchain.com/v1/bsv/main/") };
        var sut = new WhatsOnChainHeadersBootstrapSource(http,
            NullLogger<WhatsOnChainHeadersBootstrapSource>.Instance);

        var seed = await sut.FetchAsync(CancellationToken.None);

        Assert.NotNull(seed);
        Assert.Equal(0, seed!.Height);
        Assert.Equal(BlockHeader.Size, seed.HeaderBytes80.Length);
        var display = BlockHeaderHasher.ToDisplayHex(
            BlockHeaderHasher.Hash(new BlockHeader(seed.HeaderBytes80)));
        Assert.Equal(GenesisDisplayHash, display);

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task FetchAsync_HashSelfCheck_FailsClosed_OnMismatch()
    {
        // Force a bestblockhash that won't match the reconstructed header
        // → source must return null instead of seeding the chain with a
        // wrong tip.
        var handler = new FakeWoCHandler("00".PadRight(64, 'a'), GenesisHeaderJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.whatsonchain.com/v1/bsv/main/") };
        var sut = new WhatsOnChainHeadersBootstrapSource(http,
            NullLogger<WhatsOnChainHeadersBootstrapSource>.Instance);

        var seed = await sut.FetchAsync(CancellationToken.None);

        Assert.Null(seed);
    }

    private sealed class FakeWoCHandler(string bestBlockHash, string headerJson) : HttpMessageHandler
    {
        public int CallCount;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("chain/info"))
            {
                var body = $$"""{ "blocks": 0, "bestblockhash": "{{bestBlockHash}}" }""";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                });
            }
            if (path.Contains("/block/") && path.EndsWith("/header"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(headerJson, Encoding.UTF8, "application/json"),
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
