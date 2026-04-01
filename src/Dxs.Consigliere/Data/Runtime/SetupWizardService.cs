using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses.Setup;
using Dxs.Consigliere.Services.Impl;
using Dxs.Consigliere.Setup;

namespace Dxs.Consigliere.Data.Runtime;

public interface ISetupWizardService
{
    SetupStatusResponse GetStatus();
    Task<SetupOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken = default);
    Task<SetupStatusResponse> CompleteAsync(SetupCompleteRequest request, CancellationToken cancellationToken = default);
}

public sealed class SetupWizardService(
    ISetupBootstrapStore setupStore,
    IAdminProviderConfigService providerConfigService
) : ISetupWizardService
{
    public SetupStatusResponse GetStatus()
    {
        var state = GetOrSeed();
        return MapStatus(state);
    }

    public async Task<SetupOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var state = GetOrSeed();
        var providers = await providerConfigService.GetProvidersAsync(cancellationToken);
        var effective = providers.Config.Effective;
        var sources = await providerConfigService.GetEffectiveSourcesConfigAsync(cancellationToken);

        return new SetupOptionsResponse
        {
            Status = MapStatus(state),
            Defaults = new SetupDefaultsResponse
            {
                RawTxPrimaryProvider = providers.Recommendations.RawTxFetchProvider,
                RestFallbackProvider = providers.Recommendations.RestPrimaryProvider,
                RealtimePrimaryProvider = providers.Recommendations.RealtimePrimaryProvider,
                BitailsTransport = effective.BitailsTransport
            },
            Allowed = new SetupAllowedOptionsResponse
            {
                RawTxPrimaryProviders = providers.Config.AllowedRawTxPrimaryProviders,
                RestFallbackProviders = providers.Config.AllowedRestPrimaryProviders,
                RealtimePrimaryProviders = providers.Config.AllowedRealtimePrimaryProviders
                    .Where(x => !string.Equals(x, SourceCapabilityRouting.NodeProvider, StringComparison.OrdinalIgnoreCase))
                    .ToArray(),
                BitailsTransports = providers.Config.AllowedBitailsTransports
            },
            ProviderConfig = new SetupProviderFormDefaultsResponse
            {
                Bitails = new SetupBitailsProviderDefaultsResponse
                {
                    ApiKey = effective.Bitails.ApiKey,
                    BaseUrl = effective.Bitails.BaseUrl,
                    WebsocketBaseUrl = effective.Bitails.WebsocketBaseUrl,
                    ZmqTxUrl = effective.Bitails.ZmqTxUrl,
                    ZmqBlockUrl = effective.Bitails.ZmqBlockUrl
                },
                Whatsonchain = new SetupRestProviderDefaultsResponse
                {
                    ApiKey = effective.Whatsonchain.ApiKey,
                    BaseUrl = effective.Whatsonchain.BaseUrl
                },
                Junglebus = new SetupJungleBusProviderDefaultsResponse
                {
                    ApiKey = sources.Providers.JungleBus.Connection.ApiKey ?? string.Empty,
                    BaseUrl = effective.Junglebus.BaseUrl,
                    MempoolSubscriptionId = effective.Junglebus.MempoolSubscriptionId,
                    BlockSubscriptionId = effective.Junglebus.BlockSubscriptionId
                },
                Node = new SetupNodeProviderDefaultsResponse
                {
                    ZmqTxUrl = sources.Providers.Node.Connection.ZmqTxUrl ?? string.Empty,
                    ZmqBlockUrl = sources.Providers.Node.Connection.ZmqBlockUrl ?? string.Empty
                }
            }
        };
    }

    public async Task<SetupStatusResponse> CompleteAsync(SetupCompleteRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new SetupWizardException("request_required");

        var current = GetOrSeed();
        if (current.SetupCompleted)
            throw new SetupWizardException("setup_already_completed", 409);

        var admin = request.Admin ?? new SetupAdminAccessRequest();
        if (admin.Enabled)
        {
            if (string.IsNullOrWhiteSpace(admin.Username))
                throw new SetupWizardException("admin_username_required");
            if (string.IsNullOrWhiteSpace(admin.Password))
                throw new SetupWizardException("admin_password_required");
        }

        var providerResult = await providerConfigService.ApplyProviderConfigAsync(
            new AdminProviderConfigUpdateRequest
            {
                RawTxPrimaryProvider = request.Providers?.RawTxPrimaryProvider,
                RestPrimaryProvider = request.Providers?.RestFallbackProvider,
                RealtimePrimaryProvider = request.Providers?.RealtimePrimaryProvider,
                BitailsTransport = request.Providers?.BitailsTransport,
                Bitails = request.Providers?.Bitails ?? new AdminBitailsProviderConfigUpdateRequest(),
                Whatsonchain = request.Providers?.Whatsonchain ?? new AdminRestProviderConfigUpdateRequest(),
                Junglebus = request.Providers?.Junglebus ?? new AdminJungleBusProviderConfigUpdateRequest()
            },
            admin.Enabled ? admin.Username?.Trim() ?? "setup" : "setup",
            cancellationToken);

        if (!providerResult.Success)
            throw new SetupWizardException(providerResult.ErrorCode ?? "invalid_provider_configuration");

        var document = new SetupBootstrapDocument
        {
            Id = SetupBootstrapDocument.DocumentId,
            SetupCompleted = true,
            AdminEnabled = admin.Enabled,
            AdminUsername = admin.Enabled ? admin.Username.Trim() : string.Empty,
            AdminPasswordHash = admin.Enabled ? ConsigliereAdminPasswordHash.Hash(admin.Password) : string.Empty,
            UpdatedBy = admin.Enabled ? admin.Username.Trim() : "setup"
        };

        await setupStore.SaveAsync(document, cancellationToken);
        return MapStatus(document);
    }

    private SetupBootstrapDocument GetOrSeed()
    {
        var current = setupStore.Get();
        if (current is not null)
            return current;

        var seeded = new SetupBootstrapDocument
        {
            Id = SetupBootstrapDocument.DocumentId,
            SetupCompleted = false,
            AdminEnabled = false,
            AdminUsername = string.Empty,
            AdminPasswordHash = string.Empty,
            UpdatedBy = "system-defaults"
        };

        setupStore.SaveAsync(seeded).GetAwaiter().GetResult();
        return seeded;
    }

    private static SetupStatusResponse MapStatus(SetupBootstrapDocument state)
        => new()
        {
            SetupRequired = !state.SetupCompleted,
            SetupCompleted = state.SetupCompleted,
            AdminEnabled = state.AdminEnabled,
            AdminUsername = state.AdminEnabled ? state.AdminUsername ?? string.Empty : string.Empty
        };
}

public sealed class SetupWizardException(string code, int statusCode = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
