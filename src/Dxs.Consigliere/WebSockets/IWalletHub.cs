using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Responses;

namespace Dxs.Consigliere.WebSockets;

public interface IWalletHub
{
    Task OnTransactionFound(string hex);
    Task OnTransactionDeleted(string hash);
    Task OnBalanceChanged(BalanceDto balanceDto);
    Task OnRealtimeEvent(RealtimeEventResponse eventDto);
}
