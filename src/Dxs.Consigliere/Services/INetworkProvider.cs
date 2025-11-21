using Dxs.Bsv;

namespace Dxs.Consigliere.Services;

public interface INetworkProvider
{
    Network Network { get; }
}
