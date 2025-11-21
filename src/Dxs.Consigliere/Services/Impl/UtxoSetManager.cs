using Dxs.Bsv;
using Dxs.Bsv.Factories;
using Dxs.Bsv.Models;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Queries;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Extensions;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Services.Impl;

public class UtxoSetManager(IDocumentStore documentStore, INetworkProvider networkProvider):
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

        using var session = documentStore.GetNoCacheNoTrackingSession();

        return await GetBalance(session, addresses, tokenIds);
    }

    public Task<List<BalanceDto>> GetBalance(IAsyncDocumentSession session, BalanceRequest request)
    {
        var addresses = request.Addresses
            .Select(addressStr => addressStr.EnsureValidBsvAddress().Value)
            .ToList();

        var tokenIds = request
            .TokenIds?
            .Select(tokenIdStr => tokenIdStr.EnsureValidTokenId(networkProvider.Network).Value)
            .ToList();

        return GetBalance(session, addresses, tokenIds);
    }

    public async Task<List<BalanceDto>> GetStasBalances(
        IAsyncDocumentSession session,
        IList<string> addresses,
        IList<string> tokenIds
    )
    {
        var query = session
            .StasUtxoSet()
            .ByTokens(tokenIds)
            .ByAddresses(addresses)
            .Select(x => new BalanceDto
            {
                Address = x.Address,
                TokenId = x.TokenId,
                Satoshis = x.Satoshis
            });

        // way ineffective implementation
        var byAddressAndToken = new Dictionary<string, Dictionary<string, BalanceDto>>();

        await foreach (var (utxo, _) in session.Enumerate(query))
        {
            if (!byAddressAndToken.TryGetValue(utxo.Address, out var values))
            {
                values = new Dictionary<string, BalanceDto>();
                byAddressAndToken[utxo.Address] = values;
            }

            if (!values.TryAdd(utxo.TokenId, utxo))
                values[utxo.TokenId].Satoshis += utxo.Satoshis;
        }

        return byAddressAndToken
            .SelectMany(x => x.Value.Values)
            .ToList();
    }

    public async Task<GetUtxoSetResponse> GetUtxoSet(GetUtxoSetRequest request)
    {
        var address = request.Address.EnsureValidBsvAddress();
        var tokenId = request.TokenId?.EnsureValidTokenId(networkProvider.Network);

        var utxoSet = request.Satoshis is {} satoshis and > 0
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

        using var session = documentStore.GetNoCacheNoTrackingSession();

        var query = tokenIds?.Count > 0
            ? session.StasUtxoSet().ByTokens(tokenIds)
            : session.P2pkhUtxoSet();

        var utxoSet = await query.ByAddresses(addresses)
            .Take(1000)
            .Select(x => new UtxoDto
            {
                Id = x.Id,
                Address = x.Address,
                TokenId = x.TokenId,
                Satoshis = x.Satoshis,
                TxId = x.TxId,
                Vout = x.Vout,
                ScriptType = x.Type,
                ScriptPubKey = x.ScriptPubKey

            })
            .ToArrayAsync();

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
        using var session = documentStore.GetNoCacheNoTrackingSession();

        var outputs = await session
            .UtxoSet(tokenId)
            .ByAddress(address)
            .ToListAsync();

        return outputs.Select(x => new OutPoint(
                x.TxId,
                new Address(x.Address),
                x.TokenId,
                (ulong)x.Satoshis,
                (uint)x.Vout,
                x.ScriptPubKey,
                x.Type
            ))
            .ToList();
    }

    #region .pvt

    private async Task<List<BalanceDto>> GetBalance(
        IAsyncDocumentSession session,
        List<string> addresses,
        List<string> tokenIds
    )
    {
        var balances = await session
            .GetP2PkhBalances(addresses)
            .ToListAsync();

        if (tokenIds?.Count > 0)
        {
            var stasBalances = await GetStasBalances(session, addresses, tokenIds);

            return balances.Concat(stasBalances).ToList();
        }

        return balances;
    }

    private async Task<UtxoDto[]> GetUtxoSet(Address address, TokenId tokenId)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();

        var query = session.UtxoSet(tokenId).ByAddress(address);
        List<UtxoDto> result = [];

        await foreach (var (utxo, totalCount) in session.Enumerate(query))
        {
            result ??= new(totalCount);

            result.Add(new UtxoDto(utxo));
        }

        return result.ToArray();
    }

    private async Task<UtxoDto[]> GetUtxoSet(Address address, TokenId tokenId, long satoshis)
    {
        using var session = documentStore.GetNoCacheNoTrackingSession();

        var query = session
                .UtxoSet(tokenId)
                .ByAddress(address)
            ;

        var utxo = await query
            .Where(x => x.Satoshis >= satoshis)
            .OrderBy(x => x.Satoshis)
            .Take(1)
            .FirstOrDefaultAsync();

        UtxoDto[] singleUtxoResult = [];

        if (utxo != null)
        {
            singleUtxoResult = [new UtxoDto(utxo)];

            if (utxo.Satoshis == satoshis) // UTXO with exact amount
                return singleUtxoResult;
        }

        var reversedQuery = query
            .Where(x => x.Satoshis < satoshis)
            .OrderByDescending(x => x.Satoshis);

        await using var stream = await session.Advanced.StreamAsync(reversedQuery);

        var accumulatedSatoshis = 0L;
        List<UtxoDto> utxoSet = null;

        await foreach (var (output, totalCount) in session.Enumerate(query))
        {
            utxoSet ??= new(totalCount);
            utxoSet.Add(new UtxoDto(output));

            accumulatedSatoshis += output.Satoshis;

            if (accumulatedSatoshis >= satoshis)
            {
                // UTXOs which should be merged
                return utxoSet.ToArray();
            }
        }

        return singleUtxoResult;
    }

    #endregion

}