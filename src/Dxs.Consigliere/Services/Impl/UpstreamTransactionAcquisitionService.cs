using Dxs.Bsv.Rpc.Models;
using Dxs.Bsv.Rpc.Services;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.Common;
using Dxs.Infrastructure.JungleBus;
using Dxs.Infrastructure.WoC;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Services.Impl;

public interface IUpstreamTransactionAcquisitionService
{
    Task<RawTransactionFetchResult?> TryGetAsync(string txId, string capability, CancellationToken cancellationToken = default);
}

public sealed class UpstreamTransactionAcquisitionService(
    IAdminProviderConfigService providerConfigService,
    IOptions<AppConfig> legacyConfig,
    IExternalChainProviderCatalog providerCatalog,
    IJungleBusRawTransactionClient jungleBusRawTransactionClient,
    IBitailsRestApiClient bitailsRestApiClient,
    IWhatsOnChainRestApiClient whatsonChainRestApiClient,
    IRpcClient rpcClient,
    ILogger<UpstreamTransactionAcquisitionService> logger
) : IUpstreamTransactionAcquisitionService
{
    public async Task<RawTransactionFetchResult?> TryGetAsync(string txId, string capability, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(txId))
            throw new ArgumentException("Transaction id is required.", nameof(txId));
        if (string.IsNullOrWhiteSpace(capability))
            throw new ArgumentException("Capability is required.", nameof(capability));

        var effectiveSources = await providerConfigService.GetEffectiveSourcesConfigAsync(cancellationToken);
        var route = SourceCapabilityRouting.Resolve(capability, effectiveSources, legacyConfig.Value, providerCatalog);
        var providers = EnumerateProviders(route);

        var hadProviderError = false;
        Exception? lastException = null;

        foreach (var provider in providers)
        {
            try
            {
                var raw = await TryFetchFromProviderAsync(provider, txId, cancellationToken);
                if (raw is { Length: > 0 })
                    return new RawTransactionFetchResult(provider, raw);
            }
            catch (Exception exception)
            {
                hadProviderError = true;
                lastException = exception;
                logger.LogWarning(exception, "Transaction acquisition via {Provider} failed for {TxId} ({Capability})", provider, txId, capability);
            }
        }

        if (hadProviderError && lastException is not null)
        {
            throw new InvalidOperationException(
                $"Transaction acquisition failed for `{txId}` after trying {string.Join(", ", providers)} via `{capability}`.",
                lastException);
        }

        return null;
    }

    private async Task<byte[]?> TryFetchFromProviderAsync(string provider, string txId, CancellationToken cancellationToken)
        => provider.ToLowerInvariant() switch
        {
            ExternalChainProviderName.JungleBus => await jungleBusRawTransactionClient.GetTransactionRawOrNullAsync(txId, cancellationToken),
            ExternalChainProviderName.Bitails => await bitailsRestApiClient.GetTransactionRawOrNullAsync(txId, cancellationToken),
            ExternalChainProviderName.WhatsOnChain => ParseHexOrNull(await whatsonChainRestApiClient.GetTransactionRawOrNullAsync(txId, cancellationToken)),
            SourceCapabilityRouting.NodeProvider => ParseHexOrNull(await rpcClient.GetRawTransactionAsString(txId).EnsureSuccess()),
            _ => throw new InvalidOperationException($"Unsupported transaction acquisition provider `{provider}`.")
        };

    private static IReadOnlyList<string> EnumerateProviders(SourceCapabilityRoute route)
        => new[] { route.PrimarySource }
            .Concat(route.FallbackSources)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static byte[]? ParseHexOrNull(string? hex)
        => string.IsNullOrWhiteSpace(hex) ? null : Convert.FromHexString(hex);
}
