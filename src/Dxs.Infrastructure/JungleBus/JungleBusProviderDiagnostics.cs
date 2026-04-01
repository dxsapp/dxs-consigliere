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
            ExternalChainCapability.RawTxFetch,
            ExternalChainCapability.ValidationFetch,
            ExternalChainCapability.RealtimeIngest,
            ExternalChainCapability.BlockBackfill
        ],
        new ExternalChainRateLimitHint(
            RequestsPerMinute: 600,
            SourceHint: "reverse_lineage_validation_fetch_10_per_second"
        )
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
