using System.IO;

using Dxs.Bsv.Rpc.Models.Responses;
using Dxs.Bsv.Rpc.Services;
using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Tests.Shared;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Controllers;

public class AdminControllerSyncStatusTests : RavenTestDriver
{
    [Fact]
    public async Task GetSyncState_ReturnsUnsyncedWhenRpcIsUnavailable()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var controller = new AdminController(new TestNetworkProvider());

        var result = await controller.GetSyncState(
            store,
            new ThrowingRpcClient(),
            NullLogger<AdminController>.Instance);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<SyncStatusResponse>(ok.Value);
        Assert.Equal(0, payload.Height);
        Assert.False(payload.IsSynced);
    }

    private sealed class TestNetworkProvider : Dxs.Consigliere.Services.INetworkProvider
    {
        public Dxs.Bsv.Network Network => Dxs.Bsv.Network.Mainnet;
    }

    private sealed class ThrowingRpcClient : IRpcClient
    {
        public Task<RpcResponseInt> GetBlockCount() => throw new UriFormatException("Invalid URI: The hostname could not be parsed.");
        public Task<RpcResponseString> GetBlockHash(int height) => throw new NotSupportedException();
        public Task<RpcResponseString> Help(string? command = default) => throw new NotSupportedException();
        public Task<RpcResponseString> GetBlockAsString(string blockHash) => throw new NotSupportedException();
        public Task<Stream> GetBlockAsStream(string blockHash) => throw new NotSupportedException();
        public Task<RpcGetBlockHeaderResponse> GetBlockHeader(string hash) => throw new NotSupportedException();
        public Task<RpcGetBlockChainInfoResponse> GetBlockChainInfo() => throw new NotSupportedException();
        public Task<RpcRawMemPoolResponse> GetRawMemPool() => throw new NotSupportedException();
        public Task<RpcResponseStringWithErrorDetails> SendRawTransaction(string hexRawTx, bool allowHighFees = false, bool dontCheckFee = false) => throw new NotSupportedException();
        public Task<RpcResponseStringWithErrorDetails> GetRawTransactionAsString(string txId) => throw new NotSupportedException();
        public Task<RpcGetRawTxResponse> GetRawTransactionAsJsonObject(string txId) => throw new NotSupportedException();
        public Task<ChainTipsResponse> GetChainTips() => throw new NotSupportedException();
    }
}
