using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Dto.Responses.Admin;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Data.Runtime;

public sealed class AdminRuntimeSourcePolicyService(
    IRealtimeSourcePolicyOverrideStore overrideStore,
    IOptions<ConsigliereSourcesConfig> sourcesConfig,
    IOptions<AppConfig> appConfig,
    IExternalChainProviderCatalog providerCatalog
) : IAdminRuntimeSourcePolicyService
{
    private static readonly string[] CandidateRealtimePrimarySources =
    [
        ExternalChainProviderName.Bitails,
        ExternalChainProviderName.JungleBus,
        SourceCapabilityRouting.NodeProvider
    ];

    public async Task<ConsigliereSourcesConfig> GetEffectiveSourcesConfigAsync(CancellationToken cancellationToken = default)
    {
        var effective = CloneConfig(sourcesConfig.Value);
        var currentOverride = await overrideStore.GetAsync(cancellationToken);
        ApplyOverride(effective, currentOverride);
        return effective;
    }

    public async Task<AdminRuntimeSourcesResponse> GetRuntimeSourcesAsync(CancellationToken cancellationToken = default)
    {
        var staticConfig = CloneConfig(sourcesConfig.Value);
        var effectiveConfig = await GetEffectiveSourcesConfigAsync(cancellationToken);
        var currentOverride = await overrideStore.GetAsync(cancellationToken);

        var staticRoute = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.RealtimeIngest,
            staticConfig,
            appConfig.Value,
            providerCatalog);
        var effectiveRoute = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.RealtimeIngest,
            effectiveConfig,
            appConfig.Value,
            providerCatalog);

        return new AdminRuntimeSourcesResponse
        {
            RealtimePolicy = new AdminRealtimeSourcePolicyResponse
            {
                Static = BuildValue(staticConfig, staticRoute),
                Override = currentOverride is null
                    ? null
                    : new AdminRealtimeSourcePolicyValuesResponse
                    {
                        PrimaryRealtimeSource = Normalize(currentOverride.PrimaryRealtimeSource),
                        BitailsTransport = Normalize(currentOverride.BitailsTransport),
                        FallbackSources = []
                    },
                Effective = BuildValue(effectiveConfig, effectiveRoute),
                OverrideActive = currentOverride is not null,
                RestartRequired = currentOverride is not null,
                AllowedPrimarySources = GetAllowedPrimarySources(staticConfig),
                AllowedBitailsTransports = GetAllowedBitailsTransports(staticConfig),
                UpdatedAt = currentOverride?.UpdatedAt ?? currentOverride?.CreatedAt,
                UpdatedBy = currentOverride?.UpdatedBy
            }
        };
    }

    public async Task<AdminRealtimeSourcePolicyMutationResult> ApplyRealtimePolicyAsync(
        string primaryRealtimeSource,
        string bitailsTransport,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var staticConfig = CloneConfig(sourcesConfig.Value);
        var allowedPrimarySources = GetAllowedPrimarySources(staticConfig);
        var allowedBitailsTransports = GetAllowedBitailsTransports(staticConfig);

        primaryRealtimeSource = Normalize(primaryRealtimeSource);
        bitailsTransport = Normalize(bitailsTransport);
        updatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "admin" : updatedBy.Trim();

        if (string.IsNullOrWhiteSpace(primaryRealtimeSource))
            return new AdminRealtimeSourcePolicyMutationResult(false, "primary_realtime_source_required");

        if (!allowedPrimarySources.Contains(primaryRealtimeSource, StringComparer.OrdinalIgnoreCase))
            return new AdminRealtimeSourcePolicyMutationResult(false, "invalid_primary_realtime_source");

        if (string.IsNullOrWhiteSpace(bitailsTransport))
            return new AdminRealtimeSourcePolicyMutationResult(false, "bitails_transport_required");

        if (!allowedBitailsTransports.Contains(bitailsTransport, StringComparer.OrdinalIgnoreCase))
            return new AdminRealtimeSourcePolicyMutationResult(false, "invalid_bitails_transport");

        var staticRoute = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.RealtimeIngest,
            staticConfig,
            appConfig.Value,
            providerCatalog);
        var staticValues = BuildValue(staticConfig, staticRoute);

        if (string.Equals(staticValues.PrimaryRealtimeSource, primaryRealtimeSource, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(staticValues.BitailsTransport, bitailsTransport, StringComparison.OrdinalIgnoreCase))
        {
            await overrideStore.ResetAsync(cancellationToken);
            return new AdminRealtimeSourcePolicyMutationResult(true);
        }

        await overrideStore.UpsertAsync(primaryRealtimeSource, bitailsTransport, updatedBy, cancellationToken);
        return new AdminRealtimeSourcePolicyMutationResult(true);
    }

    public async Task<AdminRuntimeSourcesResponse> ResetRealtimePolicyAsync(CancellationToken cancellationToken = default)
    {
        await overrideStore.ResetAsync(cancellationToken);
        return await GetRuntimeSourcesAsync(cancellationToken);
    }

    private static AdminRealtimeSourcePolicyValuesResponse BuildValue(
        ConsigliereSourcesConfig config,
        SourceCapabilityRoute route)
        => new()
        {
            PrimaryRealtimeSource = Normalize(route.PrimarySource),
            FallbackSources = route.FallbackSources.ToArray(),
            BitailsTransport = Normalize(config.Providers.Bitails.Connection.Transport)
        };

    private static void ApplyOverride(ConsigliereSourcesConfig config, RealtimeSourcePolicyOverrideDocument currentOverride)
    {
        if (currentOverride is null)
            return;

        config.Capabilities.RealtimeIngest.Source = currentOverride.PrimaryRealtimeSource;
        config.Providers.Bitails.Connection.Transport = currentOverride.BitailsTransport;
    }

    private string[] GetAllowedPrimarySources(ConsigliereSourcesConfig config)
    {
        var descriptors = providerCatalog.GetDescriptors()
            .ToDictionary(x => x.Provider, StringComparer.OrdinalIgnoreCase);

        return CandidateRealtimePrimarySources
            .Where(provider => CanServeRealtime(provider, config, descriptors))
            .ToArray();
    }

    private static string[] GetAllowedBitailsTransports(ConsigliereSourcesConfig config)
    {
        var result = new List<string>();
        var bitails = config.Providers.Bitails;

        if (!string.IsNullOrWhiteSpace(bitails.Connection.Websocket.BaseUrl) ||
            !string.IsNullOrWhiteSpace(bitails.Connection.BaseUrl))
        {
            result.Add(BitailsRealtimeTransportMode.Websocket);
        }

        if (!string.IsNullOrWhiteSpace(bitails.Connection.Zmq.TxUrl) ||
            !string.IsNullOrWhiteSpace(bitails.Connection.Zmq.BlockUrl))
        {
            result.Add(BitailsRealtimeTransportMode.Zmq);
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool CanServeRealtime(
        string provider,
        ConsigliereSourcesConfig config,
        IReadOnlyDictionary<string, ExternalChainProviderDescriptor> descriptors)
    {
        if (string.Equals(provider, SourceCapabilityRouting.NodeProvider, StringComparison.OrdinalIgnoreCase))
        {
            return config.Providers.Node.Enabled &&
                   config.Providers.Node.EnabledCapabilities.Contains(ExternalChainCapability.RealtimeIngest, StringComparer.OrdinalIgnoreCase);
        }

        if (!descriptors.TryGetValue(provider, out var descriptor))
            return false;

        if (!descriptor.Capabilities.Contains(ExternalChainCapability.RealtimeIngest, StringComparer.OrdinalIgnoreCase))
            return false;

        SourceProviderConfig providerConfig = provider.ToLowerInvariant() switch
        {
            ExternalChainProviderName.Bitails => config.Providers.Bitails,
            ExternalChainProviderName.JungleBus => config.Providers.JungleBus,
            _ => null
        };

        return providerConfig is { Enabled: true } &&
               providerConfig.EnabledCapabilities.Contains(ExternalChainCapability.RealtimeIngest, StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static ConsigliereSourcesConfig CloneConfig(ConsigliereSourcesConfig source)
        => new()
        {
            Routing = new SourceRoutingConfig
            {
                PreferredMode = source.Routing.PreferredMode,
                PrimarySource = source.Routing.PrimarySource,
                FallbackSources = [.. source.Routing.FallbackSources],
                VerificationSource = source.Routing.VerificationSource
            },
            Capabilities = new SourceCapabilitiesConfig
            {
                Broadcast = new BroadcastCapabilityOverrideConfig
                {
                    Mode = source.Capabilities.Broadcast.Mode,
                    Source = source.Capabilities.Broadcast.Source,
                    Sources = [.. source.Capabilities.Broadcast.Sources],
                    FallbackSources = [.. source.Capabilities.Broadcast.FallbackSources]
                },
                RealtimeIngest = new RoutedCapabilityOverrideConfig
                {
                    Source = source.Capabilities.RealtimeIngest.Source,
                    FallbackSources = [.. source.Capabilities.RealtimeIngest.FallbackSources]
                },
                BlockBackfill = new RoutedCapabilityOverrideConfig
                {
                    Source = source.Capabilities.BlockBackfill.Source,
                    FallbackSources = [.. source.Capabilities.BlockBackfill.FallbackSources]
                },
                RawTxFetch = new RoutedCapabilityOverrideConfig
                {
                    Source = source.Capabilities.RawTxFetch.Source,
                    FallbackSources = [.. source.Capabilities.RawTxFetch.FallbackSources]
                },
                ValidationFetch = new RoutedCapabilityOverrideConfig
                {
                    Source = source.Capabilities.ValidationFetch.Source,
                    FallbackSources = [.. source.Capabilities.ValidationFetch.FallbackSources]
                },
                HistoricalAddressScan = new RoutedCapabilityOverrideConfig
                {
                    Source = source.Capabilities.HistoricalAddressScan.Source,
                    FallbackSources = [.. source.Capabilities.HistoricalAddressScan.FallbackSources]
                },
                HistoricalTokenScan = new RoutedCapabilityOverrideConfig
                {
                    Source = source.Capabilities.HistoricalTokenScan.Source,
                    FallbackSources = [.. source.Capabilities.HistoricalTokenScan.FallbackSources]
                }
            },
            Providers = new SourceProvidersConfig
            {
                Node = new NodeSourceConfig
                {
                    Enabled = source.Providers.Node.Enabled,
                    ConnectTimeout = source.Providers.Node.ConnectTimeout,
                    RequestTimeout = source.Providers.Node.RequestTimeout,
                    StreamTimeout = source.Providers.Node.StreamTimeout,
                    IdleTimeout = source.Providers.Node.IdleTimeout,
                    EnabledCapabilities = [.. source.Providers.Node.EnabledCapabilities],
                    RateLimits = source.Providers.Node.RateLimits,
                    Connection = new NodeSourceConnectionConfig
                    {
                        RpcUrl = source.Providers.Node.Connection.RpcUrl,
                        RpcUser = source.Providers.Node.Connection.RpcUser,
                        RpcPassword = source.Providers.Node.Connection.RpcPassword,
                        ZmqTxUrl = source.Providers.Node.Connection.ZmqTxUrl,
                        ZmqBlockUrl = source.Providers.Node.Connection.ZmqBlockUrl
                    }
                },
                JungleBus = new JungleBusSourceConfig
                {
                    Enabled = source.Providers.JungleBus.Enabled,
                    ConnectTimeout = source.Providers.JungleBus.ConnectTimeout,
                    RequestTimeout = source.Providers.JungleBus.RequestTimeout,
                    StreamTimeout = source.Providers.JungleBus.StreamTimeout,
                    IdleTimeout = source.Providers.JungleBus.IdleTimeout,
                    EnabledCapabilities = [.. source.Providers.JungleBus.EnabledCapabilities],
                    RateLimits = source.Providers.JungleBus.RateLimits,
                    Connection = new JungleBusSourceConnectionConfig
                    {
                        BaseUrl = source.Providers.JungleBus.Connection.BaseUrl,
                        ApiKey = source.Providers.JungleBus.Connection.ApiKey
                    }
                },
                Bitails = new BitailsSourceConfig
                {
                    Enabled = source.Providers.Bitails.Enabled,
                    ConnectTimeout = source.Providers.Bitails.ConnectTimeout,
                    RequestTimeout = source.Providers.Bitails.RequestTimeout,
                    StreamTimeout = source.Providers.Bitails.StreamTimeout,
                    IdleTimeout = source.Providers.Bitails.IdleTimeout,
                    EnabledCapabilities = [.. source.Providers.Bitails.EnabledCapabilities],
                    RateLimits = source.Providers.Bitails.RateLimits,
                    Connection = new BitailsSourceConnectionConfig
                    {
                        BaseUrl = source.Providers.Bitails.Connection.BaseUrl,
                        ApiKey = source.Providers.Bitails.Connection.ApiKey,
                        Transport = source.Providers.Bitails.Connection.Transport,
                        Websocket = new BitailsWebsocketConnectionConfig
                        {
                            BaseUrl = source.Providers.Bitails.Connection.Websocket.BaseUrl
                        },
                        Zmq = new BitailsZmqConnectionConfig
                        {
                            TxUrl = source.Providers.Bitails.Connection.Zmq.TxUrl,
                            BlockUrl = source.Providers.Bitails.Connection.Zmq.BlockUrl
                        }
                    }
                },
                Whatsonchain = new WhatsOnChainSourceConfig
                {
                    Enabled = source.Providers.Whatsonchain.Enabled,
                    ConnectTimeout = source.Providers.Whatsonchain.ConnectTimeout,
                    RequestTimeout = source.Providers.Whatsonchain.RequestTimeout,
                    StreamTimeout = source.Providers.Whatsonchain.StreamTimeout,
                    IdleTimeout = source.Providers.Whatsonchain.IdleTimeout,
                    EnabledCapabilities = [.. source.Providers.Whatsonchain.EnabledCapabilities],
                    RateLimits = source.Providers.Whatsonchain.RateLimits,
                    Connection = new HttpApiSourceConnectionConfig
                    {
                        BaseUrl = source.Providers.Whatsonchain.Connection.BaseUrl,
                        ApiKey = source.Providers.Whatsonchain.Connection.ApiKey
                    }
                }
            }
        };
}
