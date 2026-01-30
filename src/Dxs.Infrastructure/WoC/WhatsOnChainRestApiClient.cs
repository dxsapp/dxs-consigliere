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
public class WhatsOnChainRestApiClient(HttpClient client) : IWhatsOnChainRestApiClient
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

    // https://developers.whatsonchain.com/#rate-limits
    private static readonly TimeLimiter RateLimiter = TimeLimiter.GetFromMaxCountByInterval(3, TimeSpan.FromSeconds(1));
    private const string Network = "main";

    public async Task<bool> IsBroadcastedAsync(string txId, CancellationToken token = default)
    {
        await RateLimiter;
        var response = await client.GetAsync(
            $"https://api.whatsonchain.com/v1/bsv/{Network}/tx/hash/{txId}",
            HttpCompletionOption.ResponseHeadersRead, token
        );

        if (response.IsSuccessStatusCode)
            return true;
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        throw await DetailedHttpRequestException.FromResponseAsync(response);
    }

    public async Task<string> GetTransactionRawOrNullAsync(string txId, CancellationToken token = default)
    {
        await RateLimiter;
        var response = await client.GetAsync($"https://api.whatsonchain.com/v1/bsv/{Network}/tx/{txId}/hex", token);

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
        var response = await client.PostOrThrowAsync<TransactionDetailsSlimDto[]>(
            $"https://api.whatsonchain.com/v1/bsv/{Network}/txs/hex",
            new { txids = txIds.ToHashSet() }, token
        );

        return response;
    }

    public async Task BroadcastAsync(string body, CancellationToken token = default)
    {
        await RateLimiter;
        var res = await client.PostOrThrowAsync<object>($"https://api.whatsonchain.com/v1/bsv/{Network}/tx/raw",
            new { txhex = body }, token
        );
    }

    public async Task<Dictionary<string, decimal>> GetBalancesAsync(IEnumerable<string> addresses, CancellationToken token = default)
    {
        await RateLimiter;
        var addressSet = new HashSet<string>(addresses);

        var result = await client.PostOrThrowAsync<IList<Internal.AddressBalanceDto>>(
            $"https://api.whatsonchain.com/v1/bsv/{Network}/addresses/balance",
            new { addresses = addressSet }, token
        );

        var balanceByAddress = result.ToDictionary(ab => ab.Address, ab => ab.Balance.Total);
        return addressSet.ToDictionary(a => a, a => balanceByAddress[a]);
    }

    public async Task<UnspentOutputDto[]> GetUtxosAsync(string address, int skip, int take, CancellationToken cancellationToken = default)
    {
        await RateLimiter;
        var result = await client.GetOrThrowAsync<IList<UnspentOutputDto>>(
            $"https://api.whatsonchain.com/v1/bsv/main/address/{address}/unspent",
            cancellationToken
        );

        return result.Skip(skip)
            .Take(take)
            .ToArray();
    }

    public async Task<string[]> GetBlockPagesAsync(string hash, int number, CancellationToken token = default)
    {
        await RateLimiter;
        var response = await client.GetAsync($"https://api.whatsonchain.com/v1/bsv/{Network}/block/hash/{hash}/page/{number}", token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw await DetailedHttpRequestException.FromResponseAsync(response);

        var txs = await response.ReadContentOrThrowAsync<string[]>(false, token);
        return txs;
    }

    public async Task<BlockDto> GetBlockByHeightAsync(int height, CancellationToken token = default)
    {
        await RateLimiter;
        var response = await client.GetAsync($"https://api.whatsonchain.com/v1/bsv/{Network}/block/height/{height}", token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw await DetailedHttpRequestException.FromResponseAsync(response);

        var block = await response.ReadContentOrThrowAsync<BlockDto>(false, token);
        return block;
    }

    public async Task<TransactionDetailsDto> GetTransactionDetails(string transactionId, CancellationToken token = default)
    {
        await RateLimiter;

        var response = await client.GetAsync($"https://api.whatsonchain.com/v1/bsv/main/tx/hash/{transactionId}", token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.ReadContentOrThrowAsync<TransactionDetailsDto>(false, token);
    }

    public async Task<TokenDetailsDto> GetTokenDetails(string tokenId, string symbol, CancellationToken token = default)
    {
        await RateLimiter;

        var response = await client.GetAsync($"https://api.whatsonchain.com/v1/bsv/main/token/{tokenId}/{symbol}", token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw await DetailedHttpRequestException.FromResponseAsync(response);

        return await response.ReadContentOrThrowAsync<TokenDetailsDto>(cancellationToken: token);
    }

    public async Task<SpentTransactionOutput> GetSpentTransactionOutput(string transactionId, ulong vout, CancellationToken token = default)
    {
        await RateLimiter;

        var response = await client.GetAsync($"https://api.whatsonchain.com/v1/bsv/main/tx/{transactionId}/{vout}/spent", token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw await DetailedHttpRequestException.FromResponseAsync(response);

        return await response.ReadContentOrThrowAsync<SpentTransactionOutput>(cancellationToken: token);
    }
}
