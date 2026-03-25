using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Infrastructure.Common;

public interface IExternalChainProviderDiagnostics
{
    ExternalChainProviderDescriptor Descriptor { get; }

    ValueTask<ExternalChainProviderHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken = default);
}
