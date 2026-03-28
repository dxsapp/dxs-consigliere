using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Infrastructure.Common;

public sealed record BitailsProviderRuntimeSettings(
    string BaseUrl,
    string ApiKey,
    string Transport,
    string WebsocketBaseUrl,
    string ZmqTxUrl,
    string ZmqBlockUrl
);

public sealed record WhatsOnChainProviderRuntimeSettings(
    string BaseUrl,
    string ApiKey
);

public sealed record JungleBusProviderRuntimeSettings(
    string BaseUrl,
    string ApiKey,
    string MempoolSubscriptionId,
    string BlockSubscriptionId
);

public interface IExternalChainProviderSettingsAccessor
{
    ValueTask<BitailsProviderRuntimeSettings> GetBitailsAsync(CancellationToken cancellationToken = default);
    ValueTask<WhatsOnChainProviderRuntimeSettings> GetWhatsOnChainAsync(CancellationToken cancellationToken = default);
    ValueTask<JungleBusProviderRuntimeSettings> GetJungleBusAsync(CancellationToken cancellationToken = default);
}
