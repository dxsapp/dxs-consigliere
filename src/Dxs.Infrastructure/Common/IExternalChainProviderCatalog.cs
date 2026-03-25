using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Infrastructure.Common;

public interface IExternalChainProviderCatalog
{
    IReadOnlyCollection<ExternalChainProviderDescriptor> GetDescriptors();
    Task<IReadOnlyCollection<ExternalChainProviderHealthSnapshot>> GetHealthAsync(CancellationToken cancellationToken = default);
}
