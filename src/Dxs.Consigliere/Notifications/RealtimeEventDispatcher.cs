using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.WebSockets;

using Microsoft.AspNetCore.SignalR;

namespace Dxs.Consigliere.Notifications;

public sealed class RealtimeEventDispatcher(IHubContext<WalletHub, IWalletHub> appContext) : IRealtimeEventDispatcher
{
    private const string TokenGroupPrefix = "token:";

    public Task SubscribeToAddressStream(string connectionId, string address)
        => appContext.Groups.AddToGroupAsync(connectionId, NormalizeAddressGroup(address));

    public Task UnsubscribeToAddressStream(string connectionId, string address)
        => appContext.Groups.RemoveFromGroupAsync(connectionId, NormalizeAddressGroup(address));

    public Task SubscribeToTokenStream(string connectionId, string tokenId)
        => appContext.Groups.AddToGroupAsync(connectionId, NormalizeTokenGroup(tokenId));

    public Task UnsubscribeToTokenStream(string connectionId, string tokenId)
        => appContext.Groups.RemoveFromGroupAsync(connectionId, NormalizeTokenGroup(tokenId));

    public Task PublishToAddressAsync(string address, RealtimeEventResponse eventDto)
        => appContext.Clients.Group(NormalizeAddressGroup(address)).OnRealtimeEvent(eventDto);

    public Task PublishToTokenAsync(string tokenId, RealtimeEventResponse eventDto)
        => appContext.Clients.Group(NormalizeTokenGroup(tokenId)).OnRealtimeEvent(eventDto);

    private static string NormalizeAddressGroup(string address) => address.EnsureValidBsvAddress().Value;

    private static string NormalizeTokenGroup(string tokenId) => $"{TokenGroupPrefix}{tokenId}";
}
