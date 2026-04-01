using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses.Admin;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Data.Runtime;

public sealed class AdminProviderConfigService(
    IRealtimeSourcePolicyOverrideStore overrideStore,
    IOptions<ConsigliereSourcesConfig> sourcesConfig,
    IOptions<AppConfig> appConfig,
    IExternalChainProviderCatalog providerCatalog
) : IAdminProviderConfigService
{
    private const string RecommendedRealtimeProvider = ExternalChainProviderName.Bitails;
    private const string RecommendedRestProvider = ExternalChainProviderName.WhatsOnChain;
    private const string RecommendedRawTxProvider = ExternalChainProviderName.JungleBus;

    private static readonly string[] CandidateRealtimePrimarySources =
    [
        ExternalChainProviderName.Bitails,
        ExternalChainProviderName.JungleBus,
        SourceCapabilityRouting.NodeProvider
    ];

    private static readonly string[] CandidateRestPrimaryProviders =
    [
        ExternalChainProviderName.WhatsOnChain,
        ExternalChainProviderName.Bitails
    ];

    private static readonly string[] CandidateRawTxPrimaryProviders =
    [
        ExternalChainProviderName.JungleBus,
        ExternalChainProviderName.Bitails,
        ExternalChainProviderName.WhatsOnChain
    ];

    public async Task<ConsigliereSourcesConfig> GetEffectiveSourcesConfigAsync(CancellationToken cancellationToken = default)
    {
        var effective = CloneConfig(sourcesConfig.Value);
        var persistedProviderConfig = await GetPersistedProviderConfigDocumentAsync(cancellationToken);
        ApplyOverride(effective, persistedProviderConfig);
        return effective;
    }

    public async Task<JungleBusProviderRuntimeSnapshot> GetEffectiveJungleBusAsync(CancellationToken cancellationToken = default)
    {
        var persistedProviderConfig = await GetPersistedProviderConfigDocumentAsync(cancellationToken);
        var baseUrl = NormalizeOptional(persistedProviderConfig.JungleBusBaseUrl);
        var mempoolSubscriptionId = NormalizeOptional(persistedProviderConfig.JungleBusMempoolSubscriptionId);
        var blockSubscriptionId = NormalizeOptional(persistedProviderConfig.JungleBusBlockSubscriptionId);
        return new JungleBusProviderRuntimeSnapshot(baseUrl, mempoolSubscriptionId, blockSubscriptionId);
    }

    public async Task<AdminProvidersResponse> GetProvidersAsync(CancellationToken cancellationToken = default)
    {
        var staticConfig = CloneConfig(sourcesConfig.Value);
        var persistedProviderConfig = await GetPersistedProviderConfigDocumentAsync(cancellationToken);
        var effectiveConfig = CloneConfig(sourcesConfig.Value);
        ApplyOverride(effectiveConfig, persistedProviderConfig);
        var effectiveJungleBus = new JungleBusProviderRuntimeSnapshot(
            NormalizeOptional(persistedProviderConfig.JungleBusBaseUrl),
            NormalizeOptional(persistedProviderConfig.JungleBusMempoolSubscriptionId),
            NormalizeOptional(persistedProviderConfig.JungleBusBlockSubscriptionId));
        var effectiveValues = BuildConfigValues(effectiveConfig, effectiveJungleBus);
        var descriptors = providerCatalog.GetDescriptors().ToDictionary(x => x.Provider, StringComparer.OrdinalIgnoreCase);
        var staticValues = BuildConfigValues(staticConfig, new JungleBusProviderRuntimeSnapshot(
            NormalizeOptional(staticConfig.Providers.JungleBus.Connection.BaseUrl),
            NormalizeOptional(appConfig.Value.JungleBus.MempoolSubscriptionId),
            NormalizeOptional(appConfig.Value.JungleBus.BlockSubscriptionId)));
        var persistedValues = BuildOverrideValues(persistedProviderConfig);
        var overrideActive = !ConfigurationMatches(staticValues, persistedValues);

        return new AdminProvidersResponse
        {
            Recommendations = new AdminProviderRecommendationsResponse
            {
                RealtimePrimaryProvider = RecommendedRealtimeProvider,
                RestPrimaryProvider = RecommendedRestProvider,
                RawTxFetchProvider = RecommendedRawTxProvider
            },
            Config = new AdminProviderConfigResponse
            {
                Static = staticValues,
                Override = overrideActive ? persistedValues : null,
                Effective = effectiveValues,
                OverrideActive = overrideActive,
                RestartRequired = RequiresRestart(effectiveValues),
                AllowedRealtimePrimaryProviders = GetAllowedRealtimePrimaryProviders(staticConfig),
                AllowedRawTxPrimaryProviders = GetAllowedRawTxPrimaryProviders(staticConfig),
                AllowedRestPrimaryProviders = GetAllowedRestPrimaryProviders(staticConfig),
                AllowedBitailsTransports = GetAllowedBitailsTransports(staticConfig),
                UpdatedAt = persistedProviderConfig.UpdatedAt ?? persistedProviderConfig.CreatedAt,
                UpdatedBy = persistedProviderConfig.UpdatedBy
            },
            Providers = BuildCatalog(staticConfig, effectiveConfig, effectiveJungleBus, descriptors)
        };
    }

    private static bool RequiresRestart(AdminProviderConfigValuesResponse effectiveValues)
    {
        if (string.Equals(effectiveValues.RealtimePrimaryProvider, SourceCapabilityRouting.NodeProvider, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(effectiveValues.RealtimePrimaryProvider, ExternalChainProviderName.Bitails, StringComparison.OrdinalIgnoreCase)
               && string.Equals(effectiveValues.BitailsTransport, BitailsRealtimeTransportMode.Zmq, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<AdminProviderConfigMutationResult> ApplyProviderConfigAsync(
        AdminProviderConfigUpdateRequest request,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return new AdminProviderConfigMutationResult(false, "request_required");

        var staticConfig = CloneConfig(sourcesConfig.Value);
        var allowedRealtimePrimaryProviders = GetAllowedRealtimePrimaryProviders(staticConfig);
        var allowedRawTxPrimaryProviders = GetAllowedRawTxPrimaryProviders(staticConfig);
        var allowedRestPrimaryProviders = GetAllowedRestPrimaryProviders(staticConfig);
        var allowedBitailsTransports = GetAllowedBitailsTransports(staticConfig);

        var realtimePrimary = Normalize(request.RealtimePrimaryProvider);
        var rawTxPrimary = Normalize(request.RawTxPrimaryProvider);
        var restPrimary = Normalize(request.RestPrimaryProvider);
        var bitailsTransport = Normalize(request.BitailsTransport);
        updatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "admin" : updatedBy.Trim();

        if (string.IsNullOrWhiteSpace(realtimePrimary))
            return new AdminProviderConfigMutationResult(false, "realtime_primary_provider_required");
        if (!allowedRealtimePrimaryProviders.Contains(realtimePrimary, StringComparer.OrdinalIgnoreCase))
            return new AdminProviderConfigMutationResult(false, "invalid_realtime_primary_provider");

        if (string.IsNullOrWhiteSpace(restPrimary))
            return new AdminProviderConfigMutationResult(false, "rest_primary_provider_required");
        if (!allowedRestPrimaryProviders.Contains(restPrimary, StringComparer.OrdinalIgnoreCase))
            return new AdminProviderConfigMutationResult(false, "invalid_rest_primary_provider");

        if (string.IsNullOrWhiteSpace(rawTxPrimary))
            return new AdminProviderConfigMutationResult(false, "raw_tx_primary_provider_required");
        if (!allowedRawTxPrimaryProviders.Contains(rawTxPrimary, StringComparer.OrdinalIgnoreCase))
            return new AdminProviderConfigMutationResult(false, "invalid_raw_tx_primary_provider");

        if (string.IsNullOrWhiteSpace(bitailsTransport))
            return new AdminProviderConfigMutationResult(false, "bitails_transport_required");
        if (!allowedBitailsTransports.Contains(bitailsTransport, StringComparer.OrdinalIgnoreCase))
            return new AdminProviderConfigMutationResult(false, "invalid_bitails_transport");

        var normalized = new RealtimeSourcePolicyOverrideDocument
        {
            Id = RealtimeSourcePolicyOverrideDocument.DocumentId,
            PrimaryRealtimeSource = realtimePrimary,
            RawTxPrimaryProvider = rawTxPrimary,
            RestPrimaryProvider = restPrimary,
            BitailsTransport = bitailsTransport,
            BitailsApiKey = NormalizeOptional(request.Bitails?.ApiKey),
            BitailsBaseUrl = NormalizeOptional(request.Bitails?.BaseUrl),
            BitailsWebsocketBaseUrl = NormalizeOptional(request.Bitails?.WebsocketBaseUrl),
            BitailsZmqTxUrl = NormalizeOptional(request.Bitails?.ZmqTxUrl),
            BitailsZmqBlockUrl = NormalizeOptional(request.Bitails?.ZmqBlockUrl),
            WhatsonchainApiKey = NormalizeOptional(request.Whatsonchain?.ApiKey),
            WhatsonchainBaseUrl = NormalizeOptional(request.Whatsonchain?.BaseUrl),
            JungleBusBaseUrl = NormalizeOptional(request.Junglebus?.BaseUrl),
            JungleBusMempoolSubscriptionId = NormalizeOptional(request.Junglebus?.MempoolSubscriptionId),
            JungleBusBlockSubscriptionId = NormalizeOptional(request.Junglebus?.BlockSubscriptionId),
            UpdatedBy = updatedBy
        };

        var validationError = ValidateRequestedConfiguration(normalized, staticConfig, allowedBitailsTransports);
        if (validationError is not null)
            return new AdminProviderConfigMutationResult(false, validationError);

        var staticValues = BuildConfigValues(staticConfig, new JungleBusProviderRuntimeSnapshot(
            NormalizeOptional(staticConfig.Providers.JungleBus.Connection.BaseUrl),
            NormalizeOptional(appConfig.Value.JungleBus.MempoolSubscriptionId),
            NormalizeOptional(appConfig.Value.JungleBus.BlockSubscriptionId)));
        var requestedValues = BuildOverrideValues(normalized);
        if (ConfigurationMatches(staticValues, requestedValues))
        {
            await overrideStore.SaveAsync(BuildDefaultProviderConfigDocument(updatedBy), cancellationToken);
            return new AdminProviderConfigMutationResult(true);
        }

        await overrideStore.SaveAsync(normalized, cancellationToken);
        return new AdminProviderConfigMutationResult(true);
    }

    public async Task<AdminProvidersResponse> ResetProviderConfigAsync(CancellationToken cancellationToken = default)
    {
        await overrideStore.SaveAsync(BuildDefaultProviderConfigDocument("admin-reset"), cancellationToken);
        return await GetProvidersAsync(cancellationToken);
    }

    private async Task<RealtimeSourcePolicyOverrideDocument> GetPersistedProviderConfigDocumentAsync(CancellationToken cancellationToken)
    {
        var existing = await overrideStore.GetAsync(cancellationToken);
        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.RawTxPrimaryProvider))
            {
                existing.RawTxPrimaryProvider = Normalize(sourcesConfig.Value.Capabilities.RawTxFetch.Source) ?? RecommendedRawTxProvider;
                existing.UpdatedBy ??= "migration-defaults";
                await overrideStore.SaveAsync(existing, cancellationToken);
            }

            return existing;
        }

        var seeded = BuildDefaultProviderConfigDocument("system-defaults");
        await overrideStore.SaveAsync(seeded, cancellationToken);
        return seeded;
    }

    private RealtimeSourcePolicyOverrideDocument BuildDefaultProviderConfigDocument(string updatedBy)
        => new()
        {
            Id = RealtimeSourcePolicyOverrideDocument.DocumentId,
            PrimaryRealtimeSource = Normalize(sourcesConfig.Value.Routing.PrimarySource) ?? RecommendedRealtimeProvider,
            RawTxPrimaryProvider = Normalize(sourcesConfig.Value.Capabilities.RawTxFetch.Source) ?? RecommendedRawTxProvider,
            RestPrimaryProvider = Normalize(GetDefaultRestPrimaryProvider(sourcesConfig.Value)) ?? RecommendedRestProvider,
            BitailsTransport = Normalize(sourcesConfig.Value.Providers.Bitails.Connection.Transport) ?? BitailsRealtimeTransportMode.Websocket,
            BitailsApiKey = NormalizeOptional(sourcesConfig.Value.Providers.Bitails.Connection.ApiKey),
            BitailsBaseUrl = NormalizeOptional(sourcesConfig.Value.Providers.Bitails.Connection.BaseUrl),
            BitailsWebsocketBaseUrl = NormalizeOptional(sourcesConfig.Value.Providers.Bitails.Connection.Websocket.BaseUrl),
            BitailsZmqTxUrl = NormalizeOptional(sourcesConfig.Value.Providers.Bitails.Connection.Zmq.TxUrl),
            BitailsZmqBlockUrl = NormalizeOptional(sourcesConfig.Value.Providers.Bitails.Connection.Zmq.BlockUrl),
            WhatsonchainApiKey = NormalizeOptional(sourcesConfig.Value.Providers.Whatsonchain.Connection.ApiKey),
            WhatsonchainBaseUrl = NormalizeOptional(sourcesConfig.Value.Providers.Whatsonchain.Connection.BaseUrl),
            JungleBusBaseUrl = NormalizeOptional(sourcesConfig.Value.Providers.JungleBus.Connection.BaseUrl),
            JungleBusMempoolSubscriptionId = NormalizeOptional(appConfig.Value.JungleBus.MempoolSubscriptionId),
            JungleBusBlockSubscriptionId = NormalizeOptional(appConfig.Value.JungleBus.BlockSubscriptionId),
            UpdatedBy = updatedBy
        };

    private static string GetDefaultRestPrimaryProvider(ConsigliereSourcesConfig config)
    {
        var fallback = config.Capabilities.RawTxFetch.FallbackSources.FirstOrDefault();
        return string.IsNullOrWhiteSpace(fallback) ? RecommendedRestProvider : fallback;
    }

    private AdminProviderCatalogItemResponse[] BuildCatalog(
        ConsigliereSourcesConfig staticConfig,
        ConsigliereSourcesConfig effectiveConfig,
        JungleBusProviderRuntimeSnapshot effectiveJungleBus,
        IReadOnlyDictionary<string, ExternalChainProviderDescriptor> descriptors)
    {
        var effectiveValues = BuildConfigValues(effectiveConfig, effectiveJungleBus);
        return
        [
            BuildProviderCard(ExternalChainProviderName.Bitails, "Bitails", descriptors, staticConfig, effectiveValues),
            BuildProviderCard(ExternalChainProviderName.WhatsOnChain, "WhatsOnChain", descriptors, staticConfig, effectiveValues),
            BuildProviderCard(ExternalChainProviderName.JungleBus, "JungleBus", descriptors, staticConfig, effectiveValues, effectiveJungleBus),
            BuildNodeZmqCard(staticConfig, effectiveValues)
        ];
    }

    private AdminProviderCatalogItemResponse BuildProviderCard(
        string providerId,
        string displayName,
        IReadOnlyDictionary<string, ExternalChainProviderDescriptor> descriptors,
        ConsigliereSourcesConfig staticConfig,
        AdminProviderConfigValuesResponse effectiveValues,
        JungleBusProviderRuntimeSnapshot effectiveJungleBus = null)
    {
        descriptors.TryGetValue(providerId, out var descriptor);
        var activeFor = new List<string>();
        if (string.Equals(effectiveValues.RealtimePrimaryProvider, providerId, StringComparison.OrdinalIgnoreCase))
            activeFor.Add("realtime");
        if (string.Equals(effectiveValues.RawTxPrimaryProvider, providerId, StringComparison.OrdinalIgnoreCase))
            activeFor.Add("raw_tx_fetch");
        if (string.Equals(effectiveValues.RestPrimaryProvider, providerId, StringComparison.OrdinalIgnoreCase))
            activeFor.Add("rest_fallback");

        var missing = providerId switch
        {
            ExternalChainProviderName.Bitails => GetBitailsMissingRequirements(effectiveValues),
            ExternalChainProviderName.WhatsOnChain => GetWhatsonchainMissingRequirements(effectiveValues),
            ExternalChainProviderName.JungleBus => GetJungleBusMissingRequirements(effectiveValues, effectiveJungleBus),
            _ => []
        };

        return new AdminProviderCatalogItemResponse
        {
            ProviderId = providerId,
            DisplayName = displayName,
            Roles = providerId switch
            {
                ExternalChainProviderName.Bitails => ["realtime", "rest", "history"],
                ExternalChainProviderName.WhatsOnChain => ["rest"],
                ExternalChainProviderName.JungleBus => ["realtime", "raw_tx_fetch", "block_backfill"],
                _ => []
            },
            SupportedCapabilities = descriptor?.Capabilities?.ToArray() ?? [],
            RecommendedFor = providerId switch
            {
                ExternalChainProviderName.Bitails => ["realtime"],
                ExternalChainProviderName.WhatsOnChain => ["rest"],
                ExternalChainProviderName.JungleBus => ["raw_tx_fetch"],
                _ => []
            },
            ActiveFor = [.. activeFor],
            Status = activeFor.Count > 0
                ? "active"
                : missing.Length > 0
                    ? "missing_requirements"
                    : IsConfigured(providerId, staticConfig, effectiveValues, effectiveJungleBus)
                        ? "configured"
                        : "not_configured",
            Description = providerId switch
            {
                ExternalChainProviderName.Bitails => "Recommended managed realtime provider. Websocket is the default first-run onboarding path, with API key optional at start and available later for paid or higher-limit usage. Also supports ZMQ mode and provider-backed history access.",
                ExternalChainProviderName.WhatsOnChain => "Recommended REST default for simple fallback and onboarding flows. Keep it as the easy starter path rather than the preferred raw transaction source.",
                ExternalChainProviderName.JungleBus => "Recommended practical raw transaction source through GorillaPool transaction-get, while remaining an advanced realtime option for operators who already understand JungleBus subscription setup.",
                _ => string.Empty
            },
            MissingRequirements = missing,
            HelpLinks = BuildHelpLinks(providerId)
        };
    }

    private static AdminProviderCatalogItemResponse BuildNodeZmqCard(
        ConsigliereSourcesConfig staticConfig,
        AdminProviderConfigValuesResponse effectiveValues)
    {
        var roles = new List<string>();
        if (string.Equals(effectiveValues.RealtimePrimaryProvider, SourceCapabilityRouting.NodeProvider, StringComparison.OrdinalIgnoreCase))
            roles.Add("realtime");

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(staticConfig.Providers.Node.Connection.ZmqTxUrl) &&
            string.IsNullOrWhiteSpace(staticConfig.Providers.Node.Connection.ZmqBlockUrl))
        {
            missing.Add("node_zmq_endpoint_required");
        }

        return new AdminProviderCatalogItemResponse
        {
            ProviderId = SourceCapabilityRouting.NodeProvider,
            DisplayName = "ZMQ / Node",
            Roles = ["realtime", "infrastructure"],
            SupportedCapabilities = [ExternalChainCapability.RealtimeIngest, ExternalChainCapability.BlockBackfill, ExternalChainCapability.ValidationFetch],
            RecommendedFor = ["advanced"],
            ActiveFor = roles.ToArray(),
            Status = roles.Count > 0 ? "active" : missing.Count > 0 ? "missing_requirements" : "configured",
            Description = "Advanced infrastructure path using self-hosted node ZMQ or a managed ZMQ-compatible feed. Best when the operator controls the underlying node path.",
            MissingRequirements = missing.ToArray(),
            HelpLinks =
            [
                new AdminProviderLinkResponse { Label = "Bitails Docs", Url = "https://docs.bitails.io/" }
            ]
        };
    }

    private static AdminProviderLinkResponse[] BuildHelpLinks(string providerId)
        => providerId switch
        {
            ExternalChainProviderName.Bitails =>
            [
                new AdminProviderLinkResponse { Label = "Bitails Docs", Url = "https://docs.bitails.io/" },
                new AdminProviderLinkResponse { Label = "Bitails", Url = "https://bitails.io/" }
            ],
            ExternalChainProviderName.WhatsOnChain =>
            [
                new AdminProviderLinkResponse { Label = "WhatsOnChain Docs", Url = "https://docs.whatsonchain.com/" }
            ],
            ExternalChainProviderName.JungleBus =>
            [
                new AdminProviderLinkResponse { Label = "GorillaPool Developer Resources", Url = "https://gorillapool.com/dev-resources" }
            ],
            _ => []
        };

    private static bool IsConfigured(
        string providerId,
        ConsigliereSourcesConfig staticConfig,
        AdminProviderConfigValuesResponse effectiveValues,
        JungleBusProviderRuntimeSnapshot effectiveJungleBus)
        => providerId switch
        {
            ExternalChainProviderName.Bitails => !string.IsNullOrWhiteSpace(effectiveValues.Bitails.BaseUrl)
                || !string.IsNullOrWhiteSpace(effectiveValues.Bitails.WebsocketBaseUrl)
                || !string.IsNullOrWhiteSpace(effectiveValues.Bitails.ZmqTxUrl)
                || !string.IsNullOrWhiteSpace(effectiveValues.Bitails.ZmqBlockUrl),
            ExternalChainProviderName.WhatsOnChain => !string.IsNullOrWhiteSpace(effectiveValues.Whatsonchain.BaseUrl)
                || staticConfig.Providers.Whatsonchain.Enabled,
            ExternalChainProviderName.JungleBus => !string.IsNullOrWhiteSpace(effectiveJungleBus?.MempoolSubscriptionId)
                || !string.IsNullOrWhiteSpace(effectiveJungleBus?.BlockSubscriptionId)
                || !string.IsNullOrWhiteSpace(effectiveJungleBus?.BaseUrl),
            _ => false
        };

    private static string[] GetBitailsMissingRequirements(AdminProviderConfigValuesResponse effectiveValues)
    {
        var missing = new List<string>();
        if (string.Equals(effectiveValues.RealtimePrimaryProvider, ExternalChainProviderName.Bitails, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(effectiveValues.BitailsTransport, BitailsRealtimeTransportMode.Websocket, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(effectiveValues.Bitails.WebsocketBaseUrl) &&
                string.IsNullOrWhiteSpace(effectiveValues.Bitails.BaseUrl))
            {
                missing.Add("bitails_websocket_endpoint_required");
            }

            if (string.Equals(effectiveValues.BitailsTransport, BitailsRealtimeTransportMode.Zmq, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(effectiveValues.Bitails.ZmqTxUrl) &&
                string.IsNullOrWhiteSpace(effectiveValues.Bitails.ZmqBlockUrl))
            {
                missing.Add("bitails_zmq_endpoint_required");
            }
        }

        if (string.Equals(effectiveValues.RestPrimaryProvider, ExternalChainProviderName.Bitails, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(effectiveValues.Bitails.BaseUrl))
        {
            missing.Add("bitails_rest_base_url_required");
        }

        if (string.Equals(effectiveValues.RawTxPrimaryProvider, ExternalChainProviderName.Bitails, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(effectiveValues.Bitails.BaseUrl))
        {
            missing.Add("bitails_rest_base_url_required");
        }

        return missing.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string[] GetWhatsonchainMissingRequirements(AdminProviderConfigValuesResponse effectiveValues)
    {
        var missing = new List<string>();
        if (string.Equals(effectiveValues.RestPrimaryProvider, ExternalChainProviderName.WhatsOnChain, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(effectiveValues.Whatsonchain.BaseUrl))
        {
            missing.Add("whatsonchain_base_url_required");
        }

        if (string.Equals(effectiveValues.RawTxPrimaryProvider, ExternalChainProviderName.WhatsOnChain, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(effectiveValues.Whatsonchain.BaseUrl))
        {
            missing.Add("whatsonchain_base_url_required");
        }

        return missing.ToArray();
    }

    private static string[] GetJungleBusMissingRequirements(
        AdminProviderConfigValuesResponse effectiveValues,
        JungleBusProviderRuntimeSnapshot effectiveJungleBus)
    {
        var missing = new List<string>();

        if (string.Equals(effectiveValues.RealtimePrimaryProvider, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(effectiveJungleBus?.MempoolSubscriptionId))
            missing.Add("junglebus_mempool_subscription_id_required");

        if ((string.Equals(effectiveValues.RawTxPrimaryProvider, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(effectiveValues.RestPrimaryProvider, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase)) &&
            string.IsNullOrWhiteSpace(effectiveJungleBus?.BaseUrl))
        {
            missing.Add("junglebus_base_url_required");
        }

        return missing.ToArray();
    }

    private string[] GetAllowedRealtimePrimaryProviders(ConsigliereSourcesConfig config)
    {
        var descriptors = providerCatalog.GetDescriptors()
            .ToDictionary(x => x.Provider, StringComparer.OrdinalIgnoreCase);

        return CandidateRealtimePrimarySources
            .Where(provider => CanServeRealtime(provider, config, descriptors))
            .ToArray();
    }

    private string[] GetAllowedRestPrimaryProviders(ConsigliereSourcesConfig config)
    {
        var descriptors = providerCatalog.GetDescriptors()
            .ToDictionary(x => x.Provider, StringComparer.OrdinalIgnoreCase);

        return CandidateRestPrimaryProviders
            .Where(provider => CanServeRest(provider, config, descriptors))
            .ToArray();
    }

    private string[] GetAllowedRawTxPrimaryProviders(ConsigliereSourcesConfig config)
    {
        var descriptors = providerCatalog.GetDescriptors()
            .ToDictionary(x => x.Provider, StringComparer.OrdinalIgnoreCase);

        return CandidateRawTxPrimaryProviders
            .Where(provider => CanServeRawTx(provider, config, descriptors))
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

        if (result.Count == 0)
        {
            result.Add(BitailsRealtimeTransportMode.Websocket);
            result.Add(BitailsRealtimeTransportMode.Zmq);
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string ValidateRequestedConfiguration(
        RealtimeSourcePolicyOverrideDocument requested,
        ConsigliereSourcesConfig staticConfig,
        string[] allowedBitailsTransports)
    {
        if (string.Equals(requested.PrimaryRealtimeSource, ExternalChainProviderName.Bitails, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(requested.BitailsTransport, BitailsRealtimeTransportMode.Websocket, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(requested.BitailsWebsocketBaseUrl) &&
                string.IsNullOrWhiteSpace(requested.BitailsBaseUrl) &&
                string.IsNullOrWhiteSpace(staticConfig.Providers.Bitails.Connection.Websocket.BaseUrl) &&
                string.IsNullOrWhiteSpace(staticConfig.Providers.Bitails.Connection.BaseUrl))
            {
                return "bitails_websocket_endpoint_required";
            }

            if (string.Equals(requested.BitailsTransport, BitailsRealtimeTransportMode.Zmq, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(requested.BitailsZmqTxUrl) &&
                string.IsNullOrWhiteSpace(requested.BitailsZmqBlockUrl) &&
                string.IsNullOrWhiteSpace(staticConfig.Providers.Bitails.Connection.Zmq.TxUrl) &&
                string.IsNullOrWhiteSpace(staticConfig.Providers.Bitails.Connection.Zmq.BlockUrl))
            {
                return "bitails_zmq_endpoint_required";
            }
        }

        if (string.Equals(requested.PrimaryRealtimeSource, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(requested.JungleBusMempoolSubscriptionId))
        {
            return "junglebus_mempool_subscription_id_required";
        }

        if (string.Equals(requested.RawTxPrimaryProvider, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(requested.JungleBusBaseUrl) &&
            string.IsNullOrWhiteSpace(staticConfig.Providers.JungleBus.Connection.BaseUrl))
        {
            return "junglebus_base_url_required";
        }

        if (string.Equals(requested.RawTxPrimaryProvider, ExternalChainProviderName.WhatsOnChain, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(requested.WhatsonchainBaseUrl) &&
            string.IsNullOrWhiteSpace(staticConfig.Providers.Whatsonchain.Connection.BaseUrl))
        {
            return "whatsonchain_base_url_required";
        }

        if (string.Equals(requested.RawTxPrimaryProvider, ExternalChainProviderName.Bitails, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(requested.BitailsBaseUrl) &&
            string.IsNullOrWhiteSpace(staticConfig.Providers.Bitails.Connection.BaseUrl))
        {
            return "bitails_rest_base_url_required";
        }

        if (string.Equals(requested.RestPrimaryProvider, ExternalChainProviderName.WhatsOnChain, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(requested.WhatsonchainBaseUrl) &&
            string.IsNullOrWhiteSpace(staticConfig.Providers.Whatsonchain.Connection.BaseUrl))
        {
            return "whatsonchain_base_url_required";
        }

        if (string.Equals(requested.RestPrimaryProvider, ExternalChainProviderName.Bitails, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(requested.BitailsBaseUrl) &&
            string.IsNullOrWhiteSpace(staticConfig.Providers.Bitails.Connection.BaseUrl))
        {
            return "bitails_rest_base_url_required";
        }

        if (!allowedBitailsTransports.Contains(requested.BitailsTransport, StringComparer.OrdinalIgnoreCase))
            return "invalid_bitails_transport";

        return null;
    }

    private static bool ConfigurationMatches(
        AdminProviderConfigValuesResponse baseline,
        AdminProviderConfigValuesResponse requested)
        => string.Equals(baseline.RealtimePrimaryProvider, requested.RealtimePrimaryProvider, StringComparison.OrdinalIgnoreCase)
           && string.Equals(baseline.RawTxPrimaryProvider, requested.RawTxPrimaryProvider, StringComparison.OrdinalIgnoreCase)
           && string.Equals(baseline.RestPrimaryProvider, requested.RestPrimaryProvider, StringComparison.OrdinalIgnoreCase)
           && string.Equals(baseline.BitailsTransport, requested.BitailsTransport, StringComparison.OrdinalIgnoreCase)
           && string.Equals(baseline.Bitails.ApiKey, requested.Bitails.ApiKey, StringComparison.Ordinal)
           && string.Equals(baseline.Bitails.BaseUrl, requested.Bitails.BaseUrl, StringComparison.Ordinal)
           && string.Equals(baseline.Bitails.WebsocketBaseUrl, requested.Bitails.WebsocketBaseUrl, StringComparison.Ordinal)
           && string.Equals(baseline.Bitails.ZmqTxUrl, requested.Bitails.ZmqTxUrl, StringComparison.Ordinal)
           && string.Equals(baseline.Bitails.ZmqBlockUrl, requested.Bitails.ZmqBlockUrl, StringComparison.Ordinal)
           && string.Equals(baseline.Whatsonchain.ApiKey, requested.Whatsonchain.ApiKey, StringComparison.Ordinal)
           && string.Equals(baseline.Whatsonchain.BaseUrl, requested.Whatsonchain.BaseUrl, StringComparison.Ordinal)
           && string.Equals(baseline.Junglebus.BaseUrl, requested.Junglebus.BaseUrl, StringComparison.Ordinal)
           && string.Equals(baseline.Junglebus.MempoolSubscriptionId, requested.Junglebus.MempoolSubscriptionId, StringComparison.Ordinal)
           && string.Equals(baseline.Junglebus.BlockSubscriptionId, requested.Junglebus.BlockSubscriptionId, StringComparison.Ordinal);

    private static AdminProviderConfigValuesResponse BuildConfigValues(
        ConsigliereSourcesConfig config,
        JungleBusProviderRuntimeSnapshot effectiveJungleBus)
        => new()
        {
            RealtimePrimaryProvider = Normalize(config.Capabilities.RealtimeIngest.Source),
            RawTxPrimaryProvider = Normalize(config.Capabilities.RawTxFetch.Source),
            RestPrimaryProvider = Normalize(config.Capabilities.RawTxFetch.FallbackSources.FirstOrDefault()),
            BitailsTransport = Normalize(config.Providers.Bitails.Connection.Transport),
            Bitails = new AdminBitailsProviderConfigResponse
            {
                ApiKey = config.Providers.Bitails.Connection.ApiKey ?? string.Empty,
                BaseUrl = config.Providers.Bitails.Connection.BaseUrl ?? string.Empty,
                WebsocketBaseUrl = config.Providers.Bitails.Connection.Websocket.BaseUrl ?? string.Empty,
                ZmqTxUrl = config.Providers.Bitails.Connection.Zmq.TxUrl ?? string.Empty,
                ZmqBlockUrl = config.Providers.Bitails.Connection.Zmq.BlockUrl ?? string.Empty
            },
            Whatsonchain = new AdminRestProviderConfigResponse
            {
                ApiKey = config.Providers.Whatsonchain.Connection.ApiKey ?? string.Empty,
                BaseUrl = config.Providers.Whatsonchain.Connection.BaseUrl ?? string.Empty
            },
            Junglebus = new AdminJungleBusProviderConfigResponse
            {
                BaseUrl = effectiveJungleBus?.BaseUrl ?? config.Providers.JungleBus.Connection.BaseUrl ?? string.Empty,
                MempoolSubscriptionId = effectiveJungleBus?.MempoolSubscriptionId ?? string.Empty,
                BlockSubscriptionId = effectiveJungleBus?.BlockSubscriptionId ?? string.Empty
            }
        };

    private static AdminProviderConfigValuesResponse BuildOverrideValues(RealtimeSourcePolicyOverrideDocument currentOverride)
        => new()
        {
            RealtimePrimaryProvider = Normalize(currentOverride.PrimaryRealtimeSource),
            RawTxPrimaryProvider = Normalize(currentOverride.RawTxPrimaryProvider),
            RestPrimaryProvider = Normalize(currentOverride.RestPrimaryProvider),
            BitailsTransport = Normalize(currentOverride.BitailsTransport),
            Bitails = new AdminBitailsProviderConfigResponse
            {
                ApiKey = currentOverride.BitailsApiKey ?? string.Empty,
                BaseUrl = currentOverride.BitailsBaseUrl ?? string.Empty,
                WebsocketBaseUrl = currentOverride.BitailsWebsocketBaseUrl ?? string.Empty,
                ZmqTxUrl = currentOverride.BitailsZmqTxUrl ?? string.Empty,
                ZmqBlockUrl = currentOverride.BitailsZmqBlockUrl ?? string.Empty
            },
            Whatsonchain = new AdminRestProviderConfigResponse
            {
                ApiKey = currentOverride.WhatsonchainApiKey ?? string.Empty,
                BaseUrl = currentOverride.WhatsonchainBaseUrl ?? string.Empty
            },
            Junglebus = new AdminJungleBusProviderConfigResponse
            {
                BaseUrl = currentOverride.JungleBusBaseUrl ?? string.Empty,
                MempoolSubscriptionId = currentOverride.JungleBusMempoolSubscriptionId ?? string.Empty,
                BlockSubscriptionId = currentOverride.JungleBusBlockSubscriptionId ?? string.Empty
            }
        };

    private static void ApplyOverride(ConsigliereSourcesConfig config, RealtimeSourcePolicyOverrideDocument currentOverride)
    {
        if (currentOverride is null)
            return;

        if (!string.IsNullOrWhiteSpace(currentOverride.PrimaryRealtimeSource))
            config.Capabilities.RealtimeIngest.Source = currentOverride.PrimaryRealtimeSource;
        if (!string.IsNullOrWhiteSpace(currentOverride.RawTxPrimaryProvider))
            config.Capabilities.RawTxFetch.Source = currentOverride.RawTxPrimaryProvider;
        if (!string.IsNullOrWhiteSpace(currentOverride.RestPrimaryProvider))
        {
            config.Capabilities.RawTxFetch.FallbackSources = string.Equals(
                currentOverride.RestPrimaryProvider,
                config.Capabilities.RawTxFetch.Source,
                StringComparison.OrdinalIgnoreCase)
                ? []
                : [currentOverride.RestPrimaryProvider];
        }

        if (!string.IsNullOrWhiteSpace(currentOverride.BitailsTransport))
            config.Providers.Bitails.Connection.Transport = currentOverride.BitailsTransport;
        if (currentOverride.BitailsApiKey is not null)
            config.Providers.Bitails.Connection.ApiKey = currentOverride.BitailsApiKey;
        if (currentOverride.BitailsBaseUrl is not null)
            config.Providers.Bitails.Connection.BaseUrl = currentOverride.BitailsBaseUrl;
        if (currentOverride.BitailsWebsocketBaseUrl is not null)
            config.Providers.Bitails.Connection.Websocket.BaseUrl = currentOverride.BitailsWebsocketBaseUrl;
        if (currentOverride.BitailsZmqTxUrl is not null)
            config.Providers.Bitails.Connection.Zmq.TxUrl = currentOverride.BitailsZmqTxUrl;
        if (currentOverride.BitailsZmqBlockUrl is not null)
            config.Providers.Bitails.Connection.Zmq.BlockUrl = currentOverride.BitailsZmqBlockUrl;

        if (currentOverride.WhatsonchainApiKey is not null)
            config.Providers.Whatsonchain.Connection.ApiKey = currentOverride.WhatsonchainApiKey;
        if (currentOverride.WhatsonchainBaseUrl is not null)
            config.Providers.Whatsonchain.Connection.BaseUrl = currentOverride.WhatsonchainBaseUrl;

        if (currentOverride.JungleBusBaseUrl is not null)
            config.Providers.JungleBus.Connection.BaseUrl = currentOverride.JungleBusBaseUrl;
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

    private static bool CanServeRest(
        string provider,
        ConsigliereSourcesConfig config,
        IReadOnlyDictionary<string, ExternalChainProviderDescriptor> descriptors)
    {
        if (!descriptors.TryGetValue(provider, out var descriptor))
            return false;

        if (!descriptor.Capabilities.Contains(ExternalChainCapability.RawTxFetch, StringComparer.OrdinalIgnoreCase))
            return false;

        SourceProviderConfig providerConfig = provider.ToLowerInvariant() switch
        {
            ExternalChainProviderName.Bitails => config.Providers.Bitails,
            ExternalChainProviderName.WhatsOnChain => config.Providers.Whatsonchain,
            _ => null
        };

        return providerConfig is { Enabled: true } &&
               providerConfig.EnabledCapabilities.Contains(ExternalChainCapability.RawTxFetch, StringComparer.OrdinalIgnoreCase);
    }

    private static bool CanServeRawTx(
        string provider,
        ConsigliereSourcesConfig config,
        IReadOnlyDictionary<string, ExternalChainProviderDescriptor> descriptors)
    {
        if (!descriptors.TryGetValue(provider, out var descriptor))
            return false;

        if (!descriptor.Capabilities.Contains(ExternalChainCapability.RawTxFetch, StringComparer.OrdinalIgnoreCase))
            return false;

        SourceProviderConfig providerConfig = provider.ToLowerInvariant() switch
        {
            ExternalChainProviderName.Bitails => config.Providers.Bitails,
            ExternalChainProviderName.WhatsOnChain => config.Providers.Whatsonchain,
            ExternalChainProviderName.JungleBus => config.Providers.JungleBus,
            _ => null
        };

        return providerConfig is { Enabled: true } &&
               providerConfig.EnabledCapabilities.Contains(ExternalChainCapability.RawTxFetch, StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string NormalizeOptional(string value)
        => value is null ? null : value.Trim();

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
