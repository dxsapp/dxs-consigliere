using System.Threading;
using System.Threading.Tasks;

namespace Dxs.Bsv.Zmq;

public interface IZmqClient
{
    Task Start(CancellationToken cancellationToken);
}
