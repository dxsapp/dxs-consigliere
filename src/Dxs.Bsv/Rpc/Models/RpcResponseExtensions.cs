using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Dxs.Bsv.Rpc.Models.Responses;

namespace Dxs.Bsv.Rpc.Models;

public static class RpcResponseExtensions
{
    public static async Task<string> EnsureSuccess(this Task<RpcResponseString> task)
    {
        var result = await task;
        EnsureSuccess(result);
        return result.Result;
    }

    public static async Task<int> EnsureSuccess(this Task<RpcResponseInt> task)
    {
        var result = await task;
        EnsureSuccess(result);
        return result.Result;
    }

    public static async Task<IDictionary<string, TxInfo>> EnsureSuccess(this Task<RpcRawMemPoolResponse> task)
    {
        var result = await task;
        EnsureSuccess(result.Error);
        return result.Result;
    }

    public static async Task<ResultJsonObject> EnsureSuccess(this Task<RpcGetRawTxResponse> task)
    {
        var result = await task;
        EnsureSuccess(result.Error);
        return result.Result;
    }

    public static async Task<string> EnsureSuccess(this Task<RpcResponseStringWithErrorDetails> task)
    {
        var result = await task;
        EnsureSuccess(result.Error);
        return result.Result;
    }

    public static async Task<ResultObject> EnsureSuccess(this Task<RpcGetBlockChainInfoResponse> task)
    {
        var result = await task;
        EnsureSuccess(result.Error);
        return result.Result;
    }

    private static void EnsureSuccess<TResult>(RpcResponseBase<TResult, string> result)
    {
        if (result.Error != null)
        {
            throw GetException(result.Error);
        }
    }

    private static void EnsureSuccess(CodeAndMessageErrorResponse error)
    {
        if (error != null)
        {
            throw GetException($"(code: {error.Code}, message: {error.Message})");
        }
    }

    private static InvalidOperationException GetException(string message)
        => new($"Request failed: {message}");
}
