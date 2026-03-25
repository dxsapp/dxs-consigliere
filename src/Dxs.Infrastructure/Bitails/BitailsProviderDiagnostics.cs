using System;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Infrastructure.Common;

namespace Dxs.Infrastructure.Bitails;

public sealed class BitailsProviderDiagnostics : IExternalChainProviderDiagnostics
{
    public ExternalChainProviderDescriptor Descriptor { get; } = new(
        ExternalChainProviderName.Bitails,
        [
            ExternalChainCapability.Broadcast,
            ExternalChainCapability.RawTxFetch,
            ExternalChainCapability.ValidationFetch
        ],
        new ExternalChainRateLimitHint(
            RequestsPerMinute: 600,
            SourceHint: "client_time_limiter_10_per_second"
        )
    );

    public ValueTask<ExternalChainProviderHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(
            new ExternalChainProviderHealthSnapshot(
                Descriptor.Provider,
                ExternalChainHealthState.Unknown,
                "No lightweight live Bitails probe is configured yet.",
                DateTimeOffset.UtcNow
            )
        );
}
