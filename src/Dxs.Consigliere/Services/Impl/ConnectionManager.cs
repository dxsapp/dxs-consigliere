using System.Reactive;
using System.Reactive.Disposables;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Interfaces;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Notifications;
using Dxs.Consigliere.WebSockets;

using MediatR;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

using Raven.Client.Documents;

using TrustMargin.Common.Extensions;

namespace Dxs.Consigliere.Services.Impl;

public class ConnectionManager :
    IConnectionManager,
    INotificationHandler<TransactionDeleted>,
    IDisposable
{
    private readonly IHubContext<WalletHub, IWalletHub> _appContext;
    private readonly IAppCache<ConnectionManager> _cache;
    private readonly ILogger<ConnectionManager> _logger;

    private readonly CompositeDisposable _subscriptions = new();

    private static readonly TimeSpan TxNotificationDelay = TimeSpan.FromSeconds(5);
    private static readonly DateTime CreateDate = DateTime.UtcNow;

    public ConnectionManager(
        IDocumentStore documentStore,
        IHubContext<WalletHub, IWalletHub> appContext,
        IUtxoManager utxoManager,
        IAppCache<ConnectionManager> cache,
        IFilteredTransactionMessageBus transactionMessageBus,
        IOptions<AppConfig> appConfig,
        ILogger<ConnectionManager> logger
    )
    {
        _appContext = appContext;
        _cache = cache;
        _logger = logger;

        transactionMessageBus
            .Subscribe(Observer.Create<FilteredTransactionMessage>(OnTransactionFound))
            .AddToCompositeDisposable(_subscriptions);

        _logger.LogDebug("Ctor. ConnectionManager: {Date}", CreateDate);
    }

    public Task OnAddressBalanceChanged(
        string transactionId,
        string address
    ) => Throttle(address, async () =>
    {
        await _appContext.Clients.Group(address).OnBalanceChanged(null);
    });

    public Task SubscribeToTransactionStream(string connectionId, string address)
        => _appContext.Groups.AddToGroupAsync(connectionId, address.EnsureValidBsvAddress().Value);

    public Task UnsubscribeToTransactionStream(string connectionId, string address)
        => _appContext.Groups.RemoveFromGroupAsync(connectionId, address.EnsureValidBsvAddress().Value);

    public Task SubscribeToDeletedTransactionStream(string connectionId)
        => _appContext.Groups.AddToGroupAsync(connectionId, GetDeletedTransactionsStreamGroupName);

    public Task UnsubscribeToDeletedTransactionStream(string connectionId)
        => _appContext.Groups.RemoveFromGroupAsync(connectionId, GetDeletedTransactionsStreamGroupName);

    public Task Handle(TransactionDeleted notification, CancellationToken cancellationToken)
        => _appContext.Clients
            .Group(GetDeletedTransactionsStreamGroupName)
            .OnTransactionDeleted(notification.Id);

    public void Dispose() => _subscriptions.Dispose();


    #region .pvt

    // ReSharper disable once AsyncVoidMethod
    private async void OnTransactionFound(FilteredTransactionMessage message)
        => await Throttle(
            message.Transaction.Id,
            async () =>
            {
                try
                {
                    if (_cache.TryGet<bool>(message.Transaction.Id, out _))
                        return;

                    var hex = message.Transaction.Raw.ToHexString();

                    foreach (var address in message.Transaction.Outputs.Select(x => x.Address))
                    {
                        if (address != null)
                        {
                            await _appContext.Clients.Group(address.Value).OnTransactionFound(hex);
                        }
                    }

                    foreach (var address in message.Transaction.Inputs.Select(x => x.Address))
                    {
                        if (address != null)
                        {
                            await _appContext.Clients.Group(address.Value).OnTransactionFound(hex);
                        }
                    }

                    _cache.Set(
                        message.Transaction.Id,
                        true,
                        DateTime.UtcNow.Add(TxNotificationDelay)
                    );
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "FilteredTransactionMessage.OnTransactionFound");
                }
            });

    private async Task Throttle(string key, Func<Task> action)
    {
        if (_cache.TryGet<bool>(key, out _))
            return;

        await action();

        _cache.Set(
            key,
            true,
            DateTime.UtcNow.Add(TxNotificationDelay)
        );
    }

    private const string GetDeletedTransactionsStreamGroupName = "deleted-transactions";

    #endregion

}
