using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;
using Dxs.Common.Cache;
using Dxs.Common.Extensions;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Models.History;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Extensions;

using Microsoft.Extensions.Logging;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Services.Impl;

public class AddressHistoryService : IAddressHistoryService, IDisposable
{
    private readonly IDocumentStore _documentStore;
    private readonly IConnectionManager _connectionManager;
    private readonly INetworkProvider _networkProvider;
    private readonly ILogger _logger;
    private readonly AddressHistoryProjectionReader _projectionReader;

    private readonly IDisposable _subscription;

    public AddressHistoryService(
        IDocumentStore documentStore,
        IFilteredTransactionMessageBus filteredTransactionMessageBus,
        IConnectionManager connectionManager,
        INetworkProvider networkProvider,
        AddressHistoryProjectionReader projectionReader,
        ILogger<AddressHistoryService> logger
    )
    {
        _documentStore = documentStore;
        _connectionManager = connectionManager;
        _networkProvider = networkProvider;
        _logger = logger;
        _projectionReader = projectionReader;

        _subscription = filteredTransactionMessageBus.SubscribeAsync(OnTransactionFound, OnTransactionFoundError);
    }

    public AddressHistoryService(
        IDocumentStore documentStore,
        IFilteredTransactionMessageBus filteredTransactionMessageBus,
        IConnectionManager connectionManager,
        INetworkProvider networkProvider,
        ILogger<AddressHistoryService> logger
    )
        : this(
            documentStore,
            filteredTransactionMessageBus,
            connectionManager,
            networkProvider,
            new AddressHistoryProjectionReader(documentStore, networkProvider, new NoopProjectionReadCache(), new ProjectionReadCacheKeyFactory()),
            logger)
    {
    }

    public async Task<AddressHistoryResponse> GetHistory(GetAddressHistoryRequest request)
        => await _projectionReader.GetHistory(request);

    public void Dispose()
    {
        _subscription?.Dispose();
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
                if (output.Type is ScriptType.P2STAS or ScriptType.DSTAS)
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
}
