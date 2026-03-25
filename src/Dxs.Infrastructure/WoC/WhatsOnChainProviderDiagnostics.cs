using System;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Infrastructure.Common;

namespace Dxs.Infrastructure.WoC;

public sealed class WhatsOnChainProviderDiagnostics : IExternalChainProviderDiagnostics
{
    public ExternalChainProviderDescriptor Descriptor { get; } = new(
        ExternalChainProviderName.WhatsOnChain,
        [
            ExternalChainCapability.Broadcast,
            ExternalChainCapability.BlockBackfill,
            ExternalChainCapability.RawTxFetch,
            ExternalChainCapability.ValidationFetch
        ],
        new ExternalChainRateLimitHint(
            RequestsPerMinute: 180,
            SourceHint: "client_time_limiter_3_per_second"
        )
    );

    public ValueTask<ExternalChainProviderHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(
            new ExternalChainProviderHealthSnapshot(
                Descriptor.Provider,
                ExternalChainHealthState.Unknown,
                "No lightweight live WhatsOnChain probe is configured yet.",
                DateTimeOffset.UtcNow
            )
        );
}
