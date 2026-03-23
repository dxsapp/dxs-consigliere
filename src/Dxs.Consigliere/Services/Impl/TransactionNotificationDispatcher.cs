using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Interfaces;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.WebSockets;

using Microsoft.AspNetCore.SignalR;

namespace Dxs.Consigliere.Services.Impl;

public sealed class TransactionNotificationDispatcher(
    IHubContext<WalletHub, IWalletHub> appContext,
    IAppCache<ConnectionManager> cache,
    ILogger<TransactionNotificationDispatcher> logger
)
{
    private static readonly TimeSpan TxNotificationDelay = TimeSpan.FromSeconds(5);
    private const string DeletedTransactionsStreamGroupName = "deleted-transactions";

    public Task SubscribeToTransactionStream(string connectionId, string address)
        => appContext.Groups.AddToGroupAsync(connectionId, address.EnsureValidBsvAddress().Value);

    public Task UnsubscribeToTransactionStream(string connectionId, string address)
        => appContext.Groups.RemoveFromGroupAsync(connectionId, address.EnsureValidBsvAddress().Value);

    public Task SubscribeToDeletedTransactionStream(string connectionId)
        => appContext.Groups.AddToGroupAsync(connectionId, DeletedTransactionsStreamGroupName);

    public Task UnsubscribeToDeletedTransactionStream(string connectionId)
        => appContext.Groups.RemoveFromGroupAsync(connectionId, DeletedTransactionsStreamGroupName);

    public Task OnAddressBalanceChanged(string address)
        => Throttle(address, () => appContext.Clients.Group(address).OnBalanceChanged(null));

    public Task OnTransactionDeleted(string transactionId)
        => appContext.Clients.Group(DeletedTransactionsStreamGroupName).OnTransactionDeleted(transactionId);

    public async Task OnTransactionFound(FilteredTransactionMessage message)
    {
        try
        {
            await Throttle(
                message.Transaction.Id,
                async () =>
                {
                    var hex = message.Transaction.Raw.ToHexString();

                    foreach (var address in GetTransactionAddresses(message))
                    {
                        await appContext.Clients.Group(address).OnTransactionFound(hex);
                    }
                }
            );
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "FilteredTransactionMessage.OnTransactionFound");
        }
    }

    private async Task Throttle(string key, Func<Task> action)
    {
        if (cache.TryGet<bool>(key, out _))
            return;

        await action();

        cache.Set(
            key,
            true,
            DateTime.UtcNow.Add(TxNotificationDelay)
        );
    }

    private static IEnumerable<string> GetTransactionAddresses(FilteredTransactionMessage message)
    {
        foreach (var address in message.Transaction.Outputs.Select(x => x.Address))
        {
            if (address != null)
                yield return address.Value;
        }

        foreach (var address in message.Transaction.Inputs.Select(x => x.Address))
        {
            if (address != null)
                yield return address.Value;
        }
    }
}
