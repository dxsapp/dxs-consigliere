using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Infrastructure.Common;

public sealed class ExternalChainProviderCatalog(
    IEnumerable<IExternalChainProviderDiagnostics> diagnostics
) : IExternalChainProviderCatalog
{
    private readonly IReadOnlyCollection<IExternalChainProviderDiagnostics> _diagnostics = diagnostics.ToArray();

    public IReadOnlyCollection<ExternalChainProviderDescriptor> GetDescriptors()
        => _diagnostics.Select(x => x.Descriptor).ToArray();

    public async Task<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = new List<ExternalChainProviderHealthSnapshot>(_diagnostics.Count);

        foreach (var diagnostic in _diagnostics)
        {
            snapshots.Add(await diagnostic.GetHealthAsync(cancellationToken));
        }

        return snapshots;
    }
}
