using Dxs.Bsv;
using Dxs.Bsv.Factories;
using Dxs.Bsv.Models;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Queries;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Services.Impl;

public class UtxoSetManager(
    IDocumentStore documentStore,
    INetworkProvider networkProvider,
    AddressProjectionReader addressProjectionReader,
    AddressProjectionRebuilder addressProjectionRebuilder
) :
    IUtxoManager,
    IUtxoSetProvider
{
    public async Task<List<BalanceDto>> GetBalance(BalanceRequest request)
    {
        var addresses = request.Addresses
            .Select(addressStr => addressStr.EnsureValidBsvAddress().Value)
            .ToList();

        var tokenIds = request
            .TokenIds?
            .Select(tokenIdStr => tokenIdStr.EnsureValidTokenId(networkProvider.Network).Value)
            .ToList();

        await addressProjectionRebuilder.RebuildAsync();

        return await GetBalance(addresses, tokenIds);
    }

    public async Task<List<BalanceDto>> GetBalance(IAsyncDocumentSession session, BalanceRequest request)
    {
        var addresses = request.Addresses
            .Select(addressStr => addressStr.EnsureValidBsvAddress().Value)
            .ToList();

        var tokenIds = request
            .TokenIds?
            .Select(tokenIdStr => tokenIdStr.EnsureValidTokenId(networkProvider.Network).Value)
            .ToList();

        await addressProjectionRebuilder.RebuildAsync();

        return await GetBalance(addresses, tokenIds);
    }

    public async Task<GetUtxoSetResponse> GetUtxoSet(GetUtxoSetRequest request)
    {
        var address = request.Address.EnsureValidBsvAddress();
        var tokenId = request.TokenId?.EnsureValidTokenId(networkProvider.Network);

        await addressProjectionRebuilder.RebuildAsync();

        var utxoSet = request.Satoshis is { } satoshis and > 0
            ? await GetUtxoSet(address, tokenId, satoshis)
            : await GetUtxoSet(address, tokenId);

        return new GetUtxoSetResponse(utxoSet);
    }

    public async Task<GetUtxoSetResponse> GetUtxoSet(GetUtxoSetBatchRequest request)
    {
        var addresses = request.Addresses
            .Select(addressStr => addressStr.EnsureValidBsvAddress().Value)
            .ToList();

        var tokenIds = request
            .TokenIds?
            .Select(tokenIdStr => tokenIdStr.EnsureValidTokenId(networkProvider.Network).Value)
            .ToList();

        await addressProjectionRebuilder.RebuildAsync();

        var utxoSet = tokenIds?.Count > 0
            ? await addressProjectionReader.LoadTokenUtxosAsync(addresses, tokenIds, 1000)
            : await addressProjectionReader.LoadP2pkhUtxosAsync(addresses, 1000);

        return new(utxoSet);
    }

    public async Task<(decimal supply, decimal toBurn)> GetTokenStats(
        TokenId tokenId,
        TokenSchema tokenSchema,
        CancellationToken cancellationToken
    )
    {
        var toBurn = 0L;
        var supply = 0L;

        using var session = documentStore.GetNoCacheNoTrackingSession();

        var query = session.StasUtxoSet().ByToken(tokenId);

        await foreach (var (utxo, _) in session.Enumerate(query).WithCancellation(cancellationToken))
        {
            if (utxo.Address == tokenId.RedeemAddress.Value)
                toBurn += utxo.Satoshis;
            else
                supply += utxo.Satoshis;
        }

        return (
            tokenSchema.SatoshisToToken(supply),
            tokenSchema.SatoshisToToken(toBurn)
        );
    }

    async Task<IList<OutPoint>> IUtxoSetProvider.GetUtxoSet(Address address, TokenId tokenId)
    {
        await addressProjectionRebuilder.RebuildAsync();

        var outputs = await addressProjectionReader.LoadUtxosAsync(address.Value, tokenId?.Value);

        return outputs.Select(x => new OutPoint(
                x.TxId,
                new Address(x.Address),
                x.TokenId,
                (ulong)x.Satoshis,
                (uint)x.Vout,
                x.ScriptPubKey,
                x.ScriptType
            ))
            .ToList();
    }

    #region .pvt

    private async Task<List<BalanceDto>> GetBalance(
        List<string> addresses,
        List<string> tokenIds
    )
    {
        var balances = await addressProjectionReader.LoadBsvBalancesAsync(addresses);

        if (tokenIds?.Count > 0)
        {
            var stasBalances = await addressProjectionReader.LoadTokenBalancesAsync(addresses, tokenIds);
            return balances.Concat(stasBalances).ToList();
        }

        return balances;
    }

    private async Task<UtxoDto[]> GetUtxoSet(Address address, TokenId tokenId)
    {
        var outputs = await addressProjectionReader.LoadUtxosAsync(address.Value, tokenId?.Value);
        return outputs.Select(x => x.ToDto()).ToArray();
    }

    private async Task<UtxoDto[]> GetUtxoSet(Address address, TokenId tokenId, long satoshis)
    {
        var outputs = await addressProjectionReader.LoadUtxosAsync(address.Value, tokenId?.Value);

        var utxo = outputs
            .Where(x => x.Satoshis >= satoshis)
            .OrderBy(x => x.Satoshis)
            .FirstOrDefault();

        UtxoDto[] singleUtxoResult = [];

        if (utxo != null)
        {
            singleUtxoResult = [utxo.ToDto()];

            if (utxo.Satoshis == satoshis)
                return singleUtxoResult;
        }

        var accumulatedSatoshis = 0L;
        List<UtxoDto> utxoSet = null;

        foreach (var output in outputs.Where(x => x.Satoshis < satoshis).OrderByDescending(x => x.Satoshis))
        {
            utxoSet ??= [];
            utxoSet.Add(output.ToDto());

            accumulatedSatoshis += output.Satoshis;

            if (accumulatedSatoshis >= satoshis)
                return utxoSet.ToArray();
        }

        return singleUtxoResult;
    }

    #endregion
}
