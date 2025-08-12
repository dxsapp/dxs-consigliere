namespace Dxs.Consigliere.Services;

public interface IConnectionManager
{
    Task OnAddressBalanceChanged(string transactionId, string address);

    Task SubscribeToTransactionStream(string connectionId, string address);
    Task UnsubscribeToTransactionStream(string connectionId, string address);

    Task SubscribeToDeletedTransactionStream(string connectionId);
    Task UnsubscribeToDeletedTransactionStream(string connectionId);
}