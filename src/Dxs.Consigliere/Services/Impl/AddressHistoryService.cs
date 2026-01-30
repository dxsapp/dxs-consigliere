using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;
using Dxs.Common.Extensions;
using Dxs.Consigliere.Data.Indexes;
using Dxs.Consigliere.Data.Models.History;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Extensions;

using Microsoft.Extensions.Logging;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Dxs.Consigliere.Services.Impl;

public class AddressHistoryService : IAddressHistoryService, IDisposable
{
    private readonly IDocumentStore _documentStore;
    private readonly IConnectionManager _connectionManager;
    private readonly INetworkProvider _networkProvider;
    private readonly ILogger _logger;

    private readonly IDisposable _subscription;

    public AddressHistoryService(
        IDocumentStore documentStore,
        IFilteredTransactionMessageBus filteredTransactionMessageBus,
        IConnectionManager connectionManager,
        INetworkProvider networkProvider,
        ILogger<AddressHistoryService> logger
    )
    {
        _documentStore = documentStore;
        _connectionManager = connectionManager;
        _networkProvider = networkProvider;
        _logger = logger;

        _subscription = filteredTransactionMessageBus.SubscribeAsync(OnTransactionFound, OnTransactionFoundError);
    }

    public async Task<AddressHistoryResponse> GetHistory(GetAddressHistoryRequest request)
    {
        using var session = _documentStore.GetNoCacheNoTrackingSession();

        return await GetHistory(session, request);
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }

    #region pvt.

    private async Task<AddressHistoryResponse> GetHistory(IAsyncDocumentSession session, GetAddressHistoryRequest request)
    {
        if (request.Take > 100)
            throw new Exception($"Requested page is too big, max per request: {100} < {request.Take}");

        var address = request.Address.EnsureValidBsvAddress();

        List<TokenId> tokenIds = null;
        var bsvTokenRequested = false;

        if (request.TokenIds != null)
        {
            tokenIds = [];
            foreach (var tokenIdStr in request.TokenIds)
            {
                if (tokenIdStr.Equals("bsv", StringComparison.InvariantCultureIgnoreCase))
                {
                    bsvTokenRequested = true;
                    continue;
                }

                tokenIds.Add(tokenIdStr.EnsureValidTokenId(_networkProvider.Network));
            }
        }

        var result = new AddressHistoryResponse();
        var query = session
            .Query<AddressHistory, AddressHistoryIndex>()
            .Statistics(out var statistics);

        query = query.Where(x => x.Address == address.Value);

        if (tokenIds == null)
        {
            query = query.Where(x => x.TokenId == null);
        }
        else
        {
            var tokenValues = tokenIds
                .Select(x => x.Value)
                .ToList();
            query = bsvTokenRequested
                ? query.Where(x => x.TokenId.In(tokenValues) || x.TokenId == null)
                : query.Where(x => x.TokenId.In(tokenValues));
        }

        if (request.SkipZeroBalance)
            query = query.Where(x => x.BalanceSatoshis != 0);

        query = request.Desc
            ? query.OrderByDescending(x => x.Timestamp)
            : query.OrderBy(x => x.Timestamp);

        var data = await query
            .Skip(request.Skip)
            .Take(request.Take)
            .ToListAsync();

        result.History = data.Select(AddressHistoryDto.From).ToArray();
        result.TotalCount = (int)statistics.TotalResults;

        return result;
    }

    private async Task OnTransactionFound(FilteredTransactionMessage message)
    {
        var transaction = message.Transaction;
        var note = (string)null;
        var isKnownStasToken = (bool?)null;

        foreach (var output in transaction.Outputs)
        {
            if (!isKnownStasToken.HasValue || isKnownStasToken.Value)
            {
                if (output.Type == ScriptType.P2STAS)
                {
                }
            }

            if (output.Type == ScriptType.NullData && note == null)
            {
                note = ParseNote(output.ScriptPubKey, transaction.Raw, _networkProvider.Network);
            }
        }

        var transactionRef = new TransactionRef(transaction.Id)
        {
            Note = note,
        };

        await _documentStore.AddOrUpdateEntity(transactionRef);

        foreach (var address in message.Addresses)
        {
            await _connectionManager.OnAddressBalanceChanged(message.Transaction.Id, address);
        }
    }

    private void OnTransactionFoundError(Exception exception)
        => _logger.LogError(exception, "Failed to process FilteredTransactionMessage");

    public static string ParseNote(Slice scriptPubKey, byte[] transactionRaw, Network network)
    {
        if (scriptPubKey.Length > 200)
            return null;

        var start = scriptPubKey.Start;
        var end = scriptPubKey.Start + scriptPubKey.Length;
        var script = transactionRaw[start..end];
        var reader = LockingScriptReader.Read(script, network);

        return ParseNote(reader);
    }

    public static string ParseNote(LockingScriptReader reader)
    {
        return null;
    }

    #endregion

}
