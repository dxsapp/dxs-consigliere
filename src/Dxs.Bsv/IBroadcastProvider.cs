using System.Threading.Tasks;

namespace Dxs.Bsv;

public interface IBroadcastProvider
{
    string Name { get; }

    Task<decimal> SatoshisPerByte();

    Task<(bool success, string message, string code)> Broadcast(string hex);
}
