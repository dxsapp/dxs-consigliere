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
    INotificationHandler<BlockProcessed>,
    INotificationHandler<TransactionDeleted>,
    IDisposable
{
    private readonly TransactionNotificationDispatcher _notificationDispatcher;
    private readonly ManagedScopeRealtimeNotifier _managedScopeRealtimeNotifier;
    private readonly CompositeDisposable _subscriptions = new();

    public ConnectionManager(
        TransactionNotificationDispatcher notificationDispatcher,
        ManagedScopeRealtimeNotifier managedScopeRealtimeNotifier,
        IFilteredTransactionMessageBus transactionMessageBus
    )
    {
        _notificationDispatcher = notificationDispatcher;
        _managedScopeRealtimeNotifier = managedScopeRealtimeNotifier;

        transactionMessageBus
            .Subscribe(Observer.Create<FilteredTransactionMessage>(message =>
                _ = HandleTransactionObservedAsync(message)))
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

    public Task SubscribeToTokenStream(string connectionId, string tokenId)
        => _managedScopeRealtimeNotifier.SubscribeToTokenStream(connectionId, tokenId);

    public Task UnsubscribeToTokenStream(string connectionId, string tokenId)
        => _managedScopeRealtimeNotifier.UnsubscribeToTokenStream(connectionId, tokenId);

    public Task SubscribeToDeletedTransactionStream(string connectionId)
        => _notificationDispatcher.SubscribeToDeletedTransactionStream(connectionId);

    public Task UnsubscribeToDeletedTransactionStream(string connectionId)
        => _notificationDispatcher.UnsubscribeToDeletedTransactionStream(connectionId);

    public Task Handle(TransactionDeleted notification, CancellationToken cancellationToken)
        => HandleTransactionDeletedAsync(notification.Id, cancellationToken);

    public Task Handle(BlockProcessed notification, CancellationToken cancellationToken)
        => _managedScopeRealtimeNotifier.PublishBlockProcessedAsync(notification.Height, notification.Hash, cancellationToken);

    private async Task HandleTransactionObservedAsync(FilteredTransactionMessage message)
    {
        await _notificationDispatcher.OnTransactionFound(message);
        await _managedScopeRealtimeNotifier.PublishTransactionSeenAsync(message);
    }

    private async Task HandleTransactionDeletedAsync(string transactionId, CancellationToken cancellationToken)
    {
        await _notificationDispatcher.OnTransactionDeleted(transactionId);
        await _managedScopeRealtimeNotifier.PublishTransactionDeletedAsync(transactionId, cancellationToken);
    }

    public void Dispose() => _subscriptions.Dispose();
}
