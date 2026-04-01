using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;

using Dxs.Infrastructure.Common;
using Dxs.Infrastructure.JungleBus;

namespace Dxs.Bsv.Tests.JungleBus;

public class JungleBusRawTransactionClientTests
{
    [Fact]
    public async Task GetTransactionRawOrNullAsync_ThrottlesToTenRequestsPerSecond()
    {
        var client = new JungleBusRawTransactionClient(
            new HttpClient(new RecordingHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"id\":\"tx-1\",\"block_hash\":\"block-hash\",\"block_height\":123,\"block_index\":4,\"block_time\":1710200500,\"transaction\":\"AQI=\",\"merkle_proof\":\"\"}",
                        Encoding.UTF8,
                        "application/json")
                })),
            new FakeSettingsAccessor());

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 11; i++)
        {
            var raw = await client.GetTransactionRawOrNullAsync($"tx-{i}");
            Assert.Equal(new byte[] { 0x01, 0x02 }, raw);
        }
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed >= TimeSpan.FromMilliseconds(900),
            $"Expected 11 sequential JungleBus raw tx requests to take at least ~1 second, but took {stopwatch.Elapsed}.");
    }

    private sealed class FakeSettingsAccessor : IExternalChainProviderSettingsAccessor
    {
        public ValueTask<BitailsProviderRuntimeSettings> GetBitailsAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new BitailsProviderRuntimeSettings(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));

        public ValueTask<WhatsOnChainProviderRuntimeSettings> GetWhatsOnChainAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new WhatsOnChainProviderRuntimeSettings(string.Empty, string.Empty));

        public ValueTask<JungleBusProviderRuntimeSettings> GetJungleBusAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new JungleBusProviderRuntimeSettings(string.Empty, string.Empty, string.Empty, string.Empty));
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
