using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Infrastructure.Bitails.Realtime;

public interface IBitailsRealtimeConnection : IAsyncDisposable
{
    IObservable<BitailsRealtimeEvent> Events { get; }
}

public interface IBitailsRealtimeIngestClient
{
    Task<IBitailsRealtimeConnection> ConnectAsync(
        BitailsRealtimeTransportPlan plan,
        string apiKey = null,
        CancellationToken cancellationToken = default
    );
}
