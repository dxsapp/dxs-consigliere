namespace Dxs.Consigliere.Services;

public interface IConnectionManager
{
    Task OnAddressBalanceChanged(string transactionId, string address);

    Task SubscribeToTransactionStream(string connectionId, string address);
    Task UnsubscribeToTransactionStream(string connectionId, string address);

    Task SubscribeToTokenStream(string connectionId, string tokenId);
    Task UnsubscribeToTokenStream(string connectionId, string tokenId);

    Task SubscribeToDeletedTransactionStream(string connectionId);
    Task UnsubscribeToDeletedTransactionStream(string connectionId);
}
