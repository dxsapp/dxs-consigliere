using Dxs.Consigliere.Dto;

namespace Dxs.Consigliere.WebSockets;

public interface IWalletHub
{
    Task OnTransactionFound(string hex);
    Task OnTransactionDeleted(string hash);
    Task OnBalanceChanged(BalanceDto balanceDto);
}