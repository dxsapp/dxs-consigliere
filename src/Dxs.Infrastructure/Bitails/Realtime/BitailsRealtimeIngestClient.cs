using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Infrastructure.Bitails.Realtime;

public sealed class BitailsRealtimeIngestClient(
    BitailsSocketIoRealtimeIngestClient websocketClient,
    BitailsSocketIoZmqProxyIngestClient zmqProxyClient
) : IBitailsRealtimeIngestClient
{
    public Task<IBitailsRealtimeConnection> ConnectAsync(
        BitailsRealtimeTransportPlan plan,
        string apiKey = null,
        CancellationToken cancellationToken = default)
        => plan.Mode switch
        {
            BitailsRealtimeTransportMode.WebSocket => websocketClient.ConnectAsync(plan, apiKey, cancellationToken),
            BitailsRealtimeTransportMode.Zmq => zmqProxyClient.ConnectAsync(plan, apiKey, cancellationToken),
            _ => throw new System.NotSupportedException($"Bitails realtime transport `{plan.Mode}` is not supported.")
        };
}
