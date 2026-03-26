using Dxs.Consigliere.Dto.Responses;

namespace Dxs.Consigliere.Notifications;

public interface IRealtimeEventDispatcher
{
    Task SubscribeToAddressStream(string connectionId, string address);
    Task UnsubscribeToAddressStream(string connectionId, string address);
    Task SubscribeToTokenStream(string connectionId, string tokenId);
    Task UnsubscribeToTokenStream(string connectionId, string tokenId);
    Task PublishToAddressAsync(string address, RealtimeEventResponse eventDto);
    Task PublishToTokenAsync(string tokenId, RealtimeEventResponse eventDto);
}
