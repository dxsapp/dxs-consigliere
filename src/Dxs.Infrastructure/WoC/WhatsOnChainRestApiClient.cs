using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using ComposableAsync;

using Dxs.Common.Exceptions;
using Dxs.Common.Extensions;
using Dxs.Infrastructure.Common;
using Dxs.Infrastructure.WoC.Dto;

using Newtonsoft.Json;

using RateLimiter;

namespace Dxs.Infrastructure.WoC;

// https://developers.whatsonchain.com/
public class WhatsOnChainRestApiClient : IWhatsOnChainRestApiClient
{
    private static class Internal
    {
        public class BalanceDto
        {
            [JsonProperty("confirmed")]
            public long ConfirmedSatoshis { get; init; }

            [JsonProperty("unconfirmed")]
            public long UnconfirmedSatoshis { get; init; }

            public decimal Total => (ConfirmedSatoshis + UnconfirmedSatoshis) / (decimal)CommonConstants.SatoshisInBsv;
        }

        public class AddressBalanceDto
        {
            [JsonProperty("address", Required = Required.Always)]
            public string Address { get; init; }

            [JsonProperty("balance", Required = Required.Always)]
            public BalanceDto Balance { get; init; }
        }
    }

    private static readonly TimeLimiter RateLimiter = TimeLimiter.GetFromMaxCountByInterval(3, TimeSpan.FromSeconds(1));
    private readonly HttpClient _client;
    private readonly IExternalChainProviderSettingsAccessor _providerSettingsAccessor;

    public WhatsOnChainRestApiClient(HttpClient client, IExternalChainProviderSettingsAccessor providerSettingsAccessor)
    {
        _client = client;
        _providerSettingsAccessor = providerSettingsAccessor;
    }

    public async Task<bool> IsBroadcastedAsync(string txId, CancellationToken token = default)
    {
        await RateLimiter;
        var request = await CreateRequestAsync(HttpMethod.Get, $"tx/hash/{txId}", token);
        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

        if (response.IsSuccessStatusCode)
            return true;
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        throw await DetailedHttpRequestException.FromResponseAsync(response);
    }

    public async Task<string> GetTransactionRawOrNullAsync(string txId, CancellationToken token = default)
    {
        await RateLimiter;
        var request = await CreateRequestAsync(HttpMethod.Get, $"tx/{txId}/hex", token);
        var response = await _client.SendAsync(request, token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw await DetailedHttpRequestException.FromResponseAsync(response);

        var raw = await response.Content.ReadAsStringAsync(token);
        return string.IsNullOrEmpty(raw) ? null : raw;
    }

    public async Task<IList<TransactionDetailsSlimDto>> GetTransactionsAsync(IEnumerable<string> txIds, CancellationToken token = default)
    {
        await RateLimiter;
        return await _client.PostOrThrowAsync<TransactionDetailsSlimDto[]>(
            await BuildUrlAsync("txs/hex", token),
            new { txids = txIds.ToHashSet() },
            await BuildHeadersAsync(token),
            token);
    }

    public async Task BroadcastAsync(string body, CancellationToken token = default)
    {
        await RateLimiter;
        await _client.PostOrThrowAsync<object>(
            await BuildUrlAsync("tx/raw", token),
            new { txhex = body },
            await BuildHeadersAsync(token),
            token);
    }

    public async Task<Dictionary<string, decimal>> GetBalancesAsync(IEnumerable<string> addresses, CancellationToken token = default)
    {
        await RateLimiter;
        var addressSet = new HashSet<string>(addresses);

        var result = await _client.PostOrThrowAsync<IList<Internal.AddressBalanceDto>>(
            await BuildUrlAsync("addresses/balance", token),
            new { addresses = addressSet },
            await BuildHeadersAsync(token),
            token);

        var balanceByAddress = result.ToDictionary(ab => ab.Address, ab => ab.Balance.Total);
        return addressSet.ToDictionary(a => a, a => balanceByAddress[a]);
    }

    public async Task<UnspentOutputDto[]> GetUtxosAsync(string address, int skip, int take, CancellationToken cancellationToken = default)
    {
        await RateLimiter;
        var result = await _client.GetOrThrowAsync<IList<UnspentOutputDto>>(
            await BuildUrlAsync($"address/{address}/unspent", cancellationToken),
            headers: await BuildHeadersAsync(cancellationToken),
            validateModel: true,
            token: cancellationToken);

        return result.Skip(skip)
            .Take(take)
            .ToArray();
    }

    public async Task<string[]> GetBlockPagesAsync(string hash, int number, CancellationToken token = default)
    {
        await RateLimiter;
        var request = await CreateRequestAsync(HttpMethod.Get, $"block/hash/{hash}/page/{number}", token);
        var response = await _client.SendAsync(request, token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw await DetailedHttpRequestException.FromResponseAsync(response);

        return await response.ReadContentOrThrowAsync<string[]>(false, token);
    }

    public async Task<BlockDto> GetBlockByHeightAsync(int height, CancellationToken token = default)
    {
        await RateLimiter;
        var request = await CreateRequestAsync(HttpMethod.Get, $"block/height/{height}", token);
        var response = await _client.SendAsync(request, token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw await DetailedHttpRequestException.FromResponseAsync(response);

        return await response.ReadContentOrThrowAsync<BlockDto>(false, token);
    }

    public async Task<TransactionDetailsDto> GetTransactionDetails(string transactionId, CancellationToken token = default)
    {
        await RateLimiter;
        var request = await CreateRequestAsync(HttpMethod.Get, $"tx/hash/{transactionId}", token);
        var response = await _client.SendAsync(request, token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.ReadContentOrThrowAsync<TransactionDetailsDto>(false, token);
    }

    public async Task<TokenDetailsDto> GetTokenDetails(string tokenId, string symbol, CancellationToken token = default)
    {
        await RateLimiter;
        var request = await CreateRequestAsync(HttpMethod.Get, $"token/{tokenId}/{symbol}", token);
        var response = await _client.SendAsync(request, token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw await DetailedHttpRequestException.FromResponseAsync(response);

        return await response.ReadContentOrThrowAsync<TokenDetailsDto>(cancellationToken: token);
    }

    public async Task<SpentTransactionOutput> GetSpentTransactionOutput(string transactionId, ulong vout, CancellationToken token = default)
    {
        await RateLimiter;
        var request = await CreateRequestAsync(HttpMethod.Get, $"tx/{transactionId}/{vout}/spent", token);
        var response = await _client.SendAsync(request, token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw await DetailedHttpRequestException.FromResponseAsync(response);

        return await response.ReadContentOrThrowAsync<SpentTransactionOutput>(cancellationToken: token);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string relativePath, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, await BuildUrlAsync(relativePath, cancellationToken));
        var headers = await BuildHeadersAsync(cancellationToken);
        if (headers is not null)
        {
            foreach (var header in headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return request;
    }

    private async Task<string> BuildUrlAsync(string relativePath, CancellationToken cancellationToken)
    {
        var settings = await _providerSettingsAccessor.GetWhatsOnChainAsync(cancellationToken);
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? "https://api.whatsonchain.com/v1/bsv/main"
            : settings.BaseUrl.Trim().TrimEnd('/');
        return $"{baseUrl}/{relativePath.TrimStart('/')}";
    }

    private async Task<Dictionary<string, string>> BuildHeadersAsync(CancellationToken cancellationToken)
    {
        var settings = await _providerSettingsAccessor.GetWhatsOnChainAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return null;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = settings.ApiKey
        };
    }
}
