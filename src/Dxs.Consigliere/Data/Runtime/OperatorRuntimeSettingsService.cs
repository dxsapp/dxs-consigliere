using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Runtime;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Data.Runtime;

public interface IOperatorRuntimeSettingsService
{
    /// <summary>
    /// Effective P2P-enabled state: the persisted operator override if
    /// present, else the <see cref="BsvP2pConfig.Enabled"/> config seed.
    /// </summary>
    Task<bool> GetP2pEnabledAsync(CancellationToken cancellationToken = default);

    Task SetP2pEnabledAsync(bool enabled, string updatedBy, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads/writes operator runtime toggles. Config is the SEED; the DB
/// document is authoritative once written (wizard-enabled-p2p-runtime-toggle
/// wave, Core Rule 1). The first-run default + the CI/E2E signal (no wizard,
/// no override) is the config value, which stays false by default.
/// </summary>
public sealed class OperatorRuntimeSettingsService(
    IOperatorRuntimeSettingsStore store,
    IOptions<BsvP2pConfig> p2pConfig
) : IOperatorRuntimeSettingsService
{
    public async Task<bool> GetP2pEnabledAsync(CancellationToken cancellationToken = default)
    {
        var doc = await store.GetAsync(cancellationToken);
        return doc?.P2pEnabled ?? p2pConfig.Value.Enabled;
    }

    public async Task SetP2pEnabledAsync(bool enabled, string updatedBy, CancellationToken cancellationToken = default)
    {
        var doc = await store.GetAsync(cancellationToken) ?? new OperatorRuntimeSettingsDocument
        {
            Id = OperatorRuntimeSettingsDocument.DocumentId,
        };
        doc.P2pEnabled = enabled;
        doc.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "system" : updatedBy.Trim();
        await store.SaveAsync(doc, cancellationToken);
    }
}
