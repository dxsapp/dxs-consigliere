using Dxs.Bsv;
using Dxs.Bsv.Factories;
using Dxs.Bsv.Models;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Extensions;
using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.Common;
using Dxs.Infrastructure.WoC;

using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Services.Impl;

public class BroadcastService(
    IBitcoindService bitcoindService,
    IBitailsRestApiClient bitailsRestApiClient,
    IWhatsOnChainRestApiClient whatsOnChainRestApiClient,
    IAdminProviderConfigService providerConfigService,
    IExternalChainProviderCatalog providerCatalog,
    IDocumentStore documentStore,
    IUtxoCache utxoCache,
    INetworkProvider networkProvider,
    IOptions<AppConfig> appConfig,
    ILogger<BroadcastService> logger
) : IBroadcastService
{
    private readonly AppConfig _appConfig = appConfig.Value;

    private readonly AsyncRetryPolicy _simpleRetryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(10, _ => TimeSpan.FromSeconds(2));

    public Task<decimal> SatoshisPerByte() => bitcoindService.SatoshisPerByte();

    public Task<Broadcast> Broadcast(string transaction, string batchId = null)
    {
        if (!Transaction.TryParse(transaction, networkProvider.Network, out var parsed))
            throw new Exception("Unable to parse transaction");

        return Broadcast(parsed, batchId);
    }

    public async Task<Broadcast> Broadcast(Transaction transaction, string batchId = null)
    {
        using var _ = logger.BeginScope("{TransactionId}", transaction.Id);

        var broadcastAttempt = new Broadcast
        {
            TxId = transaction.Id,
            BatchId = batchId
        };
        await documentStore.AddOrUpdateEntity(broadcastAttempt);

        var attempts = await BroadcastToConfiguredProvidersAsync(transaction);
        var successfulAttempts = attempts.Where(x => x.Success).ToArray();

        if (successfulAttempts.Length > 0)
        {
            foreach (var input in transaction.Inputs)
            {
                var outPoint = new OutPoint(input.TxId, input.Address, null, 0, uint.MaxValue);
                utxoCache.MarkUsed(outPoint, false);
            }
        }

        broadcastAttempt.Attempts = attempts;
        broadcastAttempt.Success = successfulAttempts.Length > 0;
        broadcastAttempt.Code = successfulAttempts.FirstOrDefault()?.Provider
            ?? attempts.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Code))?.Code;
        broadcastAttempt.Message = successfulAttempts.Length > 0
            ? $"Broadcast accepted by {string.Join(", ", successfulAttempts.Select(x => x.Provider))}"
            : string.Join(" | ", attempts.Select(FormatAttemptMessage));

        await documentStore.UpdateEntity(broadcastAttempt);

        return broadcastAttempt;
    }

    private async Task<BroadcastProviderAttempt[]> BroadcastToConfiguredProvidersAsync(Transaction transaction)
    {
        var targets = await ResolveBroadcastTargetsAsync();
        var tasks = targets.Select(provider => BroadcastWithRetryAsync(provider, transaction)).ToArray();
        return await Task.WhenAll(tasks);
    }

    private async Task<BroadcastProviderAttempt> BroadcastWithRetryAsync(string provider, Transaction transaction)
    {
        var retryResult = await _simpleRetryPolicy.ExecuteAndCaptureAsync(async () =>
        {
            var attempt = await BroadcastToProviderAsync(provider, transaction);
            if (attempt.Success || IsPermanentFailure(attempt.Message))
                return attempt;

            throw new ApplicationException(attempt.Message ?? $"{provider}_broadcast_failed");
        });

        return retryResult.Outcome == OutcomeType.Successful
            ? retryResult.Result
            : new BroadcastProviderAttempt
            {
                Provider = provider,
                Success = false,
                Code = null,
                Message = retryResult.FinalException?.Message
            };
    }

    private async Task<BroadcastProviderAttempt> BroadcastToProviderAsync(string provider, Transaction transaction)
    {
        var txHex = transaction.Raw.ToHexString();
        BroadcastProviderAttempt result = provider.ToLowerInvariant() switch
        {
            SourceCapabilityRouting.NodeProvider => await BroadcastToNodeAsync(txHex),
            ExternalChainProviderName.Bitails => await BroadcastToBitailsAsync(txHex),
            ExternalChainProviderName.WhatsOnChain => await BroadcastToWhatsOnChainAsync(txHex),
            _ => new BroadcastProviderAttempt
            {
                Provider = provider,
                Success = false,
                Message = "broadcast_provider_not_supported"
            }
        };

        if (!result.Success)
        {
            logger.LogError("{@Broadcast}", new
            {
                TransactionId = transaction.Id,
                Provider = result.Provider,
                Message = result.Message,
                Code = result.Code,
            });
        }
        else
        {
            logger.LogDebug("{@Broadcast}", new
            {
                TransactionId = transaction.Id,
                Provider = result.Provider,
            });
        }

        return result;
    }

    private async Task<BroadcastProviderAttempt> BroadcastToNodeAsync(string txHex)
    {
        var result = await bitcoindService.Broadcast(txHex);
        return new BroadcastProviderAttempt
        {
            Provider = SourceCapabilityRouting.NodeProvider,
            Success = result.success,
            Code = result.code,
            Message = result.message
        };
    }

    private async Task<BroadcastProviderAttempt> BroadcastToBitailsAsync(string txHex)
    {
        var response = await bitailsRestApiClient.Broadcast(txHex, CancellationToken.None);
        return new BroadcastProviderAttempt
        {
            Provider = ExternalChainProviderName.Bitails,
            Success = response?.Error is null,
            Code = response?.Error?.Code.ToString(),
            Message = response?.Error?.Message
        };
    }

    private async Task<BroadcastProviderAttempt> BroadcastToWhatsOnChainAsync(string txHex)
    {
        await whatsOnChainRestApiClient.BroadcastAsync(txHex, CancellationToken.None);
        return new BroadcastProviderAttempt
        {
            Provider = ExternalChainProviderName.WhatsOnChain,
            Success = true
        };
    }

    private async Task<string[]> ResolveBroadcastTargetsAsync()
    {
        var effectiveSources = await providerConfigService.GetEffectiveSourcesConfigAsync();
        var requestedTargets = effectiveSources.Capabilities.Broadcast.Sources;
        if (requestedTargets is null || requestedTargets.Length == 0)
            requestedTargets = [SourceCapabilityRouting.NodeProvider];

        var descriptors = providerCatalog.GetDescriptors()
            .ToDictionary(x => x.Provider, StringComparer.OrdinalIgnoreCase);

        return requestedTargets
            .Where(x => CanBroadcastWith(x, effectiveSources, descriptors))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool CanBroadcastWith(
        string provider,
        ConsigliereSourcesConfig sourcesConfig,
        IReadOnlyDictionary<string, ExternalChainProviderDescriptor> descriptors)
    {
        if (string.Equals(provider, SourceCapabilityRouting.NodeProvider, StringComparison.OrdinalIgnoreCase))
        {
            return sourcesConfig.Providers.Node.Enabled
                && sourcesConfig.Providers.Node.EnabledCapabilities.Contains(ExternalChainCapability.Broadcast, StringComparer.OrdinalIgnoreCase);
        }

        if (!descriptors.TryGetValue(provider, out var descriptor))
            return false;

        if (!descriptor.Capabilities.Contains(ExternalChainCapability.Broadcast, StringComparer.OrdinalIgnoreCase))
            return false;

        SourceProviderConfig config = provider.ToLowerInvariant() switch
        {
            ExternalChainProviderName.Bitails => sourcesConfig.Providers.Bitails,
            ExternalChainProviderName.WhatsOnChain => sourcesConfig.Providers.Whatsonchain,
            _ => null
        };

        return config is { Enabled: true }
            && config.EnabledCapabilities.Contains(ExternalChainCapability.Broadcast, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsPermanentFailure(string message)
        => !string.IsNullOrWhiteSpace(message)
           && (message.Contains("missing-inputs", StringComparison.InvariantCultureIgnoreCase)
               || message.Contains("missing inputs", StringComparison.InvariantCultureIgnoreCase)
               || message.Contains("mandatory-script-verify-flag-failed", StringComparison.InvariantCultureIgnoreCase));

    private static string FormatAttemptMessage(BroadcastProviderAttempt attempt)
        => $"{attempt.Provider}:{attempt.Message ?? (attempt.Success ? "accepted" : "failed")}";
}
