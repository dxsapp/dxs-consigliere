using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dxs.Bsv.Rpc.Configs;
using Dxs.Bsv.Rpc.Models;
using Dxs.Bsv.Rpc.Models.Responses;
using Dxs.Bsv.Rpc.Streams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Bsv.Rpc.Services.Impl;

public class RpcClient(
    IOptions<RpcConfig> rpcConfig,
    HttpClient httpClient,
    ILogger<RpcClient> logger
) : IRpcClient
{
    private static class Methods
    {
        internal const string Help = "help";
        internal const string GetBlock = "getblock";
        internal const string GetBlockCount = "getblockcount";
        internal const string GetBlockHash = "getblockhash";
        internal const string GetBlockHeader = "getblockheader";
        internal const string GetBlockChainInfo = "getblockchaininfo";
        internal const string GetRawMemPool = "getrawmempool";
        internal const string SendRawTransaction = "sendrawtransaction";
        internal const string GetRawTransaction = "getrawtransaction";
        internal const string GetChainTips = "getchaintips";
    }

    private readonly RpcConfig _rpcConfig = rpcConfig.Value;
    private readonly ILogger _logger = logger;

    public Task<RpcResponseString> Help(string command)
    {
        return SendMethodAsString<RpcResponseString>(Methods.Help, command!);
    }

    public Task<RpcResponseString> GetBlockAsString(string blockHash)
    {
        return SendMethodAsString<RpcResponseString>(Methods.GetBlock, blockHash, false);
    }

    public async Task<Stream> GetBlockAsStream(string blockHash)
    {
        var networkStreamInternal = await SendMethodAsStream(Methods.GetBlock, blockHash, 0);

        return new JsonRpcResultNetworkStream(networkStreamInternal);
    }

    public Task<RpcResponseInt> GetBlockCount()
    {
        return SendMethodAsString<RpcResponseInt>(Methods.GetBlockCount);
    }

    public Task<RpcResponseString> GetBlockHash(int height)
    {
        return SendMethodAsString<RpcResponseString>(Methods.GetBlockHash, height);
    }

    public Task<RpcGetBlockHeaderResponse> GetBlockHeader(string hash)
    {
        return SendMethodAsString<RpcGetBlockHeaderResponse>(Methods.GetBlockHeader, hash);
    }

    public Task<RpcGetBlockChainInfoResponse> GetBlockChainInfo()
    {
        return SendMethodAsString<RpcGetBlockChainInfoResponse>(Methods.GetBlockChainInfo);
    }

    public Task<RpcRawMemPoolResponse> GetRawMemPool()
    {
        return SendMethodAsString<RpcRawMemPoolResponse>(Methods.GetRawMemPool, true);
    }

    public Task<RpcResponseStringWithErrorDetails> SendRawTransaction(string hexRawTx, bool allowHighFees = false,
        bool dontCheckFee = false)
    {
        return SendMethodAsString<RpcResponseStringWithErrorDetails>(Methods.SendRawTransaction, hexRawTx,
            allowHighFees, dontCheckFee);
    }

    public Task<RpcResponseStringWithErrorDetails> GetRawTransactionAsString(string txId)
    {
        /*
            Arguments:
                1. \"txid\"      (string, required) The transaction id
                2. verbose       (bool, optional, default=false) If false, return a string, otherwise return a json object
         */
        return SendMethodAsString<RpcResponseStringWithErrorDetails>(Methods.GetRawTransaction, txId, false);
    }

    public Task<RpcGetRawTxResponse> GetRawTransactionAsJsonObject(string txId)
    {
        /*
            Arguments:
                1. \"txid\"      (string, required) The transaction id
                2. verbose       (bool, optional, default=false) If false, return a string, otherwise return a json object
         */
        return SendMethodAsString<RpcGetRawTxResponse>(Methods.GetRawTransaction, txId, true);
    }

    public Task<ChainTipsResponse> GetChainTips()
        => SendMethodAsString<ChainTipsResponse>(Methods.GetChainTips);

    private async Task<TRpcResponse> SendMethodAsString<TRpcResponse>(string method, params object[] parameters)
    {
        var webRequest = BuildRequestMessage(method, parameters);
        var response = await httpClient.SendAsync(webRequest);
        var str = await response.Content.ReadAsStringAsync();

        try
        {
            return JsonSerializer.Deserialize<TRpcResponse>(str);
        }
        catch (Exception exception)
        {

            _logger.LogError(
                 exception,
                "RPC failed call {Method}; {Params}; {Response}",
                method, parameters, str
            );

            throw;
        }
    }

    private async Task<Stream> SendMethodAsStream(string method, params object[] @params)
    {
        if (method == null)
            throw new ArgumentNullException(nameof(method));

        var webRequest = BuildRequestMessage(method, @params);
        var response = await httpClient.SendAsync(webRequest, HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();

        return stream;
    }

    private HttpRequestMessage BuildRequestMessage(string method, object[] @params)
    {
        var request = new RpcRequest
        {
            MethodName = method,
        };

        if (@params is not null)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            var objects = @params.Where(p => p is not null).ToArray();

            if (objects.Length > 0)
            {
                request.Params = objects;
            }
        }

        var payload = JsonSerializer.Serialize(request);
        var webRequest = new HttpRequestMessage(HttpMethod.Post, _rpcConfig.BaseUrl)
        {
            Version = HttpVersion.Version20,
        };

        webRequest.Headers.Authorization = GetAuthHeader();
        webRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json-rpc");

        return webRequest;
    }

    private AuthenticationHeaderValue _authHeader;

    private AuthenticationHeaderValue GetAuthHeader()
    {
        if (_authHeader is { })
            return _authHeader;

        var bytes = Encoding.UTF8.GetBytes($"{_rpcConfig.User}:{_rpcConfig.Password}");
        var base64String = Convert.ToBase64String(bytes);

        _authHeader = new AuthenticationHeaderValue("Basic", base64String);

        return _authHeader;
    }
}