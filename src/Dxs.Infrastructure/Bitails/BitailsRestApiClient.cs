using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

using ComposableAsync;

using Dxs.Common.Exceptions;
using Dxs.Common.Extensions;
using Dxs.Common.Utils;
using Dxs.Infrastructure.Bitails.Dto;

using Polly;

using RateLimiter;

namespace Dxs.Infrastructure.Bitails;

// https://docs.bitails.io/
// https://api.bitails.io/swagger
public class BitailsRestApiClient : IBitailsRestApiClient
{
    private const int HistoryMaxCount = 5000;

    private readonly HttpClient _client;

    private static readonly TimeLimiter RateLimiter = TimeLimiter.GetFromMaxCountByInterval(10, TimeSpan.FromSeconds(1));

    public BitailsRestApiClient(HttpClient client)
    {
        _client = client;
        _client.BaseAddress = new Uri("https://api.bitails.io/");
        _client.Timeout = TimeSpan.FromSeconds(15);
    }

    public static IAsyncPolicy<HttpResponseMessage> HttpPolicy { get; } = Policy<HttpResponseMessage>
        .Handle<HttpRequestException>(e => e.InnerException is AuthenticationException or IOException)
        .RetryAsync(2);

    public async Task<HistoryPage> GetHistoryPageAsync(string address, string pgKey, int limit, CancellationToken token)
    {
        await RateLimiter;

        // Bitails has an issue if more than max limit is requested
        limit = Math.Min(limit, HistoryMaxCount);

        var query = new QueryBuilder
        {
            { "pgkey", pgKey },
            { "limit", limit }
        };

        return await _client.GetOrThrowAsync<HistoryPage>($"address/{address}/history?{query}", token);
    }

    public async Task<AddressDetailsDto> GetAddressDetailsAsync(string address, CancellationToken token = default)
    {
        await RateLimiter;

        return await _client.GetOrThrowAsync<AddressDetailsDto>($"address/{address}/details", token);
    }

    public async Task<BroadcastResponseDto> Broadcast(string txHex, CancellationToken token = default)
    {
        await RateLimiter;

        return await _client.PostOrThrowAsync<BroadcastResponseDto>(
            "tx/broadcast",
            new { raw = txHex },
            token
        );
    }

    // https://api.bitails.io/swagger/static/index.html#/Transaction/ApiTransactionController_get
    public async Task<bool> IsBroadcastedAsync(string txId, CancellationToken token = default)
    {
        await RateLimiter;

        var httpResponse = await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"download/tx/{txId}"),
            HttpCompletionOption.ResponseHeadersRead, token
        );

        return httpResponse switch
        {
            { StatusCode: HttpStatusCode.NotFound } => false,
            { IsSuccessStatusCode: true } => true,
            _ => throw await DetailedHttpRequestException.FromResponseAsync(httpResponse)
        };
    }

    public async Task<byte[]> GetTransactionRawOrNullAsync(string txId, CancellationToken token = default)
    {
        await RateLimiter;

        var message = new HttpRequestMessage(HttpMethod.Get, $"download/tx/{txId}");
        var httpResponse = await _client.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            token
        );

        return httpResponse switch
        {
            { StatusCode: HttpStatusCode.NotFound } => null,
            _ => await httpResponse.Content.ReadAsByteArrayAsync(token) //..ReadContentOrThrowAsync<string>(cancellationToken: token),
        };
    }

    public async Task<TransactionDetailsDto> GetTransactionDetails(string txId, CancellationToken token = default)
    {
        await RateLimiter;

        return await _client.GetOrThrowAsync<TransactionDetailsDto>($"tx/{txId}", token);
    }

    public async Task<OutputDetailsDto> GetOutputDetails(string txId, int vout, CancellationToken token = default)
    {
        await RateLimiter;

        return await _client.GetOrThrowAsync<OutputDetailsDto>($"tx/{txId}/output/{vout}", token);
    }

    public async Task<TokenDetailsDto> GetTokenDetails(string tokenId, string symbol, CancellationToken token = default)
    {
        await RateLimiter;

        return await _client.GetOrThrowAsync<TokenDetailsDto>($"token/{tokenId}/symbol/{symbol}", token);
    }
}
