using Dxs.Consigliere.Data.Models.Runtime;

namespace Dxs.Consigliere.Data.Runtime;

/// <summary>
/// Thin persistence seam for <see cref="OperatorRuntimeSettingsDocument"/>.
/// Kept behind an interface so the seed/override logic in
/// <see cref="OperatorRuntimeSettingsService"/> is unit-testable without a
/// RavenDB server.
/// </summary>
public interface IOperatorRuntimeSettingsStore
{
    Task<OperatorRuntimeSettingsDocument> GetAsync(CancellationToken cancellationToken = default);

    Task<OperatorRuntimeSettingsDocument> SaveAsync(
        OperatorRuntimeSettingsDocument document,
        CancellationToken cancellationToken = default);
}
