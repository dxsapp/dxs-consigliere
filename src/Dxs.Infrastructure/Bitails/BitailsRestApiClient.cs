using System;
using System.Collections.Generic;
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
using Dxs.Infrastructure.Common;

using Polly;

using RateLimiter;

namespace Dxs.Infrastructure.Bitails;

// https://docs.bitails.io/
// https://api.bitails.io/swagger
public class BitailsRestApiClient : IBitailsRestApiClient
{
    private const int HistoryMaxCount = 5000;
    private static readonly TimeLimiter RateLimiter = TimeLimiter.GetFromMaxCountByInterval(10, TimeSpan.FromSeconds(1));
    private readonly HttpClient _client;
    private readonly IExternalChainProviderSettingsAccessor _providerSettingsAccessor;

    public static IAsyncPolicy<HttpResponseMessage> HttpPolicy { get; } = Policy<HttpResponseMessage>
        .Handle<HttpRequestException>(e => e.InnerException is AuthenticationException or IOException)
        .RetryAsync(2);

    public BitailsRestApiClient(HttpClient client, IExternalChainProviderSettingsAccessor providerSettingsAccessor)
    {
        _client = client;
        _providerSettingsAccessor = providerSettingsAccessor;
        _client.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<HistoryPage> GetHistoryPageAsync(string address, string pgKey, int limit, CancellationToken token)
    {
        await RateLimiter;

        limit = Math.Min(limit, HistoryMaxCount);
        var query = new QueryBuilder
        {
            { "pgkey", pgKey },
            { "limit", limit }
        };

        return await _client.GetOrThrowAsync<HistoryPage>(
            await BuildUrlAsync($"address/{address}/history?{query}", token),
            headers: await BuildHeadersAsync(token),
            validateModel: true,
            token: token);
    }

    public async Task<AddressDetailsDto> GetAddressDetailsAsync(string address, CancellationToken token = default)
    {
        await RateLimiter;
        return await _client.GetOrThrowAsync<AddressDetailsDto>(
            await BuildUrlAsync($"address/{address}/details", token),
            headers: await BuildHeadersAsync(token),
            validateModel: true,
            token: token);
    }

    public async Task<BroadcastResponseDto> Broadcast(string txHex, CancellationToken token = default)
    {
        await RateLimiter;
        return await _client.PostOrThrowAsync<BroadcastResponseDto>(
            await BuildUrlAsync("tx/broadcast", token),
            new { raw = txHex },
            await BuildHeadersAsync(token),
            token);
    }

    public async Task<bool> IsBroadcastedAsync(string txId, CancellationToken token = default)
    {
        await RateLimiter;

        var message = new HttpRequestMessage(HttpMethod.Get, await BuildUrlAsync($"download/tx/{txId}", token));
        ApplyHeaders(message, await BuildHeadersAsync(token));
        var httpResponse = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);

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

        var message = new HttpRequestMessage(HttpMethod.Get, await BuildUrlAsync($"download/tx/{txId}", token));
        ApplyHeaders(message, await BuildHeadersAsync(token));
        var httpResponse = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);

        return httpResponse switch
        {
            { StatusCode: HttpStatusCode.NotFound } => null,
            _ => await httpResponse.Content.ReadAsByteArrayAsync(token)
        };
    }

    public async Task<TransactionDetailsDto> GetTransactionDetails(string txId, CancellationToken token = default)
    {
        await RateLimiter;
        return await _client.GetOrThrowAsync<TransactionDetailsDto>(
            await BuildUrlAsync($"tx/{txId}", token),
            headers: await BuildHeadersAsync(token),
            validateModel: true,
            token: token);
    }

    public async Task<OutputDetailsDto> GetOutputDetails(string txId, int vout, CancellationToken token = default)
    {
        await RateLimiter;
        return await _client.GetOrThrowAsync<OutputDetailsDto>(
            await BuildUrlAsync($"tx/{txId}/output/{vout}", token),
            headers: await BuildHeadersAsync(token),
            validateModel: true,
            token: token);
    }

    public async Task<TokenDetailsDto> GetTokenDetails(string tokenId, string symbol, CancellationToken token = default)
    {
        await RateLimiter;
        return await _client.GetOrThrowAsync<TokenDetailsDto>(
            await BuildUrlAsync($"token/{tokenId}/symbol/{symbol}", token),
            headers: await BuildHeadersAsync(token),
            validateModel: true,
            token: token);
    }

    private async Task<string> BuildUrlAsync(string relativePath, CancellationToken cancellationToken)
    {
        var settings = await _providerSettingsAccessor.GetBitailsAsync(cancellationToken);
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? "https://api.bitails.io"
            : settings.BaseUrl.Trim().TrimEnd('/');
        return $"{baseUrl}/{relativePath.TrimStart('/')}";
    }

    private async Task<Dictionary<string, string>> BuildHeadersAsync(CancellationToken cancellationToken)
    {
        var settings = await _providerSettingsAccessor.GetBitailsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return null;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["apikey"] = settings.ApiKey
        };
    }

    private static void ApplyHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string> headers)
    {
        if (headers is null)
            return;

        foreach (var header in headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
    }
}
