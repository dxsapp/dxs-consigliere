using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Infrastructure.JungleBus;

public interface IJungleBusRawTransactionClient
{
    Task<byte[]> GetTransactionRawOrNullAsync(string txId, CancellationToken cancellationToken = default);
}
