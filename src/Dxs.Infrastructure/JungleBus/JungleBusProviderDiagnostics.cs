using System;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Infrastructure.Common;

namespace Dxs.Infrastructure.JungleBus;

public sealed class JungleBusProviderDiagnostics : IExternalChainProviderDiagnostics
{
    public ExternalChainProviderDescriptor Descriptor { get; } = new(
        ExternalChainProviderName.JungleBus,
        [
            ExternalChainCapability.RealtimeIngest,
            ExternalChainCapability.BlockBackfill
        ]
    );

    public ValueTask<ExternalChainProviderHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(
            new ExternalChainProviderHealthSnapshot(
                Descriptor.Provider,
                ExternalChainHealthState.Unknown,
                "Runtime-owned JungleBus websocket health is not surfaced yet.",
                DateTimeOffset.UtcNow
            )
        );
}
