using System.Reactive;
using System.Reactive.Disposables;

using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Interfaces;
using Dxs.Consigliere.Notifications;

using MediatR;

using TrustMargin.Common.Extensions;

namespace Dxs.Consigliere.Services.Impl;

public class ConnectionManager :
    IConnectionManager,
    INotificationHandler<TransactionDeleted>,
    IDisposable
{
    private readonly TransactionNotificationDispatcher _notificationDispatcher;
    private readonly CompositeDisposable _subscriptions = new();

    public ConnectionManager(
        TransactionNotificationDispatcher notificationDispatcher,
        IFilteredTransactionMessageBus transactionMessageBus
    )
    {
        _notificationDispatcher = notificationDispatcher;

        transactionMessageBus
            .Subscribe(Observer.Create<FilteredTransactionMessage>(message =>
                _ = _notificationDispatcher.OnTransactionFound(message)))
            .AddToCompositeDisposable(_subscriptions);
    }

    public Task OnAddressBalanceChanged(
        string transactionId,
        string address
    ) => _notificationDispatcher.OnAddressBalanceChanged(address);

    public Task SubscribeToTransactionStream(string connectionId, string address)
        => _notificationDispatcher.SubscribeToTransactionStream(connectionId, address);

    public Task UnsubscribeToTransactionStream(string connectionId, string address)
        => _notificationDispatcher.UnsubscribeToTransactionStream(connectionId, address);

    public Task SubscribeToDeletedTransactionStream(string connectionId)
        => _notificationDispatcher.SubscribeToDeletedTransactionStream(connectionId);

    public Task UnsubscribeToDeletedTransactionStream(string connectionId)
        => _notificationDispatcher.UnsubscribeToDeletedTransactionStream(connectionId);

    public Task Handle(TransactionDeleted notification, CancellationToken cancellationToken)
        => _notificationDispatcher.OnTransactionDeleted(notification.Id);

    public void Dispose() => _subscriptions.Dispose();
}
